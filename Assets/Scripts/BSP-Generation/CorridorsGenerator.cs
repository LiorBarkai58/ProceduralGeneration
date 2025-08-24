using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class CorridorsGenerator
{
    public static event UnityAction<Vector2Int> OnCorridorFailed;
    public List<Node> CreateCorridor(List<RoomNode> allNodesCollection, int corridorWidth)
    {
        List<Node> corridorList = new List<Node>();
        Queue<RoomNode> structuresToCheck = new Queue<RoomNode>(
            allNodesCollection.OrderByDescending(node => node.TreeLayerIndex).ToList());
        while (structuresToCheck.Count > 0)
        {
            var node = structuresToCheck.Dequeue();
            if (node.ChildrenNodeList.Count == 0)
            {
                continue;
            }
            CorridorNode corridor = new CorridorNode(node.ChildrenNodeList[0], node.ChildrenNodeList[1], corridorWidth);
            
            
            corridorList.Add(corridor);
        }
        return corridorList;
    }
    
    private bool validateCorridor(Node node1, Node node2, int corridorWidth)
    {
        if (Mathf.Abs(node1.BottomLeftAreaCorner.x - node2.TopRightAreaCorner.x) < corridorWidth / 2 ||
            Mathf.Abs(node1.BottomLeftAreaCorner.y - node2.TopRightAreaCorner.y) < corridorWidth / 2 ||
            Mathf.Abs(node1.BottomRightAreaCorner.y - node2.TopLeftAreaCorner.y) < corridorWidth / 2 ||
            Mathf.Abs(node1.BottomRightAreaCorner.x - node2.TopLeftAreaCorner.x) < corridorWidth / 2 ||

            Mathf.Abs(node1.TopLeftAreaCorner.x - node2.BottomRightAreaCorner.x) < corridorWidth / 2 ||
            Mathf.Abs(node1.TopLeftAreaCorner.y - node2.BottomRightAreaCorner.y) < corridorWidth / 2 ||
            Mathf.Abs(node1.TopRightAreaCorner.y - node2.BottomLeftAreaCorner.y) < corridorWidth / 2 ||
            Mathf.Abs(node1.TopRightAreaCorner.x - node2.BottomLeftAreaCorner.x) < corridorWidth / 2)
        {
            OnCorridorFailed?.Invoke(node1.BottomLeftAreaCorner);
            OnCorridorFailed?.Invoke(node2.TopRightAreaCorner);
            
            return false;
        }
        OnCorridorFailed?.Invoke(node1.BottomLeftAreaCorner);
        OnCorridorFailed?.Invoke(node2.BottomLeftAreaCorner);
        
        return true;
    }    
}