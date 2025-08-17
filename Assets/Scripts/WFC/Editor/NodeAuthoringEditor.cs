#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NodeAuthoring))]
public class NodeAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Adjacency is learned from example rooms. Sockets are no longer used.\n" +
            "Make sure objects are grid-snapped and use prefab instances for duplicates.",
            MessageType.Info
        );
    }

    void OnSceneGUI()
    {
        var t = (NodeAuthoring)target;
        if (t == null) return;

        var tr = t.transform;
        var fp = t.footprint;
        var center = tr.position + new Vector3(
            (fp.x - 1) * 0.5f,
            (fp.y - 1) * 0.5f,
            (fp.z - 1) * 0.5f
        );

        var size = new Vector3(fp.x, fp.y, fp.z);

        Handles.color = new Color(1f, 1f, 1f, 0.8f);
        Handles.DrawWireCube(center, size);

        // Draw a little axis cross for orientation
        float s = HandleUtility.GetHandleSize(center) * 0.25f;
        Handles.color = Color.red; Handles.DrawLine(center, center + tr.right * s);
        Handles.color = Color.green; Handles.DrawLine(center, center + tr.up * s);
        Handles.color = Color.blue; Handles.DrawLine(center, center + tr.forward * s);

        // Label
        Handles.color = Color.white;
        string id = !string.IsNullOrEmpty(t.nodeIdOverride) ? t.nodeIdOverride : tr.gameObject.name;
        Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.1f),
            $"{id}\nFootprint {fp.x}×{fp.y}×{fp.z}\nY-Rot: {(t.allowYRotation ? "Yes" : "No")}");
    }
}
#endif
