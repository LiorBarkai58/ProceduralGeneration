
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static WFC3D_EditorUtil;

public class AdjacencyFromExamples : ScriptableWizard
{
    [Header("Input")]
    public Transform[] roomRoots;

    [Header("Output")]
    public NodeSet targetNodeSet;

    [Header("Grid")]
    public Vector3 cellSize = Vector3.one;     // size of one logical cell
    public Vector3 cellOrigin = Vector3.zero;  // world origin of the grid
    public float snapTolerance = 0.05f;        // tolerance to consider an object snapped

    [MenuItem("WFC3D/Learn Adjacency From Rooms")]
    static void Open() => DisplayWizard<AdjacencyFromExamples>("Learn Adjacency", "Learn");

    void OnWizardCreate()
    {
        if (targetNodeSet == null || targetNodeSet.prototypes == null || targetNodeSet.prototypes.Length == 0)
        {
            Debug.LogError("Adjacency: Assign a NodeSet with prototypes.");
            return;
        }

        // Build fast lookup: proto -> adjacency record
        var protoList = targetNodeSet.prototypes.Where(p => p != null).Distinct().ToArray();
        var protoIndex = new Dictionary<NodePrototype, int>(protoList.Length);
        for (int i = 0; i < protoList.Length; i++) protoIndex[protoList[i]] = i;

        var adj = new PrototypeAdjacency[protoList.Length];
        for (int i = 0; i < protoList.Length; i++)
        {
            adj[i] = new PrototypeAdjacency { proto = protoList[i] };
        }

        int pairsAdded = 0;

        foreach (var root in roomRoots)
        {
            if (!root) continue;

            // Collect placed instances with their grid coords
            var map = new Dictionary<(int x, int y, int z), NodePrototype>();
            var nodes = root.GetComponentsInChildren<NodeAuthoring>(true);

            foreach (var na in nodes)
            {
                var proto = ResolvePrototype(na, targetNodeSet);
                if (!proto) continue;

                // Quantize to grid
                Vector3 lp = na.transform.position - cellOrigin;
                int gx = Mathf.RoundToInt(lp.x / Mathf.Max(0.0001f, cellSize.x));
                int gy = Mathf.RoundToInt(lp.y / Mathf.Max(0.0001f, cellSize.y));
                int gz = Mathf.RoundToInt(lp.z / Mathf.Max(0.0001f, cellSize.z));

                // Snap check (for authoring mistakes)
                Vector3 snapped = new Vector3(gx * cellSize.x, gy * cellSize.y, gz * cellSize.z);
                if ((lp - snapped).magnitude > snapTolerance)
                {
                    Debug.LogWarning($"Adjacency: {na.name} not on grid (delta {(lp - snapped)}). Snapping anyway.");
                }

                map[(gx, gy, gz)] = proto;
            }

            // For each cell, check 6 neighbors and add directed adjacency (including Air if missing)
            foreach (var kv in map)
            {
                var (x, y, z) = kv.Key;
                var A = kv.Value;
                if (!protoIndex.TryGetValue(A, out int ia)) continue;

                TryDir(+1, 0, 0, Face.PX);
                TryDir(-1, 0, 0, Face.NX);
                TryDir(0, +1, 0, Face.PY);
                TryDir(0, -1, 0, Face.NY);
                TryDir(0, 0, +1, Face.PZ);
                TryDir(0, 0, -1, Face.NZ);

                void TryDir(int dx, int dy, int dz, Face faceFromA)
                {
                    var keyN = (x + dx, y + dy, z + dz);
                    if (map.TryGetValue(keyN, out var B))
                    {
                        // A face -> B
                        var lstA = adj[ia].faces[(int)faceFromA].allowed;
                        if (!lstA.Contains(B)) { lstA.Add(B); pairsAdded++; }

                        // Symmetry: B opp face -> A
                        if (protoIndex.TryGetValue(B, out int ib))
                        {
                            var opp = NodeSet.Opposite(faceFromA);
                            var lstB = adj[ib].faces[(int)opp].allowed;
                            if (!lstB.Contains(A)) { lstB.Add(A); pairsAdded++; }
                        }
                    }
                    else if (targetNodeSet.airPrototype)
                    {
                        var Air = targetNodeSet.airPrototype;
                        // A -> Air
                        var lstA = adj[ia].faces[(int)faceFromA].allowed;
                        if (!lstA.Contains(Air)) { lstA.Add(Air); pairsAdded++; }

                        // Symmetry: Air -> A on opposite face
                        if (protoIndex.TryGetValue(Air, out int iAir))
                        {
                            var opp = NodeSet.Opposite(faceFromA);
                            var lstAir = adj[iAir].faces[(int)opp].allowed;
                            if (!lstAir.Contains(A)) { lstAir.Add(A); pairsAdded++; }
                        }
                    }
                }
            }
        }

        // Ensure Air ↔ Air on all faces (critical for empty regions)
        if (targetNodeSet.airPrototype && targetNodeSet.prototypes != null)
        {
            int iAir = -1;
            for (int i = 0; i < protoList.Length; i++)
                if (protoList[i] == targetNodeSet.airPrototype) { iAir = i; break; }

            if (iAir >= 0)
            {
                for (int f = 0; f < 6; f++)
                {
                    var lst = adj[iAir].faces[f].allowed;
                    if (!lst.Contains(targetNodeSet.airPrototype))
                        lst.Add(targetNodeSet.airPrototype);
                }
            }
        }

        // Persist into NodeSet
        targetNodeSet.learnedAdjacency = adj;
        EditorUtility.SetDirty(targetNodeSet);
        AssetDatabase.SaveAssets();

        // Rebuild compatibility from learned adjacency
        targetNodeSet.Build();

        Debug.Log($"Adjacency: learned from {roomRoots.Length} root(s). Added ~{pairsAdded} directed face pairs. Variants: {targetNodeSet.variants.Length}.");
    }

    NodePrototype ResolvePrototype(NodeAuthoring na, NodeSet set)
    {
        // 1) Explicit override
        if (na.prototypeAssetOverride) return na.prototypeAssetOverride;

        // 2) Match by prefab GUID (computed on the fly)
        string instGuid = GetPrefabGuid(na.gameObject);
        if (!string.IsNullOrEmpty(instGuid))
        {
            foreach (var p in set.prototypes)
            {
                if (!p || !p.prefab) continue;
                string path = AssetDatabase.GetAssetPath(p.prefab);
                if (string.IsNullOrEmpty(path)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (guid == instGuid) return p;
            }
        }

        // 3) ID override
        if (!string.IsNullOrEmpty(na.nodeIdOverride))
        {
            foreach (var p in set.prototypes)
                if (p && p.nodeId == na.nodeIdOverride) return p;
        }

        // 4) Fallback: by instance name
        foreach (var p in set.prototypes)
            if (p && p.nodeId == na.gameObject.name) return p;

        return null;
    }
}
#endif
