using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WFCNodeOption", menuName = "Scriptable Objects/WFCNodeOption")]
public class WFCNodeOption : ScriptableObject
{

    [SerializeField] private string Name;
    [SerializeField] private float WFCWeight = 1;
    [SerializeField] private GameObject AttachedPrefab;

    [SerializeField] private List<WFCNodeOption> _LegalNeighborsUP;
    [SerializeField] private List<WFCNodeOption> _LegalNeighborsDOWN;
    [SerializeField] private List<WFCNodeOption> _LegalNeighborsPositiveX;
    [SerializeField] private List<WFCNodeOption> _LegalNeighborsNegativeX;
    [SerializeField] private List<WFCNodeOption> _LegalNeighborsNegativeZ;
    [SerializeField] private List<WFCNodeOption> _LegalNeighborsPositiveZ;

    // Add Validation for Lists Containing Unique options
    //private void OnValidate() { }

    public void InitializeAdjacensies(List<WFCNodeOption> LegalNeighborsUP, List<WFCNodeOption> LegalNeighborsDOWN, List<WFCNodeOption> LegalNeighborsPositiveX,
                                      List<WFCNodeOption> LegalNeighborsNegativeX, List<WFCNodeOption> LegalNeighborsNegativeZ, List<WFCNodeOption> LegalNeighborsPositiveZ)
    {
        _LegalNeighborsUP = LegalNeighborsUP;
        _LegalNeighborsDOWN = LegalNeighborsDOWN;
        _LegalNeighborsNegativeX = LegalNeighborsNegativeX;
        _LegalNeighborsNegativeZ = LegalNeighborsNegativeZ;
        _LegalNeighborsPositiveX = LegalNeighborsPositiveX;
        _LegalNeighborsPositiveZ = LegalNeighborsPositiveZ;
    }

    public void AddLegalNeighbor(WFCNodeOption LegalNeighbor, NeighborDirection Direction, int Rotations = 0) 
    {
        int adjustedIndex = ((int)Direction + Rotations) % 4;
        List<WFCNodeOption> CurrentList;

        if (Direction == NeighborDirection.UP) CurrentList = _LegalNeighborsUP;
        else if (Direction == NeighborDirection.DOWN) CurrentList = _LegalNeighborsDOWN;
        else if (adjustedIndex == 0) CurrentList = _LegalNeighborsPositiveZ;
        else if (adjustedIndex == 1) CurrentList = _LegalNeighborsPositiveX;
        else if (adjustedIndex == 2) CurrentList = _LegalNeighborsNegativeZ;
        else CurrentList = _LegalNeighborsNegativeX;

        if (!CurrentList.Contains(LegalNeighbor)) CurrentList.Add(LegalNeighbor);
    }

    public float GetWeight() => WFCWeight;
    public string GetName() => Name;

    public List<WFCNodeOption> GetLegatNeighbors(NeighborDirection Direction, int Rotations)
    {
        // normalize to 0..3
        int r = ((Rotations % 4) + 4) % 4;

        // vertical faces do not rotate
        if (Direction == NeighborDirection.UP) return _LegalNeighborsUP;
        if (Direction == NeighborDirection.DOWN) return _LegalNeighborsDOWN;

        // map world face -> local face by applying the inverse rotation (SUBTRACT r)
        // your enum is: POSITIVEZ=0, POSITIVEX=1, NEGATIVEZ=2, NEGATIVEX=3
        int dirIndex = (int)Direction;              // 0..3
        int localIdx = ((dirIndex - r) % 4 + 4) % 4; // ← key change: subtract, not add

        // now pick the list in LOCAL frame
        switch (localIdx)
        {
            case 0: return _LegalNeighborsPositiveZ;
            case 1: return _LegalNeighborsPositiveX;
            case 2: return _LegalNeighborsNegativeZ;
            default: return _LegalNeighborsNegativeX;
        }
    }


    internal GameObject GetPrefab()
    {
        return AttachedPrefab;
    }
}

public enum NeighborDirection { UP = -1, DOWN = -2, POSITIVEZ = 0, POSITIVEX = 1, NEGATIVEZ = 2, NEGATIVEX = 3 }



