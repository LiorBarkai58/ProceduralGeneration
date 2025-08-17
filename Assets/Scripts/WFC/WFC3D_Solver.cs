using System;
using System.Collections.Generic;
using UnityEngine;

public enum BoundaryPolicy { Any, NotAir, SolidOnly }

[Serializable]
public class WFC3DSettings
{
    [Tooltip("Exterior constraint: Any (no restriction), NotAir (no Air on edges), or SolidOnly (respect prototype masks).")]
    public BoundaryPolicy boundary = BoundaryPolicy.NotAir;

    public int maxSteps = 1_000_000;
    public int maxBacktracks = 10_000;
    public int seed = 12345;

    [Tooltip("If true, logs detailed contradiction reports (heavy but very helpful).")]
    public bool verbose = false;
}

public struct WFC3DResult
{
    public bool success;
    public int sx, sy, sz;
    public int[] variantIndex; // length = sx*sy*sz, each entry is chosen variant index (or -1 on failure)
}

public enum WFCEventType { Forbid, Collapse }

public struct WFCEvent
{
    public WFCEventType type;
    public int cell;     // linear index
    public int vIdx;     // variant index (for Collapse or Forbid)
}

// ---- NEW: reasons for pruning ----
public enum RemovalReason
{
    Unknown = 0,
    BoundaryPolicy_NotAir,
    BoundaryPolicy_SolidOnly,
    BoundaryDirective_Must,
    BoundaryDirective_Forbid,
    IncompatibleWithNeighbor
}

public struct RemovalInfo
{
    public bool removed;
    public RemovalReason reason;
    public int fromCell;       // for neighbor-based removals
    public Face viaFace;       // face direction from culprit to this cell
}

public class WFC3DSolver
{
    // Inputs
    readonly NodeSet nodeSet;
    readonly WFC3DSettings settings;
    readonly int sx, sy, sz;

    // Cached
    readonly int V;
    readonly float[] variantWeight;
    readonly System.Random rng;

    // Grid state
    readonly bool[][] domain;         // [cell][variant]
    readonly int[] domainCount;       // [cell]
    readonly float[] weightSum;       // [cell] (entropy uses boundary-adjusted weights)
    readonly Queue<int> queue;
    readonly bool[] inQueue;

    // Boundary awareness
    readonly FaceMask[] boundaryMaskPerCell;

    // Change log & backtracking
    struct Change { public int cell; public int v; }
    readonly List<Change> changes = new List<Change>(1 << 16);
    class ChoicePoint
    {
        public int cell;
        public int changeStart;
        public int[] order;
        public int nextIdx;
    }
    readonly Stack<ChoicePoint> stack = new Stack<ChoicePoint>();

    // Event timeline (for playback)
    readonly List<WFCEvent> events = new List<WFCEvent>(1 << 16);
    public IReadOnlyList<WFCEvent> Events => events;

    // ---- NEW: per-cell per-variant removal details for diagnostics ----
    readonly RemovalInfo[][] removalInfo; // [cell][variant]

    public WFC3DSolver(NodeSet set, int sx, int sy, int sz, WFC3DSettings cfg)
    {
        nodeSet = set ?? throw new ArgumentNullException(nameof(set));
        if (set.variants == null || set.variants.Length == 0)
            throw new ArgumentException("NodeSet has no variants. Did you Build() it?", nameof(set));

        this.sx = sx; this.sy = sy; this.sz = sz;
        settings = cfg ?? new WFC3DSettings();
        V = set.variants.Length;

        rng = (settings.seed >= 0) ? new System.Random(settings.seed) : new System.Random();

        variantWeight = new float[V];
        for (int i = 0; i < V; i++) variantWeight[i] = Mathf.Max(1e-6f, set.variants[i].weight);

        int N = sx * sy * sz;
        domain = new bool[N][];
        domainCount = new int[N];
        weightSum = new float[N];
        queue = new Queue<int>(N);
        inQueue = new bool[N];

        removalInfo = new RemovalInfo[N][];
        for (int c = 0; c < N; c++)
        {
            domain[c] = new bool[V];
            removalInfo[c] = new RemovalInfo[V];
            for (int v = 0; v < V; v++)
            {
                domain[c][v] = true;
                removalInfo[c][v] = new RemovalInfo
                {
                    removed = false,
                    reason = RemovalReason.Unknown,
                    fromCell = -1,
                    viaFace = Face.PX
                };
            }
            domainCount[c] = V;
            weightSum[c] = SumAllWeights();
        }

        // Boundary faces per cell
        boundaryMaskPerCell = new FaceMask[N];
        for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
                for (int x = 0; x < sx; x++)
                {
                    FaceMask m = FaceMask.None;
                    if (x == sx - 1) m |= FaceMask.PX;
                    if (x == 0) m |= FaceMask.NX;
                    if (y == sy - 1) m |= FaceMask.PY;
                    if (y == 0) m |= FaceMask.NY;
                    if (z == sz - 1) m |= FaceMask.PZ;
                    if (z == 0) m |= FaceMask.NZ;
                    boundaryMaskPerCell[Idx(x, y, z)] = m;
                }

        // Boundary policy: NotAir / SolidOnly
        if (settings.boundary != BoundaryPolicy.Any)
        {
            bool checkSolid = settings.boundary == BoundaryPolicy.SolidOnly;
            bool checkNotAir = settings.boundary == BoundaryPolicy.NotAir;

            for (int c = 0; c < N; c++)
            {
                var cellBoundary = boundaryMaskPerCell[c];
                if (cellBoundary == FaceMask.None) continue;

                bool changed = false;

                for (int v = 0; v < V; v++)
                {
                    if (!domain[c][v]) continue;
                    var p = nodeSet.variants[v].proto;

                    if (checkNotAir && nodeSet.airPrototype && p == nodeSet.airPrototype)
                    {
                        RemoveVariant(c, v, RemovalReason.BoundaryPolicy_NotAir);
                        changed = true; continue;
                    }

                    if (checkSolid)
                    {
                        // hook here if you add per-face "solid" mask later
                        // RemoveVariant(c, v, RemovalReason.BoundaryPolicy_SolidOnly);
                    }
                }

                if (changed) Enqueue(c);
            }
            if (!Propagate())
            {
                if (settings.verbose) ExplainAnyZeroDomain("Initial boundary propagation failed");
                throw new Exception("Initial boundary propagation failed.");
            }
        }

        // Apply per-prototype boundary directives (must / forbid)
        for (int c = 0; c < N; c++)
        {
            var cellBoundary = boundaryMaskPerCell[c];
            if (cellBoundary == FaceMask.None) continue;

            bool changed = false;
            for (int v = 0; v < V; v++)
            {
                if (!domain[c][v]) continue;

                var pv = nodeSet.variants[v];
                var p = pv.proto;

                var must = NodePrototype.RotateMaskY(p.mustTouchBoundaryOn, pv.rotY);
                var forbid = NodePrototype.RotateMaskY(p.forbidBoundaryOn, pv.rotY);

                if (must != FaceMask.None &&
                    !NodePrototype.AnyFaceInMaskTouchesBoundary(must, cellBoundary))
                {
                    RemoveVariant(c, v, RemovalReason.BoundaryDirective_Must);
                    changed = true; continue;
                }

                if (forbid != FaceMask.None &&
                    NodePrototype.AnyFaceInMaskTouchesBoundary(forbid, cellBoundary))
                {
                    RemoveVariant(c, v, RemovalReason.BoundaryDirective_Forbid);
                    changed = true; continue;
                }
            }
            if (changed) Enqueue(c);
        }
        if (!Propagate())
        {
            if (settings.verbose) ExplainAnyZeroDomain("Boundary directives propagation failed");
            throw new Exception("Boundary directives propagation failed.");
        }
    }

    public WFC3DResult Solve()
    {
        int steps = 0, backs = 0;

        while (true)
        {
            if (++steps > settings.maxSteps)
            {
                if (settings.verbose) Debug.LogWarning("WFC: Exceeded max steps.");
                return BuildResult(false);
            }

            int cell = PickCellMinEntropy();
            if (cell < 0) return BuildResult(true); // done

            var order = WeightedOrder(cell);
            if (order.Length == 0)
                if (!Backtrack(ref backs)) return BuildResult(false);

            var cp = new ChoicePoint { cell = cell, changeStart = changes.Count, order = order, nextIdx = 0 };
            stack.Push(cp);

            while (true)
            {
                if (cp.nextIdx >= cp.order.Length)
                {
                    if (!Backtrack(ref backs)) return BuildResult(false);
                    if (stack.Count == 0) return BuildResult(false);
                    cp = stack.Peek();
                    continue;
                }

                int chosenV = cp.order[cp.nextIdx++];

                RevertTo(cp.changeStart);
                for (int i = 0; i < cp.nextIdx - 1; i++)
                {
                    int triedV = cp.order[i];
                    if (domain[cp.cell][triedV]) RemoveVariant(cp.cell, triedV, RemovalReason.Unknown);
                }

                if (!CollapseTo(cp.cell, chosenV)) continue;

                if (Propagate()) break; // proceed outer loop
                if (settings.verbose) ExplainAnyZeroDomain("Propagation failed during solve");
            }
        }
    }

    // === Core mechanics ======================================================

    bool Propagate()
    {
        int iterations = 0;
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            inQueue[c] = false;

            var (x, y, z) = Coords(c);

            for (int f = 0; f < 6; f++)
            {
                var face = (Face)f;
                var (nx, ny, nz) = Neighbor(x, y, z, face);
                if (nx < 0 || nx >= sx || ny < 0 || ny >= sy || nz < 0 || nz >= sz)
                    continue;

                int n = Idx(nx, ny, nz);
                if (ReviseNeighbor(c, n, face))
                {
                    if (domainCount[n] == 0) return false;
                    Enqueue(n);
                }
            }
            if (++iterations > settings.maxSteps) return false;
        }
        return true;
    }

    bool ReviseNeighbor(int fromCell, int toCell, Face faceFromToNeighbor)
    {
        bool changed = false;

        for (int b = 0; b < V; b++)
        {
            if (!domain[toCell][b]) continue;

            bool supported = false;
            for (int a = 0; a < V; a++)
            {
                if (!domain[fromCell][a]) continue;
                if (nodeSet.compatible[a, (int)faceFromToNeighbor, b])
                {
                    supported = true; break;
                }
            }

            if (!supported)
            {
                RemoveVariant(toCell, b, RemovalReason.IncompatibleWithNeighbor, fromCell, faceFromToNeighbor);
                changed = true;
            }
        }
        return changed;
    }

    bool CollapseTo(int cell, int chosenV)
    {
        if (!domain[cell][chosenV]) return false;

        // Remove all except chosen
        for (int v = 0; v < V; v++)
        {
            if (v == chosenV) continue;
            if (domain[cell][v]) RemoveVariant(cell, v, RemovalReason.Unknown);
        }

        // Record collapse event
        events.Add(new WFCEvent { type = WFCEventType.Collapse, cell = cell, vIdx = chosenV });

        Enqueue(cell);
        return true;
    }

    void RemoveVariant(int cell, int v, RemovalReason reason, int fromCell = -1, Face via = Face.PX)
    {
        if (!domain[cell][v]) return;

        domain[cell][v] = false;
        domainCount[cell]--;
        weightSum[cell] -= variantWeight[v];
        if (weightSum[cell] < 0f) weightSum[cell] = 0f;
        changes.Add(new Change { cell = cell, v = v });

        // Record forbid event
        events.Add(new WFCEvent { type = WFCEventType.Forbid, cell = cell, vIdx = v });

        // Track diagnostic info
        removalInfo[cell][v] = new RemovalInfo
        {
            removed = true,
            reason = reason,
            fromCell = fromCell,
            viaFace = via
        };
    }

    void Enqueue(int cell)
    {
        if (inQueue[cell]) return;
        inQueue[cell] = true;
        queue.Enqueue(cell);
    }

    void RevertTo(int changeStart)
    {
        // Re-enable all variants removed since 'changeStart'
        for (int i = changes.Count - 1; i >= changeStart; i--)
        {
            var ch = changes[i];
            if (!domain[ch.cell][ch.v])
            {
                domain[ch.cell][ch.v] = true;
                domainCount[ch.cell]++;
                weightSum[ch.cell] += variantWeight[ch.v];

                // Reset removal info because it's no longer removed
                removalInfo[ch.cell][ch.v] = new RemovalInfo
                {
                    removed = false,
                    reason = RemovalReason.Unknown,
                    fromCell = -1,
                    viaFace = Face.PX
                };
            }
        }
        changes.RemoveRange(changeStart, changes.Count - changeStart);

        // NOTE: we keep the recorded events; the generator will filter during playback.
        Array.Clear(inQueue, 0, inQueue.Length);
        queue.Clear();
    }

    bool Backtrack(ref int backs)
    {
        if (++backs > settings.maxBacktracks)
        {
            if (settings.verbose) Debug.LogWarning("WFC: Max backtracks reached.");
            return false;
        }

        if (stack.Count == 0) return false;

        while (stack.Count > 0)
        {
            var cp = stack.Peek();
            if (cp.nextIdx >= cp.order.Length)
            {
                RevertTo(cp.changeStart);
                stack.Pop();
            }
            else return true;
        }
        return false;
    }

    int PickCellMinEntropy()
    {
        int bestCell = -1;
        double bestEntropy = double.PositiveInfinity;
        double tieBreaker = 1e-6;

        int N = sx * sy * sz;
        for (int c = 0; c < N; c++)
        {
            if (domainCount[c] <= 1) continue;

            double H = ShannonEntropy(c);
            H += rng.NextDouble() * tieBreaker;

            if (H < bestEntropy)
            {
                bestEntropy = H;
                bestCell = c;
            }
        }
        return bestCell;
    }

    // ---- Boundary-aware weights ----
    float BoundaryWeightMultiplier(int cell, int v)
    {
        var pv = nodeSet.variants[v];
        var p = pv.proto;
        var cellBoundary = boundaryMaskPerCell[cell];
        if (cellBoundary == FaceMask.None) return 1f;

        float mul = 1f;

        var prefer = NodePrototype.RotateMaskY(p.preferBoundaryOn, pv.rotY);
        var avoid = NodePrototype.RotateMaskY(p.avoidBoundaryOn, pv.rotY);

        if (prefer != FaceMask.None &&
            NodePrototype.AnyFaceInMaskTouchesBoundary(prefer, cellBoundary))
            mul *= Mathf.Max(0.1f, p.preferMultiplier);

        if (avoid != FaceMask.None &&
            NodePrototype.AnyFaceInMaskTouchesBoundary(avoid, cellBoundary))
            mul *= Mathf.Max(0.1f, p.avoidMultiplier);

        return mul;
    }

    int[] WeightedOrder(int cell)
    {
        var tmp = new List<int>(V);
        var weights = new List<double>(V);

        for (int v = 0; v < V; v++)
        {
            if (!domain[cell][v]) continue;
            double w = variantWeight[v] * BoundaryWeightMultiplier(cell, v);
            if (w <= 0) continue;
            tmp.Add(v); weights.Add(w);
        }
        if (tmp.Count == 0) return Array.Empty<int>();
        if (tmp.Count == 1) return new int[] { tmp[0] };

        var order = new List<int>(tmp.Count);
        while (tmp.Count > 0)
        {
            double total = 0.0;
            for (int i = 0; i < weights.Count; i++) total += weights[i];

            double r = rng.NextDouble() * total, acc = 0.0;
            int pick = 0;
            for (int i = 0; i < tmp.Count; i++)
            {
                acc += weights[i];
                if (r <= acc) { pick = i; break; }
            }

            order.Add(tmp[pick]);
            tmp.RemoveAt(pick);
            weights.RemoveAt(pick);
        }

        return order.ToArray();
    }

    double ShannonEntropy(int cell)
    {
        double sum = 0.0;
        for (int v = 0; v < V; v++)
            if (domain[cell][v]) sum += variantWeight[v] * BoundaryWeightMultiplier(cell, v);
        if (sum <= 0.0) return 0.0;

        double H = Math.Log(sum);
        for (int v = 0; v < V; v++)
        {
            if (!domain[cell][v]) continue;
            double w = variantWeight[v] * BoundaryWeightMultiplier(cell, v);
            if (w <= 0) continue;
            double p = w / sum;
            H -= p * Math.Log(p);
        }
        return Math.Max(0.0, H);
    }

    // Build final result from current domains.
    WFC3DResult BuildResult(bool success)
    {
        int N = sx * sy * sz;
        var res = new WFC3DResult
        {
            success = success,
            sx = sx,
            sy = sy,
            sz = sz,
            variantIndex = new int[N]
        };

        if (!success)
        {
            for (int i = 0; i < N; i++) res.variantIndex[i] = -1;
            return res;
        }

        // For each cell, pick the surviving variant with highest boundary-adjusted weight.
        for (int c = 0; c < N; c++)
        {
            int chosen = -1;
            double best = double.NegativeInfinity;

            for (int v = 0; v < V; v++)
            {
                if (!domain[c][v]) continue;
                double w = variantWeight[v] * BoundaryWeightMultiplier(c, v);
                if (w > best) { best = w; chosen = v; }
            }

            res.variantIndex[c] = chosen; // can be -1 if all pruned, which shouldn't happen on success
        }

        return res;
    }

    // ---- Diagnostics ---------------------------------------------------------

    void ExplainAnyZeroDomain(string header)
    {
        int N = sx * sy * sz;
        for (int c = 0; c < N; c++)
        {
            if (domainCount[c] == 0)
            {
                var (x, y, z) = Coords(c);
                var mask = boundaryMaskPerCell[c];
                Debug.LogError($"WFC CONTRADICTION: {header} at cell {c} -> ({x},{y},{z}), boundary={mask}");

                // Print removal reasons per variant
                for (int v = 0; v < V; v++)
                {
                    var info = removalInfo[c][v];
                    if (!info.removed) continue;

                    string proto = nodeSet.variants[v].proto ? nodeSet.variants[v].proto.nodeId : "null";
                    string reason = info.reason.ToString();

                    if (info.reason == RemovalReason.IncompatibleWithNeighbor && info.fromCell >= 0)
                    {
                        var (nx, ny, nz) = Coords(info.fromCell);
                        Debug.LogError($"  - removed [{v}:{proto}] by NEIGHBOR ({nx},{ny},{nz}) via face {info.viaFace}");
                    }
                    else
                    {
                        Debug.LogError($"  - removed [{v}:{proto}] by {reason}");
                    }
                }

                // Also show which neighbors exist
                foreach (Face f in new[] { Face.PX, Face.NX, Face.PY, Face.NY, Face.PZ, Face.NZ })
                {
                    var (nx, ny, nz) = Neighbor(x, y, z, f);
                    bool outside = nx < 0 || nx >= sx || ny < 0 || ny >= sy || nz < 0 || nz >= sz;
                    Debug.LogError($"    neighbor {f}: {(outside ? "OUTSIDE" : $"({nx},{ny},{nz})")}");
                }
                break; // print the first contradiction only
            }
        }
    }

    // --- Helpers ---
    int Idx(int x, int y, int z) => x + sx * (y + sy * z);
    (int x, int y, int z) Coords(int idx)
    {
        int x = idx % sx;
        int t = idx / sx;
        int y = t % sy;
        int z = t / sy;
        return (x, y, z);
    }

    (int nx, int ny, int nz) Neighbor(int x, int y, int z, Face f) => f switch
    {
        Face.PX => (x + 1, y, z),
        Face.NX => (x - 1, y, z),
        Face.PY => (x, y + 1, z),
        Face.NY => (x, y - 1, z),
        Face.PZ => (x, y, z + 1),
        Face.NZ => (x, y, z - 1),
        _ => (x, y, z)
    };

    float SumAllWeights()
    {
        float s = 0f; for (int i = 0; i < V; i++) s += variantWeight[i]; return s;
    }
}
