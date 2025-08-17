using UnityEngine;

[CreateAssetMenu(menuName = "WFC3D/SocketCompatibility")]
public class SocketCompatibility : ScriptableObject
{
    [System.Serializable]
    public struct Pair { public Socket a; public Socket b; }

    [Tooltip("Pairs considered compatible in addition to direct bit overlap.")]
    public Pair[] additionalPairs;

    public bool AreCompatible(Socket a, Socket b)
    {
        if ((a & b) != 0) return true; // share a channel bit
        if (additionalPairs == null) return false;
        foreach (var p in additionalPairs)
        {
            bool ab = (a & p.a) != 0 && (b & p.b) != 0;
            bool ba = (a & p.b) != 0 && (b & p.a) != 0;
            if (ab || ba) return true;
        }
        return false;
    }
}
