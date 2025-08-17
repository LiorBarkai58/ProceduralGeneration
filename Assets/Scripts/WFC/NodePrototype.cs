using UnityEngine;
using System;

[Flags]
public enum FaceMask : byte
{
    None = 0,
    PX = 1 << 0, NX = 1 << 1,
    PY = 1 << 2, NY = 1 << 3,
    PZ = 1 << 4, NZ = 1 << 5,
    All = PX | NX | PY | NY | PZ | NZ
}

[CreateAssetMenu(menuName = "WFC3D/NodePrototype")]
public class NodePrototype : ScriptableObject
{
    public string nodeId;
    public GameObject prefab;

    [Header("Weights")]
    [Range(0.01f, 10f)] public float baseWeight = 1f;
    [HideInInspector] public float learnedWeight = 1f;
    public bool useLearnedWeight = true;

    [Header("Metadata")]
    public bool walkable = true;
    public bool allowYRotation = true;

    [Header("Special")]
    public bool isAir = false; // empty space prototype (no instantiation)

    [Header("Boundary Rules")]
    // Hard rules (pruning):
    public FaceMask mustTouchBoundaryOn = FaceMask.None; // e.g. walls: outward face
    public FaceMask forbidBoundaryOn = FaceMask.None; // e.g. chests: avoid outer shell
    // Soft bias (weights):
    public FaceMask preferBoundaryOn = FaceMask.None;
    public FaceMask avoidBoundaryOn = FaceMask.None;
    [Range(0.1f, 4f)] public float preferMultiplier = 2f;
    [Range(0.1f, 4f)] public float avoidMultiplier = 0.5f;

    // Optional per-rotation bias
    [HideInInspector] public float[] learnedRotationBias = new float[4] { 1, 1, 1, 1 };

    public float EffectiveWeight => useLearnedWeight ? learnedWeight : baseWeight;

    // ---- Helpers ----
    public static Face RotateFaceY(Face f, int rot)
    {
        rot = ((rot % 4) + 4) % 4;
        if (f == Face.PY || f == Face.NY) return f;
        for (int i = 0; i < rot; i++)
        {
            switch (f)
            {
                case Face.PX: f = Face.PZ; break;
                case Face.PZ: f = Face.NX; break;
                case Face.NX: f = Face.NZ; break;
                case Face.NZ: f = Face.PX; break;
            }
        }
        return f;
    }

    public static FaceMask ToMask(Face f) => f switch
    {
        Face.PX => FaceMask.PX,
        Face.NX => FaceMask.NX,
        Face.PY => FaceMask.PY,
        Face.NY => FaceMask.NY,
        Face.PZ => FaceMask.PZ,
        Face.NZ => FaceMask.NZ,
        _ => FaceMask.None
    };

    public static bool FaceMaskHas(FaceMask mask, Face f) => (mask & ToMask(f)) != 0;

    public static FaceMask RotateMaskY(FaceMask mask, int rot)
    {
        FaceMask outMask = FaceMask.None;
        foreach (Face f in new[] { Face.PX, Face.NX, Face.PY, Face.NY, Face.PZ, Face.NZ })
        {
            if (!FaceMaskHas(mask, f)) continue;
            var rf = RotateFaceY(f, rot);
            outMask |= ToMask(rf);
        }
        return outMask;
    }

    public static bool AnyFaceInMaskTouchesBoundary(FaceMask needed, FaceMask cellBoundaryMask)
    {
        return (needed & cellBoundaryMask) != 0;
    }
}
