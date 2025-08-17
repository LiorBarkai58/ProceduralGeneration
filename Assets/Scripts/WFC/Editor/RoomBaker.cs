#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using static WFC3D_EditorUtil;

public class RoomBaker : ScriptableWizard
{
    public Transform roomRoot;
    public NodeSet targetNodeSet;

    [Header("Asset Output")]
    public string prefabFolder = "Assets/WFC3D/Prefabs";
    public string prototypeFolder = "Assets/WFC3D/Prototypes";

    [MenuItem("WFC3D/Bake Room -> NodeSet")]
    static void Open() => DisplayWizard<RoomBaker>("Bake Room", "Bake");

    void OnWizardCreate()
    {
        if (!roomRoot || !targetNodeSet)
        {
            Debug.LogError("Assign roomRoot and targetNodeSet.");
            return;
        }

        var nodes = roomRoot.GetComponentsInChildren<NodeAuthoring>(true);
        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogWarning("No NodeAuthoring components under roomRoot.");
            return;
        }

        System.IO.Directory.CreateDirectory(prefabFolder);
        System.IO.Directory.CreateDirectory(prototypeFolder);

        var keyToProto = new Dictionary<string, NodePrototype>();
        var collected = new List<NodePrototype>();

        foreach (var na in nodes)
        {
            // 1) Prototype override
            if (na.prototypeAssetOverride)
            {
                NodePrototype protoAsset = na.prototypeAssetOverride;
                string keyString = $"PROTO::{protoAsset.name}";
                if (!keyToProto.ContainsKey(keyString))
                    keyToProto[keyString] = protoAsset;
                continue;
            }

            // 2) Key by prefab GUID / ID / name
            string guid = GetPrefabGuid(na.gameObject);
            string keyStr = !string.IsNullOrEmpty(guid) ? $"GUID::{guid}"
                         : !string.IsNullOrEmpty(na.nodeIdOverride) ? $"ID::{na.nodeIdOverride}"
                         : $"NAME::{na.gameObject.name}";

            if (!keyToProto.TryGetValue(keyStr, out NodePrototype protoAssetFound))
            {
                // Create or reuse prefab asset
                GameObject prefabRef;
                if (!string.IsNullOrEmpty(guid))
                {
                    prefabRef = PrefabUtility.GetCorrespondingObjectFromSource(na.gameObject);
                }
                else
                {
                    string prefabPath = $"{prefabFolder}/{Sanitize(na.nodeIdOverride ?? na.gameObject.name)}.prefab";
                    prefabRef = PrefabUtility.SaveAsPrefabAsset(na.gameObject, prefabPath);
                    guid = AssetDatabase.AssetPathToGUID(prefabPath);
                }

                // Create prototype
                NodePrototype newProto = ScriptableObject.CreateInstance<NodePrototype>();
                newProto.nodeId = !string.IsNullOrEmpty(na.nodeIdOverride) ? na.nodeIdOverride
                               : (!string.IsNullOrEmpty(guid) ? guid : na.gameObject.name);
                newProto.prefab = prefabRef;
                newProto.walkable = na.walkable;
                newProto.allowYRotation = na.allowYRotation;

                // ==== Naming: use prefab/gameObject name as prefix for the asset file ====
                string prefix = prefabRef != null ? prefabRef.name : na.gameObject.name;
                string safePrefix = Sanitize(prefix);
                string safeId = Sanitize(newProto.nodeId);
                string baseName = $"{safePrefix}_{safeId}";      // e.g., WallTile_GUID123.asset
                string rawPath = $"{prototypeFolder}/{baseName}.asset";
                string soPath = AssetDatabase.GenerateUniqueAssetPath(rawPath);
                // ========================================================================

                AssetDatabase.CreateAsset(newProto, soPath);

                keyToProto[keyStr] = newProto;
                collected.Add(newProto);
            }
        }


        // Merge into NodeSet
        var merged = new List<NodePrototype>();
        if (targetNodeSet.prototypes != null) merged.AddRange(targetNodeSet.prototypes);
        foreach (var p in collected) if (!merged.Contains(p)) merged.Add(p);

        targetNodeSet.prototypes = merged.ToArray();
        EditorUtility.SetDirty(targetNodeSet);
        AssetDatabase.SaveAssets();

        targetNodeSet.Build();
        Debug.Log($"Baked/merged {collected.Count} prototypes. NodeSet now has {targetNodeSet.variants.Length} variants.");
    }
}
#endif
