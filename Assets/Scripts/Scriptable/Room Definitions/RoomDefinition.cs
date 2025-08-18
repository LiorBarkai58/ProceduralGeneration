using UnityEngine;

[CreateAssetMenu(fileName = "RoomDefinition", menuName = "Procedural/RoomDefinition")]
public class RoomDefinition : ScriptableObject
{
    public RoomType roomType;
    public RoomShape roomShape;
    public Vector2 roomSize;
    public DistanceRules distanceRules;
    public PlacementRules placementRules;
}
