#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static WFC3D_EditorUtil;

public class FrequencyLearner : ScriptableWizard
{
    [Header("Input")]
    public Transform[] roomRoots;

    [Header("Output")]
    public NodeSet targetNodeSet;

    [Header("Counting")]
    public bool countPerCell = true;     // bigger footprints count more
    public bool ignoreInactive = true;

    [Header("Weighting")]
    [Tooltip("Laplace smoothing. 0 = raw counts, 1 = add-one smoothing.")]
    public float laplaceAlpha = 0.5f;
    [Tooltip("Sharpen (gamma>1) or soften (0<gamma<1) the distribution.")]
    public float gamma = 1.0f;
    [Tooltip("Normalize so the mean weight is ~1.0.")]
    public bool normalizeWeights = true;

    [MenuItem("WFC3D/Learn Weights From Rooms")]
    static void Open() => DisplayWizard<FrequencyLearner>("Learn Weights", "Learn");

    void OnWizardCreate()
    {
        if (targetNodeSet == null || targetNodeSet.prototypes == null || targetNodeSet.prototypes.Length == 0)
        {
            Debug.LogError("Assign a NodeSet with prototypes.");
            return;
        }

        var counts = new Dictionary<NodePrototype, int>();
        int totalPlacements = 0;

        foreach (var root in roomRoots)
        {
            if (!root) continue;
            var nodes = root.GetComponentsInChildren<NodeAuthoring>(true);
            foreach (var na in nodes)
            {
                if (ignoreInactive && !na.gameObject.activeInHierarchy) continue;

                var proto = ResolvePrototype(na, targetNodeSet);
                if (!proto) continue;

                int add = 1;
                if (countPerCell)
                    add = Mathf.Max(1, na.footprint.x * na.footprint.y * na.footprint.z);

                counts[proto] = (counts.TryGetValue(proto, out var c) ? c : 0) + add;
                totalPlacements += add;
            }
        }

        if (totalPlacements == 0)
        {
            Debug.LogWarning("No nodes found under provided roots.");
            return;
        }

        var protoList = targetNodeSet.prototypes.Where(p => p != null).ToArray();
        float denom = protoList.Sum(p => (counts.TryGetValue(p, out var c) ? c : 0) + laplaceAlpha);
        var weights = new Dictionary<NodePrototype, float>(protoList.Length);

        foreach (var p in protoList)
        {
            float c = (counts.TryGetValue(p, out var cc) ? cc : 0) + laplaceAlpha;
            float prob = Mathf.Max(1e-8f, c / Mathf.Max(1e-8f, denom));
            float w = Mathf.Pow(prob, Mathf.Max(1e-6f, gamma));
            weights[p] = w;
        }

        if (normalizeWeights)
        {
            float mean = weights.Values.Sum() / Mathf.Max(1, weights.Count);
            float scale = (mean > 1e-6f) ? (1f / mean) : 1f;
            foreach (var k in weights.Keys.ToArray())
                weights[k] *= scale;
        }

        // Persist learned weights
        foreach (var p in protoList)
        {
            p.learnedWeight = weights[p];
            p.useLearnedWeight = true;
            EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssets();

        // Rebuild NodeSet so variants pick up updated weights
        targetNodeSet.Build();

        Debug.Log($"Learned weights for {protoList.Length} prototypes from {roomRoots.Length} root(s). Total placements counted: {totalPlacements}.");
    }

    NodePrototype ResolvePrototype(NodeAuthoring na, NodeSet set)
    {
        // 1) Explicit mapping
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

        // 3) nodeIdOverride
        if (!string.IsNullOrEmpty(na.nodeIdOverride))
        {
            foreach (var p in set.prototypes)
                if (p && p.nodeId == na.nodeIdOverride) return p;
        }

        // 4) Fallback: name
        foreach (var p in set.prototypes)
            if (p && p.nodeId == na.gameObject.name) return p;

        return null;
    }
}
#endif
