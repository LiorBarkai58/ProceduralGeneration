using UnityEngine;

[DisallowMultipleComponent]
public class NodeAuthoring : MonoBehaviour
{
    [Header("Identity / Mapping")]
    [Tooltip("If set, this instance will bake to this prototype directly (ignores GUID/ID).")]
    public NodePrototype prototypeAssetOverride;

    [Tooltip("Optional custom ID if not using a prefab. Otherwise the prefab GUID is used.")]
    public string nodeIdOverride;

    [Header("Footprint / Metadata")]
    [Tooltip("Footprint in grid cells (for multi-cell pieces). 1x1x1 for single cell.")]
    public Vector3Int footprint = new Vector3Int(1, 1, 1);

    [Tooltip("Pathing metadata (optional).")]
    public bool walkable = true;

    [Tooltip("If true, the node can rotate around Y. Variants are baked at 0,90,180,270.")]
    public bool allowYRotation = true;
}