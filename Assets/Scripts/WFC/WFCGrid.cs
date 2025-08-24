#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

#endif
using UnityEngine;

public class WFCGrid : MonoBehaviour
{
    [SerializeField] public int DimX = 1;
    [SerializeField] public int DimY = 1;
    [SerializeField] public int DimZ = 1;

    private Vector3Int CachedDimensions = new Vector3Int(1, 1, 1);

    [SerializeField] private WFCNode[] _grid = new WFCNode[0];

    [SerializeField] private WFCNode _nodeObject;
    [SerializeField] private List<WFCNodeOption> _DefaultDomain;
    [SerializeField] private WFCNodeOption _BoundaryOption;
    [SerializeField] public GameObject ErrorObject;
    public WFCNodeOption GetBoundaryOption() => _BoundaryOption;
    public List<WFCNodeOption> GetDeafultDomain() => new(_DefaultDomain);

    [ContextMenu("Update Grid Dimensions")]
    public void UpdateGrid()
    {
        var newDims = new Vector3Int(DimX, DimY, DimZ);
        var newArr = new WFCNode[newDims.x * newDims.y * newDims.z];

        // 1) Move / keep old nodes (do NOT reset their domains)
        if (_grid != null)
        {

            for (int i = 0; i < _grid.Length; i++)
            {
                var node = _grid[i];
                if (node == null) continue;

                var old = FromIndexCached(i); // uses old CachedDimensions
                if (IsWhithinBounds(old))     // uses *new* DimX/Y/Z (your existing helper)
                {
                    int newIdx = old.x + old.y * newDims.x + old.z * newDims.x * newDims.y;
                    newArr[newIdx] = node;

                    // IMPORTANT: keep current domain/seed state — no InitializeNode/ResetDomain here
                    node.transform.SetParent(transform, false);
                    node.transform.localPosition = new Vector3(old.x, old.y, old.z);
                }
                else
                {
                    // out of new bounds: destroy old node
#if UNITY_EDITOR
                    DestroyImmediate(node.gameObject);
#else
                Destroy(node.gameObject);
#endif
                }
            }
        }

        // 2) Create missing nodes (these DO need init + fresh domain)
        for (int idx = 0; idx < newArr.Length; idx++)
        {
            if (newArr[idx] != null) continue;

            var coords = new Vector3Int(
                idx % newDims.x,
                (idx / newDims.x) % newDims.y,
                idx / (newDims.x * newDims.y)
            );

            var go = new GameObject($"WFCNode [{coords.x},{coords.y},{coords.z}]");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(coords.x, coords.y, coords.z);

            var node = go.AddComponent<WFCNode>();
            newArr[idx] = node;

            // initialize ONLY new nodes
            node.InitializeNode(this); // inside, call ResetDomain() (clones default list)
            node.GetEntropy();
        }

        _grid = newArr;
        CachedDimensions = newDims;

        // optional: recompute entropies (non-destructive)
        for (int i = 0; i < _grid.Length; i++)
            _grid[i].GetEntropy();
    }

#if UNITY_EDITOR
    [ContextMenu("WFC/Reset All Domains (non-seed)")]
    private void ResetAllDomainsNonSeed()
    {
        if (_grid == null) return;
        for (int i = 0; i < _grid.Length; i++)
        {
            var n = _grid[i];
            if (n == null) continue;
            if (n.IsSeedLocked) continue;
            n.ResetDomain();
            n.GetEntropy();
        }
    }
#endif



    public int ToIndex(int x, int y, int z)
    {
        return x + y * CachedDimensions.x + z * CachedDimensions.x * CachedDimensions.y;
    }

    public int ToIndex(Vector3Int Coords) => ToIndex(Coords.x, Coords.y, Coords.z);

    public bool IsWhithinBounds(Vector3Int coords)
    {
        return coords.x >= 0 && coords.y >= 0 && coords.z >= 0 &&
            coords.x < DimX && coords.y < DimY && coords.z < DimZ;

    }
    public Vector3Int GetNodeCoords(WFCNode node)
    {
        int index = System.Array.IndexOf(_grid, node);
        if (index < 0) return new Vector3Int(-1, -1, -1);
        return FromIndexCached(index);
    }


    public WFCNode GetNodeAt(Vector3Int coords)
    {
        if (!IsWhithinBounds(coords)) return _grid[ToIndex(new Vector3Int(1, 1, 0))];
        return _grid[ToIndex(coords)];
    }

    public Vector3Int FromIndexCached(int index)
    {
        int xy = CachedDimensions.x * CachedDimensions.y;
        int z = index / xy;
        int rem = index - z * xy;
        int y = rem / CachedDimensions.x;
        int x = rem - y * CachedDimensions.x;
        return new Vector3Int(x, y, z);
    }

    private Vector3Int FromIndex(int index)
    {
        int xy = DimX * DimY;
        int z = index / xy;
        int rem = index - z * xy;
        int y = rem / DimX;
        int x = rem - y * DimX;
        return new Vector3Int(x, y, z);
    }

    private WFCNode CreateNode(int index)
    {
        WFCNode node;

#if UNITY_EDITOR
        node = (WFCNode)PrefabUtility.InstantiatePrefab(_nodeObject, transform);
#else
        node = Instantiate(_nodeObject, transform);
#endif
        node.transform.localPosition = transform.localPosition + FromIndex(index);
        node.InitializeNode(this);
        return node;
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        foreach (WFCNode node in _grid)
        {
#if UNITY_EDITOR
            DestroyImmediate(node.gameObject);
#else
            Destroy(node.gameObject);
#endif

        }
        DimX = 0; DimY = 0; DimZ = 0;
        CachedDimensions = Vector3Int.zero;
        _grid = new WFCNode[0];
    }

    #region WFC Solver V0 (Greedy: min-entropy, no propagation/backtracking)

    public enum SolveV0Result { Done, Progress, Stuck }

#if UNITY_EDITOR
    [ContextMenu("WFC/Solve V0 (Greedy)")]
    private void _ContextMenu_SolveV0()
    {
        var result = SolveV0_Greedy();
        Debug.Log($"[WFC] V0 result: {result}");
    }
#endif

    public SolveV0Result SolveV0_Greedy(int maxSteps = 1_000_000)
    {
        EnsureGridInitializedForSolve();

        // (Re)compute entropies up front
        for (int i = 0; i < _grid.Length; i++)
            _grid[i].GetEntropy();

        int steps = 0;
        bool madeProgress = false;

        while (steps++ < maxSteps)
        {
            int idx = PickMinEntropyIndex();
            if (idx < 0) return SolveV0Result.Done; // all collapsed

            var node = _grid[idx];

            // Node handles legality (boundary + neighbors + rotation)
            if (!node.Collapse())
                return madeProgress ? SolveV0Result.Progress : SolveV0Result.Stuck;

            madeProgress = true;

            // Keep ordering sane by refreshing local entropies
            node.GetEntropy();
            foreach (var nb in EnumerateNeighborsByIndex(idx))
                nb.GetEntropy();
        }

        return madeProgress ? SolveV0Result.Progress : SolveV0Result.Stuck;
    }

    private int PickMinEntropyIndex()
    {
        int best = -1;
        float bestH = float.PositiveInfinity;

        for (int i = 0; i < _grid.Length; i++)
        {
            var n = _grid[i];
            if (n == null || n.IsCollapsed()) continue;

            float h = n.GetCachedEntropy();
            if (h <= 0f) return i; // deterministic win: choose zero-entropy immediately

            if (h < bestH || (Mathf.Approximately(h, bestH) && Random.value < 0.5f))
            {
                bestH = h;
                best = i;
            }
        }
        return best; // -1 => all collapsed
    }

    private IEnumerable<WFCNode> EnumerateNeighborsByIndex(int index)
    {
        var c = FromIndexCached(index);
        var deltas = new[]{
        new Vector3Int( 1,0,0), new Vector3Int(-1,0,0),
        new Vector3Int( 0,1,0), new Vector3Int( 0,-1,0),
        new Vector3Int( 0,0,1), new Vector3Int( 0,0,-1)
    };
        foreach (var d in deltas)
        {
            var n = c + d;
            if (IsWhithinBounds(n)) yield return GetNodeAt(n);
        }
    }

    private void EnsureGridInitializedForSolve()
    {
        if (_grid == null || _grid.Length != DimX * DimY * DimZ)
            UpdateGrid();

        // Defensive: make sure every node has its own domain
        for (int i = 0; i < _grid.Length; i++)
        {
            var n = _grid[i];
            if (n == null) continue;
            if (n.IsSeedLocked) PropagateFrom(i, false, false);
            else if (n.GetDomain() == null || n.GetDomain().Count == 0)
                n.ResetDomain();
        }
    }

    #endregion

    #region WFC Solver V1 (Greedy + propagation)

    public enum SolveV1Result { Done, Progress, Contradiction, Stuck }

#if UNITY_EDITOR
    [ContextMenu("WFC/Solve V1 (Greedy + Propagate)")]
    private void _ContextMenu_SolveV1()
    {
        var result = SolveV1_GreedyPropagate();
        Debug.Log($"[WFC] V1 result: {result}");
    }
#endif

    public SolveV1Result SolveV1_GreedyPropagate(int maxSteps = 1_000_000)
    {
        EnsureGridInitializedForSolve();


        // prime entropies
        for (int i = 0; i < _grid.Length; i++)
            _grid[i].GetEntropy();

        int steps = 0;
        bool madeProgress = false;

        while (steps++ < maxSteps)
        {
            int idx = PickMinEntropyIndex();
            if (idx < 0) return SolveV1Result.Done; // all collapsed

            var node = _grid[idx];

            // Collapse one node (your node handles boundary+neighbor legality).
            if (!node.Collapse())
                return madeProgress ? SolveV1Result.Stuck : SolveV1Result.Stuck;

            madeProgress = true;

            // Propagate constraints outward from the collapsed node.
            // debug=false, strictMutualForUncollapsed=false keeps authoring lenient.
            if (!PropagateFrom(idx, debug: false, strictMutualForUncollapsed: false))
                return SolveV1Result.Contradiction;

            // refresh local entropies (propagate already pruned most neighbors)
            node.GetEntropy();
            foreach (var nb in EnumerateNeighborsByIndex(idx))
                nb.GetEntropy();
        }

        return madeProgress ? SolveV1Result.Progress : SolveV1Result.Stuck;
    }

    /// AC-3 style propagation: BFS outwards pruning neighbor domains.
    /// Returns false if any domain becomes empty (contradiction).
    public bool PropagateFrom(int startIndex, bool debug, bool strictMutualForUncollapsed)
    {
        var q = new Queue<int>();
        var seen = new HashSet<int>();

        q.Enqueue(startIndex);
        seen.Add(startIndex);

        while (q.Count > 0)
        {
            int idx = q.Dequeue();
            var c = FromIndexCached(idx);

            // visit each 6-neighbor
            foreach (var d in new[]{
            new Vector3Int( 1,0,0), new Vector3Int(-1,0,0),
            new Vector3Int( 0,1,0), new Vector3Int( 0,-1,0),
            new Vector3Int( 0,0,1), new Vector3Int( 0,0,-1)
        })
            {
                var nc = c + d;
                if (!IsWhithinBounds(nc)) continue;

                var nb = GetNodeAt(nc);
                if (nb == null || nb.IsCollapsed()) continue;

                // Ask the node to prune its domain against *current* neighbors.
                // Your signature matches (contradiction, debug, strict). See logs. :contentReference[oaicite:1]{index=1}
                bool contradiction;
                bool changed = nb.ConstrainDomainFromNeighbors(out contradiction, debug, strictMutualForUncollapsed);
                if (contradiction) return false;

                if (changed)
                {
                    nb.GetEntropy();
                    // neighbors of this neighbor may now be affected → enqueue them
                    int nIdx = ToIndex(nc);
                    if (!seen.Contains(nIdx))
                    {
                        seen.Add(nIdx);
                        q.Enqueue(nIdx);
                    }
                }
            }
        }

        return true;
    }

    #endregion

    #region WFC Seeding API

    // Generic seeding

    public bool SeedAt(UnityEngine.Vector3Int coords, WFCNodeOption option, int rot = 0, bool debug = false)
    {
        if (!IsWhithinBounds(coords) || option == null) return false;

        var n = GetNodeAt(coords);
        if (!n.SeedCollapse(option, rot)) return false;

        // Propagate constraints from this seed so neighbors prune immediately
        // (uses your existing propagation; strict=false is authoring-friendly)
        if (!PropagateFrom(ToIndex(coords), debug: debug, strictMutualForUncollapsed: false))
        {
            if (debug) UnityEngine.Debug.LogError($"[WFC] Contradiction after seeding {option.name} at {coords}.");
            return false;
        }
        return true;
    }

    // Optional struct & list to predefine seeds in the inspector
    [System.Serializable]
    public struct SeedPlacement
    {
        public string label;
        public WFCNodeOption option;
        public UnityEngine.Vector3Int coords;
        public int rot;
    }
    [SerializeField] private System.Collections.Generic.List<SeedPlacement> _Seeds = new System.Collections.Generic.List<SeedPlacement>();

    /// Apply all serialized seeds (safe to call multiple times; already-seeded nodes are locked)
    public bool ApplySeeds(bool debug = false)
    {
        foreach (var s in _Seeds)
        {
            if (!SeedAt(s.coords, s.option, s.rot, debug))
                return false; // early out on contradiction
        }
        return true;
    }

    #endregion


    private System.Collections.Generic.List<NeighborDirection> GetOobFaces(UnityEngine.Vector3Int c)
    {
        var f = new System.Collections.Generic.List<NeighborDirection>(3);
        if (c.x == 0) f.Add(NeighborDirection.NEGATIVEX);
        if (c.x == DimX - 1) f.Add(NeighborDirection.POSITIVEX);
        if (c.y == 0) f.Add(NeighborDirection.DOWN);
        if (c.y == DimY - 1) f.Add(NeighborDirection.UP);
        if (c.z == 0) f.Add(NeighborDirection.NEGATIVEZ);
        if (c.z == DimZ - 1) f.Add(NeighborDirection.POSITIVEZ);
        return f;
    }

    // Remove options that have no rotation allowing Boundary on ALL OOB faces
    private bool PrefilterNodeDomainForBoundary(WFCNode n)
    {
        var boundary = GetBoundaryOption(); // can be null (then no filtering)
        if (boundary == null) return false;

        var coords = GetNodeCoords(n);
        var oob = GetOobFaces(coords);
        if (oob.Count == 0) return false; // interior cell → nothing to do

        var dom = n.GetDomain();
        if (dom == null || dom.Count == 0) return false;

        var survivors = new System.Collections.Generic.List<WFCNodeOption>(dom.Count);
        for (int i = 0; i < dom.Count; i++)
        {
            var opt = dom[i];
            if (opt == null) continue;

            bool okSomeRot = false;
            for (int rot = 0; rot < 4 && !okSomeRot; rot++)
            {
                bool allFacesOk = true;
                for (int j = 0; j < oob.Count; j++)
                {
                    var face = oob[j];
                    var lst = opt.GetLegatNeighbors(face, rot);
                    // accept reference OR name match (avoids duplicate-asset gotchas)
                    bool allows = false;
                    if (lst != null)
                    {
                        for (int k = 0; k < lst.Count; k++)
                        {
                            var o = lst[k];
                            if (o == null) continue;
                            if (object.ReferenceEquals(o, boundary) ||
                                (!string.IsNullOrEmpty(o.GetName()) && o.GetName() == boundary.GetName()))
                            { allows = true; break; }
                        }
                    }
                    if (!allows) { allFacesOk = false; break; }
                }
                if (allFacesOk) okSomeRot = true;
            }
            if (okSomeRot) survivors.Add(opt);
        }

        if (survivors.Count == dom.Count) return false;
        dom.Clear();
        dom.AddRange(survivors);
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("WFC/Prepare Domains (Boundary + Seeds)")]
    private void _ContextMenu_PrepareDomains()
    {
        PrepareDomains(debug: true);
    }
#endif

    public void PrepareDomains(bool debug = false)
    {
        if (_grid == null || _grid.Length != DimX * DimY * DimZ)
            UpdateGrid(); // structural only; does NOT reset reused nodes

        // A) Boundary prefilter for NON-seed, NON-collapsed nodes
        for (int i = 0; i < _grid.Length; i++)
        {
            var n = _grid[i];
            if (n == null) continue;

            if (!n.IsSeedLocked && !n.IsCollapsed())
            {
                // Ensure a domain exists (some editor workflows leave it null)
                if (n.GetDomain() == null || n.GetDomain().Count == 0)
                    n.ResetDomain();

                PrefilterNodeDomainForBoundary(n);
            }
        }

        // B) Propagate from all collapsed seeds (and any already collapsed nodes)
        for (int i = 0; i < _grid.Length; i++)
        {
            var n = _grid[i];
            if (n == null || !n.IsCollapsed()) continue;

            // lenient mutual check during prep avoids over-pruning while authoring
            if (!PropagateFrom(i, debug: false, strictMutualForUncollapsed: false))
            {
                if (debug) UnityEngine.Debug.LogError($"[WFC] Contradiction after prep from seed at {FromIndexCached(i)}.");
                // early-out if you want; or continue to see all issues
            }
        }

        // C) Refresh entropies once
        for (int i = 0; i < _grid.Length; i++)
            if (_grid[i] != null) _grid[i].GetEntropy();

        if (debug) UnityEngine.Debug.Log("[WFC] Domains prepared (boundary-filtered + seeded propagation).");
    }

}
