using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WFCNode : MonoBehaviour
{
    [SerializeField] private List<WFCNodeOption> _domain;
    [SerializeField] private bool _isCollapsed = false;
    [SerializeField] private WFCNodeOption CollapsedSO;
    [SerializeField] private GameObject CollapsedObject;
    [SerializeField] private int WFCOptionRotations = 0;
    [SerializeField] private WFCGrid ParentGrid;
    [SerializeField] private float CachedEntropy = 1;

    public WFCNodeOption GetCollapsedSO() => CollapsedSO;
    public int GetRotation() => WFCOptionRotations;
    public List<WFCNodeOption> GetDomain() => _domain;

    public void InitializeNode(WFCGrid parent)
    {
        ParentGrid = parent;
        ResetDomain();  // clones default list for this node
    }

    public void ResetDomain()
    {
        var def = ParentGrid != null ? ParentGrid.GetDeafultDomain() : null;
        _domain = def != null ? new List<WFCNodeOption>(def) : new List<WFCNodeOption>();

#if UNITY_EDITOR
        if (CollapsedObject != null) DestroyImmediate(CollapsedObject);
#else
    if (CollapsedObject != null) Destroy(CollapsedObject);
#endif
        CollapsedObject = null;
        CollapsedSO = null;
        _isCollapsed = false;
        WFCOptionRotations = 0;
        CachedEntropy = 0f;
    }

    public bool IsCollapsed() => _isCollapsed;

    public List<WFCNodeOption> GetLegalDomainAtEdge(NeighborDirection direction)
    {
        if (_isCollapsed) return CollapsedSO.GetLegatNeighbors(direction, WFCOptionRotations);
        else return ParentGrid.GetDeafultDomain();
    }

    #region Entropy
    public float GetEntropy()
    {
        // Collapsed or empty/singleton domain => zero uncertainty
        if (_isCollapsed || _domain == null || _domain.Count <= 1)
        {
            CachedEntropy = 0f;
            return 0f;
        }

        float sumW = 0.0f;
        float sumWLogW = 0.0f;
        int validCount = 0;

        for (int i = 0; i < _domain.Count; i++)
        {
            var opt = _domain[i];
            if (opt == null) continue;
             
            float w = opt.GetWeight();
            if (w <= 0.0) continue; 

            sumW += w;
            sumWLogW += w * Mathf.Log(w);
            validCount++;
        }

        float Hbits;
        if (validCount <= 1 || sumW <= 0.0)
        {
            Hbits = 0f;
        }
        else
        {
            // Shannon entropy: H = log S − (Σ w log w)/S   (natural log)
            // Convert nats -> bits by multiplying 1/ln(2)
            float Hnats = Mathf.Log(sumW) - (sumWLogW / sumW);
            Hbits = (float)(Hnats * (1.0 / Mathf.Log(2)));
        }

        // Small random tie-breaker (does not affect ordering materially)
        Hbits += Random.value * 1e-6f;

        CachedEntropy = Hbits;
        return Hbits;
    }

    /// Filters this node's domain using current neighbors' legal adjacencies,
    /// then refreshes CachedEntropy. Returns true if the domain changed.
    /// Sets 'contradiction' if the domain becomes empty.
    /// Set debug=true to log why each option was removed.
    public bool ConstrainDomainFromNeighbors(out bool contradiction, bool debug = false, bool strictMutualForUncollapsed = true)
    {
        contradiction = false;

        if (_domain == null)
        {
            if (debug) Debug.LogWarning("[WFC] Domain is null on node " + name);
            _domain = new List<WFCNodeOption>();
            CachedEntropy = 0f;
            contradiction = true;
            return false;
        }

        if (_isCollapsed)
        {
            // Ensure domain reflects the single chosen option
            bool changed = !(_domain.Count == 1 && _domain[0] == CollapsedSO);
            _domain = new List<WFCNodeOption>(1) { CollapsedSO };
            CachedEntropy = 0f;
            return changed;
        }

        var survivors = new List<WFCNodeOption>(_domain.Count);
        foreach (var opt in _domain)
        {
            if (opt == null) continue;

            bool okSomeRot = false;
            for (int rot = 0; rot < 4 && !okSomeRot; rot++)
            {
                if (RotationConsistentAgainstNeighbors(opt, rot, strictMutualForUncollapsed, debug))
                    okSomeRot = true;
            }

            if (okSomeRot) survivors.Add(opt);
            else if (debug) Debug.Log($"[WFC] Removed option '{opt?.name ?? "<null>"}' at {ParentGrid.GetNodeCoords(this)}: no rotation satisfies neighbors.");
        }

        bool changedDomain = survivors.Count != _domain.Count;

        if (survivors.Count == 0)
        {
            _domain = survivors;     // empty
            CachedEntropy = 0f;
            contradiction = true;
            Instantiate(ParentGrid.ErrorObject,transform);
            if (debug) Debug.LogError("[WFC] CONTRADICTION: domain emptied at " + ParentGrid.GetNodeCoords(this));
            return changedDomain;
        }

        _domain = survivors;
        GetEntropy();                // refresh CachedEntropy
        return changedDomain;
    }

    // --- Detailed rotation check with null guards and optional leniency ---
    private bool RotationConsistentAgainstNeighbors(WFCNodeOption option, int rot, bool strictMutualForUncollapsed, bool debug)
    {
        // Faces to test (6-neighborhood)
        NeighborDirection[] dirs = new[]{
        NeighborDirection.UP, NeighborDirection.DOWN,
        NeighborDirection.POSITIVEZ, NeighborDirection.NEGATIVEZ,
        NeighborDirection.POSITIVEX, NeighborDirection.NEGATIVEX
    };

        foreach (var dir in dirs)
        {
            var allowedFromUs = option.GetLegatNeighbors(dir, rot); // may be null
            var myCoords = ParentGrid.GetNodeCoords(this);
            var nbCoords = myCoords + DirToDelta(dir);

            // Out of bounds => boundary face must be allowed (if boundary is set)
            if (!ParentGrid.IsWhithinBounds(nbCoords))
            {
                var boundary = ParentGrid.GetBoundaryOption();
                bool ok = boundary == null || (allowedFromUs != null && allowedFromUs.Contains(boundary));
                if (!ok)
                {
                    if (debug) Debug.Log($"[WFC] Fail '{option.name}' rot {rot} on {dir}: boundary not allowed.");
                    return false;
                }
                continue;
            }

            // In-bounds neighbor
            var nb = ParentGrid.GetNodeAt(nbCoords);
            var back = Opposite(dir);

            if (nb.IsCollapsed())
            {
                // Two-way check vs collapsed neighbor
                bool usAllow = (allowedFromUs != null && allowedFromUs.Contains(nb.CollapsedSO));
                var themList = nb.CollapsedSO.GetLegatNeighbors(back, nb.WFCOptionRotations);
                bool themAllow = (themList != null && themList.Contains(option));
                if (!(usAllow && themAllow))
                {
                    if (debug) Debug.Log($"[WFC] Fail '{option.name}' rot {rot} vs COLLAPSED @ {nbCoords} on {dir}: usAllow={usAllow}, themAllow={themAllow}");
                    return false;
                }
            }
            else
            {
                // UNCOLLAPSED neighbor
                back = Opposite(dir);
                var nbDomain = nb.GetDomain();
                if (nbDomain == null || nbDomain.Count == 0)
                {
                    if (debug) Debug.Log($"[WFC] Fail '{option.name}' rot {rot} on {dir}: neighbor has no domain.");
                    return false;
                }

                allowedFromUs = option.GetLegatNeighbors(dir, rot);

                bool exists = false;

                // >>> FIX #2: in lenient mode, treat a NULL face list as WILDCARD-ALLOW <<<
                if (!strictMutualForUncollapsed && allowedFromUs == null)
                {
                    exists = true; // allow anything for this face during lenient pruning
                }
                else
                {
                    foreach (var nOpt in nbDomain)
                    {
                        if (nOpt == null) continue;
                        if (allowedFromUs == null || !allowedFromUs.Contains(nOpt)) continue;

                        if (!strictMutualForUncollapsed)
                        {
                            exists = true; // we allow at least one of theirs; defer mutual check
                            break;
                        }

                        // strict: there must exist a rotation of neighbor that allows us back
                        for (int r2 = 0; r2 < 4; r2++)
                        {
                            var lst = nOpt.GetLegatNeighbors(back, r2);
                            if (lst != null && lst.Contains(option)) { exists = true; break; }
                        }
                        if (exists) break;
                    }
                }

                if (!exists)
                {
                    if (debug) Debug.Log($"[WFC] Fail '{option.name}' rot {rot} vs UNCOLLAPSED @ {nbCoords} on {dir}: no mutually compatible neighbor option.");
                    return false;
                }
            }
        }

        return true; // all faces satisfied
    }



public float GetCachedEntropy()
    {
        return CachedEntropy;
    }
    #endregion

    #region Collapse Logic
    public bool Collapse()
    {
        if (_isCollapsed || _domain == null || _domain.Count == 0)
            return false;

        // Work on a local pool so we can try multiple options without mutating the real domain.
        var pool = new List<WFCNodeOption>(_domain);

        // Try up to all remaining options (weighted, without replacement).
        int attempts = Mathf.Min(pool.Count, 8); // "a few more times" cap; tune as you like

        for (int t = 0; t < attempts && pool.Count > 0; t++)
        {
            // 1) Pick one option by weight, remove it from the pool
            var candidate = WeightedPickAndRemove(pool);
            if (candidate == null) break;

            // 2) Find all rotations that are consistent with neighbors
            var legalRots = FindLegalRotations(candidate);
            if (legalRots.Count == 0)
                continue;

            // 3) Collapse into a random legal rotation
            int rot = legalRots[Random.Range(0, legalRots.Count)];
            _isCollapsed = true;
            CollapsedSO = candidate;
            WFCOptionRotations = rot;

            if (candidate.GetPrefab() != null)
            {
#if UNITY_EDITOR
                CollapsedObject = (GameObject)PrefabUtility.InstantiatePrefab(candidate.GetPrefab(), transform);
                CollapsedObject.transform.rotation = Quaternion.Euler(0, 90 * WFCOptionRotations, 0);
#else
                CollapsedObject = Instantiate(candidate.GetPrefab(), transform.position, Quaternion.Euler(0,90*WFCOptionRotations,0),transform);
#endif
            }


            return true;
        }

        // No candidate could be placed consistently
        return false;
    }

    private WFCNodeOption WeightedPickAndRemove(List<WFCNodeOption> pool)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            var w = pool[i]?.GetWeight() ?? 0f;
            if (w > 0f) total += w;
        }
        if (total <= 0f) return null;

        float r = Random.Range(0f, total);
        for (int i = 0; i < pool.Count; i++)
        {
            var opt = pool[i];
            float w = (opt?.GetWeight() ?? 0f);
            if (w <= 0f) continue;

            r -= w;
            if (r <= 0f)
            {
                pool.RemoveAt(i);
                return opt;
            }
        }
        // Fallback (numerical edge)
        var last = pool[pool.Count - 1];
        pool.RemoveAt(pool.Count - 1);
        return last;
    }

    // Test all four rotations, return those that keep neighbors consistent
    private List<int> FindLegalRotations(WFCNodeOption option)
    {
        var legal = new List<int>(4);
        for (int rot = 0; rot < 4; rot++)
        {
            if (RotationIsConsistent(option, rot))
                legal.Add(rot);
        }
        return legal;
    }

    private bool RotationIsConsistent(WFCNodeOption option, int rot)
    {
        // Check all 6 directions around this node
        foreach (NeighborDirection dir in new[]{
        NeighborDirection.UP, NeighborDirection.DOWN,
        NeighborDirection.POSITIVEZ, NeighborDirection.POSITIVEX,
        NeighborDirection.NEGATIVEZ, NeighborDirection.NEGATIVEX})
        {
            if (!NeighborCheck(option, rot, dir))
                return false;
        }
        return true;
    }

    private bool NeighborCheck(WFCNodeOption option, int rot, NeighborDirection dir)
    {
        // Where is the neighbor?
        var myCoords = ParentGrid.GetNodeCoords(this);
        var delta = DirToDelta(dir);
        var nCoords = myCoords + delta;
        var allowedFromUs = option.GetLegatNeighbors(dir, rot);

        // Out of bounds => only OK if this option allows the boundary on that side (with our rotation)
        if (!ParentGrid.IsWhithinBounds(nCoords)) 
        {
            var boundary = ParentGrid.GetBoundaryOption(); // null => treat as unconstrained
            return boundary == null || allowedFromUs.Contains(boundary);
        }

        var neighbor = ParentGrid.GetNodeAt(nCoords);

        // Which options does *our* option allow on that side (given our rotation)?
        

        if (neighbor._isCollapsed)
        {
            // 1) Our option must allow the neighbor's collapsed option on this side
            if (!allowedFromUs.Contains(neighbor.CollapsedSO))
                return false;

            // 2) The neighbor's collapsed option+rotation must also allow us (symmetry)
            var backDir = Opposite(dir);
            var neighborAllowsUs = neighbor.CollapsedSO
                .GetLegatNeighbors(backDir, neighbor.WFCOptionRotations)
                .Contains(option);
            return neighborAllowsUs;
        }
        else
        {
            // Neighbor not collapsed: it must still have at least one option (in some rotation)
            // that is mutually compatible with (option,rot) across this edge.
            var backDir = Opposite(dir);

            for (int i = 0; i < neighbor._domain.Count; i++)
            {
                var nOpt = neighbor._domain[i];
                if (nOpt == null) continue;

                // It must be allowed by us
                if (!allowedFromUs.Contains(nOpt)) continue;

                // And there must exist *some* rotation of the neighbor that allows us back
                if (NeighborCanAccept(nOpt, backDir, option))
                    return true;
            }
            return false;
        }
    }

    // Does there exist a rotation of neighborOpt such that it allows 'thisOpt' from 'towardThis'?
    private bool NeighborCanAccept(WFCNodeOption neighborOpt, NeighborDirection towardThis, WFCNodeOption thisOpt)
    {
        // UP/DOWN ignore rotation in your implementation; loops are still fine/correct.
        for (int r = 0; r < 4; r++)
        {
            var list = neighborOpt.GetLegatNeighbors(towardThis, r);
            if (list.Contains(thisOpt)) return true;
        }
        return false;
    }

    private static Vector3Int DirToDelta(NeighborDirection dir)
    {
        switch (dir)
        {
            case NeighborDirection.UP: return new Vector3Int(0, 1, 0);
            case NeighborDirection.DOWN: return new Vector3Int(0, -1, 0);
            case NeighborDirection.POSITIVEX: return new Vector3Int(1, 0, 0);
            case NeighborDirection.NEGATIVEX: return new Vector3Int(-1, 0, 0);
            case NeighborDirection.POSITIVEZ: return new Vector3Int(0, 0, 1);
            case NeighborDirection.NEGATIVEZ: return new Vector3Int(0, 0, -1);
            default: return Vector3Int.zero;
        }
    }

    private static NeighborDirection Opposite(NeighborDirection dir)
    {
        switch (dir)
        {
            case NeighborDirection.UP: return NeighborDirection.DOWN;
            case NeighborDirection.DOWN: return NeighborDirection.UP;
            case NeighborDirection.POSITIVEX: return NeighborDirection.NEGATIVEX;
            case NeighborDirection.NEGATIVEX: return NeighborDirection.POSITIVEX;
            case NeighborDirection.POSITIVEZ: return NeighborDirection.NEGATIVEZ;
            case NeighborDirection.NEGATIVEZ: return NeighborDirection.POSITIVEZ;
            default: return dir;
        }
    }

    #endregion


    #region WFC Seeding (lockable collapse + safe resets)

    [SerializeField] private bool _isSeedLocked = false;
    public bool IsSeedLocked => _isSeedLocked;

    [SerializeField] private WFCNodeOption _seedOption;
    [SerializeField] private int _seedRotation = 0;

    [ContextMenu("Seed")]
    public void InspectorSeed()
    {
        if (!_seedOption) return;
        if (SeedCollapse(_seedOption, _seedRotation % 4))
        {
            // immediately propagate from this node
            var coords = ParentGrid.GetNodeCoords(this);
            var idx = ParentGrid.ToIndex(coords);
            // lenient during authoring
            var ok = ParentGrid.PropagateFrom(idx, debug: true, strictMutualForUncollapsed: false);
            if (!ok) Debug.LogError($"[WFC] Contradiction after seeding {_seedOption.name} at {coords}.");
        }
    }

    /// Manually collapse this node into a specific option+rotation and lock it.
    /// Returns false if option is null.
    public bool SeedCollapse(WFCNodeOption option, int rot = 0)
    {
        if (option == null) return false;

        // commit collapse
        _isCollapsed = true;
        CollapsedSO = option;
        WFCOptionRotations = ((rot % 4) + 4) % 4;
        _isSeedLocked = true;

        // reflect in domain/entropy
        _domain = new System.Collections.Generic.List<WFCNodeOption>(1) { option };
        CachedEntropy = 0f;

        // refresh instance (optional)
        if (CollapsedObject != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(CollapsedObject);
#else
        Destroy(CollapsedObject);
#endif
            CollapsedObject = null;
        }
        // If your option exposes a prefab, instantiate it
        // (replace GetPrefab() with your accessor or comment these two lines out)
        var prefab = option.GetPrefab();
        if (prefab != null)
            CollapsedObject = Instantiate(prefab, transform.position, Quaternion.Euler(0, 90 * WFCOptionRotations, 0), transform);

        return true;
    }

    /// Clear placement only if not a seed (prevents chunk/resets from nuking seeds)
    public void ClearCollapseIfNotLocked()
    {
        if (_isSeedLocked) return;
        _isCollapsed = false;
        CollapsedSO = null;
        WFCOptionRotations = 0;

        if (CollapsedObject != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(CollapsedObject);
#else
        Destroy(CollapsedObject);
#endif
            CollapsedObject = null;
        }
    }

    /// Reset domain only if not a seed
    public void ResetDomainIfNotLocked()
    {
        if (_isSeedLocked) return;
        ResetDomain();
        GetEntropy();
    }

    /// Optional: unlock this node so future resets can clear it
    public void UnlockSeed() => _isSeedLocked = false;

    #endregion

}
