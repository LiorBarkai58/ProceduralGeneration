#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

#endif
using UnityEngine;

public class WFCGrid : MonoBehaviour
{
    [SerializeField] private int DimX = 1;
    [SerializeField] private int DimY = 1;
    [SerializeField] private int DimZ = 1;

    private Vector3Int CachedDimensions = new Vector3Int(1,1,1);

    [SerializeField] private WFCNode[] _grid = new WFCNode[0];

    [SerializeField] private WFCNode _nodeObject;

    [SerializeField] private List<WFCNodeOption> _DefaultDomain;

    public List<WFCNodeOption> GetDeafultDomain() => _DefaultDomain;

    [ContextMenu("Update Grid Dimensions")]
    private void UpdateGrid()
    {
        
        WFCNode[] newArr = new WFCNode[DimX * DimY * DimZ];

        int i;
        for (i = 0; i < _grid.Length; i++)
        {
            Vector3Int coords = FromIndexCached(i);

            if (IsWhithinBounds(coords)) newArr[ToIndex(coords)] = _grid[i];
            else
            {
#if UNITY_EDITOR
                DestroyImmediate(_grid[i].gameObject);
#else
                Destroy(_grid[i].gameObject);
#endif
            }
        }

        for (i = 0; i < newArr.Length; i++)
        {
            if (newArr[i] == null) newArr[i] = CreateNode(i);
        }

        _grid = newArr;
        CachedDimensions = new Vector3Int(DimX, DimY, DimZ);
        
    }

    public int ToIndex(int x, int y, int z)
    {
        return x + y * DimX + z * DimX * DimY;
    }

    public int ToIndex(Vector3Int Coords) => ToIndex(Coords.x, Coords.y, Coords.z);

    public bool IsWhithinBounds(Vector3Int coords)
    {
        return coords.x >= 0 && coords.y >= 0 && coords.z >= 0 && 
            coords.x < DimX && coords.y < DimY && coords.z < DimZ;

    }

    public Vector3Int FromIndexCached(int index)
    {
        int xy = CachedDimensions.x * CachedDimensions.y;
        int z = index / xy;
        int rem = index - z * xy;
        int y = rem / CachedDimensions.x;
        int x = rem - y * CachedDimensions.x;
        return new Vector3Int(x, y, z);
    }

    public Vector3Int FromIndex(int index)
    {
        int xy = DimX * DimY;
        int z = index / xy;
        int rem = index - z * xy;
        int y = rem / DimX;
        int x = rem - y * DimX;
        return new Vector3Int(x, y, z);
    }

    private WFCNode CreateNode(int index) 
    {
        WFCNode node;

#if UNITY_EDITOR
        node = (WFCNode)PrefabUtility.InstantiatePrefab(_nodeObject, transform);
#else
                node = Instantiate(_nodeObject, transform);
#endif
        node.transform.localPosition = transform.localPosition + FromIndex(index);
        node.InitializeNode(this);
        return node;
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        foreach (WFCNode node in _grid)
        {
#if UNITY_EDITOR
            DestroyImmediate(node.gameObject);
#else
            Destroy(node.gameObject);
#endif

        }
        DimX = 0; DimY = 0; DimZ = 0;
        CachedDimensions = Vector3Int.zero;
        _grid = new WFCNode[0];
    }

}
