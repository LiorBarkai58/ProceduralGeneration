#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WFC3D_EditorUtil
{
    /// <summary>Returns the GUID of the prefab asset backing this instance, or empty if none.</summary>
    public static string GetPrefabGuid(GameObject instance)
    {
        if (!instance) return string.Empty;
        var src = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (!src) return string.Empty;
        var path = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return AssetDatabase.AssetPathToGUID(path) ?? string.Empty;
    }

    /// <summary>Returns the NodeAuthoring on the prefab asset if any, otherwise null.</summary>
    public static NodeAuthoring GetPrefabAuthoring(GameObject instance)
    {
        if (!instance) return null;
        var src = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        return src ? src.GetComponent<NodeAuthoring>() : null;
    }

    public static string Sanitize(string s)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
#endif
