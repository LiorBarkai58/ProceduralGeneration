#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

#endif
using UnityEngine;

public class WFCGrid : MonoBehaviour
{
    [SerializeField] private int DimX = 1;
    [SerializeField] private int DimY = 1;
    [SerializeField] private int DimZ = 1;

    private Vector3Int CachedDimensions = new Vector3Int(1,1,1);

    [SerializeField] private WFCNode[] _grid = new WFCNode[0];

    [SerializeField] private WFCNode _nodeObject;
    [SerializeField] private List<WFCNodeOption> _DefaultDomain;
    [SerializeField] private WFCNodeOption _BoundaryOption;
    public WFCNodeOption GetBoundaryOption() => _BoundaryOption;
    public List<WFCNodeOption> GetDeafultDomain() => new(_DefaultDomain);

    [ContextMenu("Update Grid Dimensions")]
    private void UpdateGrid()
    {
        
        WFCNode[] newArr = new WFCNode[DimX * DimY * DimZ];

        int i;
        for (i = 0; i < _grid.Length; i++)
        {
            Vector3Int coords = FromIndexCached(i);

            if (IsWhithinBounds(coords))
            {
                newArr[ToIndex(coords)] = _grid[i];
                newArr[ToIndex(coords)].ResetDomain();
            }
            else
            {
#if UNITY_EDITOR
                DestroyImmediate(_grid[i].gameObject);
#else
                Destroy(_grid[i].gameObject);
#endif
            }
        }

        for (i = 0; i < newArr.Length; i++)
        {
            if (newArr[i] == null) newArr[i] = CreateNode(i);
        }

        _grid = newArr;
        CachedDimensions = new Vector3Int(DimX, DimY, DimZ);

        for (i = 0; i< _grid.Length; i++)
        {
            _grid[i].ConstrainDomainFromNeighbors(out bool contradiction, true, false);
            _grid[i].GetEntropy();
        }

        
        
        
    }

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
            if (n.GetDomain() == null || n.GetDomain().Count == 0)
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
    private bool PropagateFrom(int startIndex, bool debug, bool strictMutualForUncollapsed)
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

}
