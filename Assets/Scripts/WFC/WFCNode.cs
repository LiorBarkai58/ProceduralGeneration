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

    public void InitializeNode(WFCGrid parent)
    {
        ParentGrid = parent;
    }

    public void ResetDomain()
    {
        _domain = ParentGrid.GetDeafultDomain();
#if UNITY_EDITOR
        DestroyImmediate(CollapsedObject);
#else
        Destroy(CollapsedObject);
#endif
        CollapsedSO = null;
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

    public bool ConstrainDomainFromNeighbors(out bool contradiction)
    {
        contradiction = false;

        if (_domain == null) { CachedEntropy = 0f; contradiction = true; return false; }

        // If already collapsed, make sure the domain reflects that single choice and entropy is 0.
        if (_isCollapsed)
        {
            bool changed = true;
            if (_domain.Count == 1 && _domain[0] == CollapsedSO) changed = false;
            _domain = new List<WFCNodeOption>(1) { CollapsedSO };
            CachedEntropy = 0f;
            return changed;
        }

        // Keep only options that have at least one rotation consistent with neighbors.
        var survivors = new List<WFCNodeOption>(_domain.Count);
        for (int i = 0; i < _domain.Count; i++)
        {
            var opt = _domain[i];
            if (opt == null) continue;

            // Reuse your rotation/neighbor logic
            if (FindLegalRotations(opt).Count > 0)
                survivors.Add(opt);
        }

        // Detect change / contradiction
        bool domainChanged = survivors.Count != _domain.Count;
        if (survivors.Count == 0)
        {
            _domain = survivors;        // empty domain
            CachedEntropy = 0f;          // treat as contradiction / zero entropy
            contradiction = true;
            return domainChanged;
        }

        // Commit survivors and refresh cached entropy
        _domain = survivors;
        GetEntropy(); // updates CachedEntropy internally

        return domainChanged;
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
}
