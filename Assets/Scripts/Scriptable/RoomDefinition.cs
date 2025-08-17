using UnityEngine;

[CreateAssetMenu(fileName = "RoomDefinition", menuName = "Procedural/RoomDefinition")]
public class RoomDefinition : ScriptableObject
{
    public RoomType roomType;
    public RoomShape roomShape;
    public int roomSize;
    public int roomAmount;
    public DistanceRules distanceRules;
    public PlacementRules placementRules;
}
