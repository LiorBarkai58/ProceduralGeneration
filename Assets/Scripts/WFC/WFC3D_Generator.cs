using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WFC3D_Generator : MonoBehaviour
{
    [Header("Content")]
    public NodeSet nodeSet;

    [Header("Grid Size")]
    public int sizeX = 8;
    public int sizeY = 4;
    public int sizeZ = 8;

    [Header("Solver Settings")]
    public WFC3DSettings settings = new WFC3DSettings
    {
        boundary = BoundaryPolicy.NotAir,
        maxSteps = 1_000_000,
        maxBacktracks = 10_000,
        seed = 12345,
        verbose = false
    };

    [Header("Instantiation")]
    public Vector3 cellSize = Vector3.one; // world spacing per cell
    public bool clearChildrenOnGenerate = true;

    [Header("Visualization")]
    public bool playBackPropagation = true;
    [Range(0f, 0.5f)] public float eventDelaySeconds = 0.02f; // speed of playback
    public bool showForbidDebug = false; // if true, drops tiny debug markers for forbids

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!nodeSet)
        {
            Debug.LogError("WFC3D_Generator: Assign a NodeSet asset.");
            return;
        }
        if (nodeSet.variants == null || nodeSet.variants.Length == 0) nodeSet.Build();
        if (nodeSet.variants == null || nodeSet.variants.Length == 0)
        {
            Debug.LogError("WFC3D_Generator: NodeSet has no variants after Build().");
            return;
        }

        if (clearChildrenOnGenerate)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        var solver = new WFC3DSolver(nodeSet, sizeX, sizeY, sizeZ, settings);

        WFC3DResult result;
        try
        {
            result = solver.Solve();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"WFC3D Solve exception: {ex.Message}");
            return;
        }

        if (!result.success)
        {
            Debug.LogError("WFC3D_Generator: Generation failed (contradiction or limits reached).");
            return;
        }

        if (playBackPropagation)
        {
            StartCoroutine(PlaybackEvents(solver.Events, result));
        }
        else
        {
            InstantiateFinal(result);
        }
    }

    void InstantiateFinal(WFC3DResult result)
    {
        int idx = 0;
        for (int z = 0; z < sizeZ; z++)
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++, idx++)
                {
                    int vIdx = result.variantIndex[idx];
                    if (vIdx < 0) continue;

                    var v = nodeSet.variants[vIdx];
                    if (!v.proto || v.proto.isAir) continue;
                    if (!v.proto.prefab) continue;

                    var go = (GameObject)Instantiate(v.proto.prefab, transform);
                    go.name = $"{v.proto.nodeId}@R{v.rotY} ({x},{y},{z})";
                    go.transform.localPosition = new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                    go.transform.localRotation = Quaternion.Euler(0, v.rotY * 90f, 0);
                }

        Debug.Log($"WFC3D_Generator: Generated {sizeX}x{sizeY}x{sizeZ} = {sizeX * sizeY * sizeZ} cells (instant).");
    }

    IEnumerator PlaybackEvents(IReadOnlyList<WFCEvent> events, WFC3DResult finalRes)
    {
        // We'll instantiate on the first Collapse event per cell that matches the final variant.
        var seenCell = new HashSet<int>();
        int instanced = 0;

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];

            if (ev.type == WFCEventType.Collapse)
            {
                if (seenCell.Contains(ev.cell)) continue;
                if (ev.vIdx != finalRes.variantIndex[ev.cell]) continue; // collapse that survived

                var (x, y, z) = Coords(ev.cell, finalRes.sx, finalRes.sy);
                var v = nodeSet.variants[ev.vIdx];
                if (v.proto && !v.proto.isAir && v.proto.prefab)
                {
                    var go = (GameObject)Instantiate(v.proto.prefab, transform);
                    go.name = $"{v.proto.nodeId}@R{v.rotY} ({x},{y},{z})";
                    go.transform.localPosition = new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                    go.transform.localRotation = Quaternion.Euler(0, v.rotY * 90f, 0);
                }

                seenCell.Add(ev.cell);
                instanced++;
                if (eventDelaySeconds > 0f) yield return new WaitForSeconds(eventDelaySeconds);
                else yield return null; // at least yield a frame
            }
            else if (ev.type == WFCEventType.Forbid && showForbidDebug)
            {
                // Optional: tiny gizmo cube (editor-only feel). Safe to skip instantiation for performance.
                // You can draw debug here if desired.
                if (eventDelaySeconds > 0f) yield return new WaitForSeconds(eventDelaySeconds * 0.25f);
            }
        }

        Debug.Log($"WFC3D_Generator: Playback done. Instanced {instanced} objects via timeline.");
    }

    (int x, int y, int z) Coords(int idx, int sx, int sy)
    {
        int x = idx % sx;
        int t = idx / sx;
        int y = t % sy;
        int z = t / sy;
        return (x, y, z);
    }
}
