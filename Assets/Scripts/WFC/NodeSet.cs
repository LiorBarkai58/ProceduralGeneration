using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct NodeVariant
{
    public NodePrototype proto;
    public int rotY;              // 0..3
    public float weight;          // proto weight * rotation bias
    public string VariantId => $"{proto.nodeId}@R{rotY}";
}

[System.Serializable]
public class FaceNeighbors
{
    public List<NodePrototype> allowed = new List<NodePrototype>();
}

[System.Serializable]
public class PrototypeAdjacency
{
    public NodePrototype proto;
    // PX,NX,PY,NY,PZ,NZ
    public FaceNeighbors[] faces = new FaceNeighbors[6]
    {
        new FaceNeighbors(), new FaceNeighbors(), new FaceNeighbors(),
        new FaceNeighbors(), new FaceNeighbors(), new FaceNeighbors()
    };
}

[CreateAssetMenu(menuName = "WFC3D/NodeSet")]
public class NodeSet : ScriptableObject
{
    public NodePrototype[] prototypes;

    [Header("Adjacency-by-Example")]
    [Tooltip("Populated by the AdjacencyFromExamples wizard.")]
    public PrototypeAdjacency[] learnedAdjacency;

    [Header("Air Prototype")]
    [Tooltip("Prototype that represents empty space. Variants of this will not be instantiated.")]
    public NodePrototype airPrototype;

    [System.NonSerialized] public NodeVariant[] variants;
    [System.NonSerialized] public bool[,,] compatible; // [variantA, face, variantB]
    [System.NonSerialized] public int airVariantStart = -1;
    [System.NonSerialized] public int airVariantCount = 0;

    void OnEnable()
    {
        if (variants == null || variants.Length == 0) Build();
    }
#if UNITY_EDITOR
    void OnValidate() { Build(); }
#endif

    [ContextMenu("Build Variants & Compatibility")]
    public void Build()
    {
        if (prototypes == null || prototypes.Length == 0)
        {
            variants = new NodeVariant[0];
            compatible = new bool[0, 0, 0];
            return;
        }

        // Build variants
        var list = new List<NodeVariant>(prototypes.Length * 4);
        foreach (var p in prototypes)
        {
            if (!p) continue;
            int maxR = p.allowYRotation ? 4 : 1;
            for (int r = 0; r < maxR; r++)
            {
                float rotBias = (r >= 0 && r < p.learnedRotationBias.Length) ? p.learnedRotationBias[r] : 1f;
                list.Add(new NodeVariant
                {
                    proto = p,
                    rotY = r,
                    weight = Mathf.Max(1e-6f, p.EffectiveWeight * rotBias)
                });
            }
        }
        variants = list.ToArray();

        // Air coverage
        airVariantStart = -1; airVariantCount = 0;
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i].proto == airPrototype)
            {
                if (airVariantStart < 0) airVariantStart = i;
                airVariantCount++;
            }
        }

        // Build compatibility from learned adjacency
        int V = variants.Length;
        compatible = new bool[V, 6, V];

        var protoToAdj = new Dictionary<NodePrototype, PrototypeAdjacency>();
        if (learnedAdjacency != null)
        {
            foreach (var pa in learnedAdjacency)
                if (pa != null && pa.proto != null) protoToAdj[pa.proto] = pa;
        }

        for (int a = 0; a < V; a++)
            for (int b = 0; b < V; b++)
                for (int f = 0; f < 6; f++)
                {
                    var face = (Face)f;
                    var protoA = variants[a].proto;
                    var protoB = variants[b].proto;

                    bool ok = false;

                    // Learned adjacency: rotate face back into A's prototype space
                    if (protoToAdj.TryGetValue(protoA, out var adj))
                    {
                        Face faceOnProtoA = NodePrototype.RotateFaceY(face, -variants[a].rotY);
                        var lst = adj.faces[(int)faceOnProtoA].allowed;
                        for (int i = 0; i < lst.Count; i++)
                        {
                            if (lst[i] == protoB) { ok = true; break; }
                        }
                    }

                    // Ensure Air can sit next to Air (lets empty regions exist)
                    if (!ok && airPrototype != null && protoA == airPrototype && protoB == airPrototype)
                        ok = true;

                    // Fallback: if A has no learned neighbors on this face at all,
                    // allow Air (or self) so we don't over-prune and dead-end.
                    if (!ok)
                    {
                        bool aHasAdj = false;
                        if (protoToAdj.TryGetValue(protoA, out var adj2))
                        {
                            Face faceOnProtoA = NodePrototype.RotateFaceY(face, -variants[a].rotY);
                            var lst2 = adj2.faces[(int)faceOnProtoA].allowed;
                            aHasAdj = (lst2 != null && lst2.Count > 0);
                        }

                        if (!aHasAdj)
                        {
                            if (airPrototype != null && protoB == airPrototype) ok = true;
                            else if (protoB == protoA) ok = true;
                        }
                    }

                    compatible[a, f, b] = ok;
                }
    }

    public static Face Opposite(Face f) => f switch
    {
        Face.PX => Face.NX,
        Face.NX => Face.PX,
        Face.PY => Face.NY,
        Face.NY => Face.PY,
        Face.PZ => Face.NZ,
        Face.NZ => Face.PZ,
        _ => f
    };
}
