﻿using System;
using System.Collections;
using System.Collections.Generic;
using BSP_Generation;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

public class DungeonCreator : MonoBehaviour
{
    public int dungeonWidth, dungeonLength;
    public int roomWidthMin, roomLengthMin;
    public int maxIterations;
    public int corridorWidth;
    public Material material;
    [Range(0.0f, 0.3f)]
    public float roomBottomCornerModifier;
    [Range(0.7f, 1.0f)]
    public float roomTopCornerMidifier;
    [Range(0, 2)]
    public int roomOffset;

    [Range(1, 30)] public float WallHeight = 2;

    [Range(1, 3)]
    [SerializeField] private int floorCount = 1;

    [SerializeField] private NavMeshSurface NMSurface;
    [Header("Special Room")]
    [SerializeField] private float specialRoomChance;
    [SerializeField] private int maxSpecialRooms = 3;

    [SerializeField] private GameObject specialRoomObjectType1;
    [SerializeField] private GameObject specialRoomObjectType2;
    [SerializeField] private GameObject specialRoomObjectType3;

    [SerializeField] private List<RoomDefinition> specialDefinitions;

    [Header("Positions")] [SerializeField] private Vector3 startPosition;
    [SerializeField] private Portal portalPrefab;
    [SerializeField] private ConfigurableWall doorPrefab;
    [SerializeField] private ItemBase key;
    public ConfigurableWall wallVertical, wallHorizontal;

    [SerializeField] private WFCGrid grid;
    [SerializeField] private WFCNodeOption DoorOption;
    [SerializeField] private GameObject errorSphere;
    List<Vector3Int> possibleDoorVerticalPosition;
    List<Vector3Int> possibleDoorHorizontalPosition;
    List<Vector3Int> possibleWallHorizontalPosition;
    List<Vector3Int> possibleWallVerticalPosition;
    // Start is called before the first frame update

    private LayerMask _groundLayer;
    void Start()
    {
       _groundLayer = LayerMask.NameToLayer("Floor");
        CreateDungeon();
        
    }

    [ContextMenu("Subscribe to failed")]
    public void Subscribe()
    {
        CorridorsGenerator.OnCorridorFailed += CreateDebugSphere;
    }
    [ContextMenu("UnSubscribe to failed")]
    public void UnSubscribe()
    {
        CorridorsGenerator.OnCorridorFailed -= CreateDebugSphere;
    }

    public void CreateDungeon()
    {
        DestroyAllChildren();
        for (int i = 0; i < floorCount; i++)
        {
            DugeonGenerator generator = new DugeonGenerator(dungeonWidth, dungeonLength);
            var listOfRooms = generator.CalculateDungeon(maxIterations,
                
                roomWidthMin,
                roomLengthMin,
                roomBottomCornerModifier,
                roomTopCornerMidifier,
                roomOffset,
                corridorWidth,
                specialDefinitions);
            GameObject wallParent = new GameObject("WallParent");
            wallParent.transform.parent = transform;
            wallParent.transform.position += Vector3.up * (WallHeight * i);;
            
            possibleDoorVerticalPosition = new List<Vector3Int>();
            possibleDoorHorizontalPosition = new List<Vector3Int>();
            possibleWallHorizontalPosition = new List<Vector3Int>();
            possibleWallVerticalPosition = new List<Vector3Int>();

            float maxDistance = 0;
            Node furthestRoom = listOfRooms[0];
            for (int j = 0; j < listOfRooms.Count; j++)
            {
                if (listOfRooms[j] is RoomNode && Vector2Int.Distance(new Vector2Int(0, 0), listOfRooms[j].BottomLeftAreaCorner) > maxDistance)
                {
                    maxDistance = Vector2Int.Distance(new Vector2Int(0, 0), listOfRooms[j].BottomLeftAreaCorner);
                    furthestRoom = listOfRooms[j];
                }

                if (listOfRooms[j] is CorridorNode corridor)
                {
                    CreateMesh(listOfRooms[j].BottomLeftAreaCorner, listOfRooms[j].TopRightAreaCorner, i, false, corridor.horizontal);
                }
                else CreateMesh(listOfRooms[j].BottomLeftAreaCorner, listOfRooms[j].TopRightAreaCorner, i, true);
                
            }

            int specialRoomCount = 0;

            foreach (Node room in listOfRooms)
            {
                Vector2 currentRoomMiddle =
                              (room.BottomLeftAreaCorner + room.TopRightAreaCorner) / 2;
                
                if (room is RoomNode roomNode)
                {
                    WFCGrid roomGrid = Instantiate(grid,
                        new Vector3(room.BottomLeftAreaCorner.x + 0.5f, WallHeight * i, room.BottomLeftAreaCorner.y + 0.5f),
                        Quaternion.identity, transform);
                    roomGrid.DimX = roomNode.Width;
                    roomGrid.DimZ = roomNode.Length;
                    roomGrid.DimY = (int)WallHeight;
                    roomGrid.UpdateGrid();
                    roomGrid.GetNodeAt(new Vector3Int(0, 1, 1)).SeedCollapse(DoorOption, 3);
                    for (int k = 0; k < 5; k++)
                    {
                        if (roomGrid.SolveV1_GreedyPropagate() == WFCGrid.SolveV1Result.Done)
                        {
                            break;
                        }
                    }       
                    
                    switch (roomNode.RoomType)
                    {
                        
                        case (RoomType.Type1):
                            // insert spawning logic similar to 
                            
                            if (roomNode.corridors.Count > 0)
                            {
                                print("Creating at corridor");
                                Vector2 exitCorridorMiddle = (roomNode.corridors[^1].BottomLeftAreaCorner + roomNode.corridors[^1].TopRightAreaCorner) / 2;
                                Instantiate(doorPrefab, new Vector3(exitCorridorMiddle.x, WallHeight * i, exitCorridorMiddle.y), Quaternion.identity, transform).ConfigureHeight(WallHeight);
                                Instantiate(key, new Vector3(currentRoomMiddle.x, WallHeight * i + 1, currentRoomMiddle.y), Quaternion.identity, transform);
                                
                            }
                            specialRoomCount++;
                            print("type1");
                            break;
                        case (RoomType.Type2):
                        
                            Instantiate(specialRoomObjectType2, new Vector3(currentRoomMiddle.x,WallHeight * i , currentRoomMiddle.y), Quaternion.identity, transform);
                            specialRoomCount++;
                            print("type2");
                            break;
                        case (RoomType.Type3):
                            Instantiate(specialRoomObjectType3, new Vector3(currentRoomMiddle.x, WallHeight * i, currentRoomMiddle.y), Quaternion.identity, transform);
                            specialRoomCount++;
                            print("type3");
                            break;
                        default:
                            print("normal");
                            break;
                        
                    }
                }
            }
            Vector2Int xzPosition =
                (furthestRoom.BottomLeftAreaCorner + furthestRoom.TopRightAreaCorner) / 2;
            Portal current = Instantiate(portalPrefab, new Vector3(xzPosition.x- 7,  WallHeight * (i), xzPosition.y- 7), Quaternion.identity, transform);
            current.TargetPosition = new Vector3(startPosition.x, WallHeight * (i + 1), startPosition.z);
            CreateMesh(Vector2.zero, new Vector2(dungeonWidth, dungeonLength), i, true);
            CreateWalls(wallParent);    
        }
        NMSurface.BuildNavMesh();
    }

    private void CreateWalls(GameObject wallParent)
    {
        foreach (var wallPosition in possibleWallHorizontalPosition)
        {
            CreateWall(wallParent, wallPosition, wallHorizontal);
        }
        foreach (var wallPosition in possibleWallVerticalPosition)
        {
            CreateWall(wallParent, wallPosition, wallVertical);
        }
    }

    private void CreateWall(GameObject wallParent, Vector3Int wallPosition, ConfigurableWall wallPrefab)
    {
        ConfigurableWall current = Instantiate(wallPrefab, wallPosition, Quaternion.identity, wallParent.transform);
        current.AddComponent<BoxCollider>();
        current.ConfigureHeight(WallHeight);
        current.transform.localPosition = new Vector3(current.transform.localPosition.x, 0, current.transform.localPosition.z);
    }

    private void CreateMesh(Vector2 bottomLeftCorner, Vector2 topRightCorner, int floorNumber, bool onlyFloor = false, bool horizontal = false)
    {
        Vector3 bottomLeftV = new Vector3(bottomLeftCorner.x, 0, bottomLeftCorner.y);
        Vector3 bottomRightV = new Vector3(topRightCorner.x, 0, bottomLeftCorner.y);
        Vector3 topLeftV = new Vector3(bottomLeftCorner.x, 0, topRightCorner.y);
        Vector3 topRightV = new Vector3(topRightCorner.x, 0, topRightCorner.y);

        Vector3[] vertices = new Vector3[]
        {
            topLeftV,
            topRightV,
            bottomLeftV,
            bottomRightV
        };

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        int[] triangles = new int[]
        {
            0,
            1,
            2,
            2,
            1,
            3,
            2, 1, 0,
            3, 1, 2

        };
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        GameObject dungeonFloor = new GameObject("Mesh" + bottomLeftCorner, typeof(MeshFilter), typeof(MeshRenderer));
        float floorHeight = floorNumber * WallHeight + 0.001f*floorNumber;
        dungeonFloor.transform.position = Vector3.zero + Vector3.up * floorHeight;
        dungeonFloor.transform.localScale = Vector3.one;
        dungeonFloor.GetComponent<MeshFilter>().mesh = mesh;
        dungeonFloor.GetComponent<MeshRenderer>().material = material;
        dungeonFloor.AddComponent<BoxCollider>();
        dungeonFloor.layer = _groundLayer;
        dungeonFloor.transform.parent = transform;
        if (onlyFloor) return;
        for (int row = (int)bottomLeftV.x; row < (int)bottomRightV.x; row++)
        {
            if (horizontal || (row == (int)bottomLeftV.x || row == (int)bottomRightV.x) )
            {

                var wallPosition = new Vector3(row, floorHeight, bottomLeftV.z);
                AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
            }
        }
        for (int row = (int)topLeftV.x; row < (int)topRightCorner.x; row++)
        {
            if (horizontal || (row == (int)topLeftV.x || row == (int)topRightCorner.x))
            {
                var wallPosition = new Vector3(row, floorHeight, topRightV.z);
                AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
            }
        }
        for (int col = (int)bottomLeftV.z; col < (int)topLeftV.z; col++)
        {
            if (!horizontal || (col == (int)bottomLeftV.z || col == (int)topLeftV.z))
            {
                var wallPosition = new Vector3(bottomLeftV.x, floorHeight, col);
                AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
            }
        }
        for (int col = (int)bottomRightV.z; col <= (int)topRightV.z; col++)
        {
            if (!horizontal || (col == (int)bottomRightV.z || col == (int)topRightV.z))
            {
                var wallPosition = new Vector3(bottomRightV.x, floorHeight, col);
                AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
            }
        }
    }
    
    

    private void AddWallPositionToList(Vector3 wallPosition, List<Vector3Int> wallList, List<Vector3Int> doorList)
    {
        Vector3Int point = Vector3Int.CeilToInt(wallPosition);
        if (wallList.Contains(point)){
            doorList.Add(point);
            wallList.Remove(point);
        }
        else
        {
            wallList.Add(point);
        }
    }

    private void DestroyAllChildren()
    {
        while(transform.childCount != 0)
        {
            foreach(Transform item in transform)
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }

    private void CreateDebugSphere(Vector2Int position)
    {
        Instantiate(errorSphere, new Vector3(position.x, 0, position.y), Quaternion.identity, transform);
    }
}