using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public List<ItemBase> DisplayList = new();
    private List <ItemBase> _itemInventory = new();

    public void AddItem(ItemBase item)
    {
        Debug.Log("Added item");
        _itemInventory.Add(item);
        DisplayList = _itemInventory;
    }
    public void ConsumeItem(ItemBase item)
    {
        if(_itemInventory.Contains(item)) _itemInventory.Remove(item);
        DisplayList = _itemInventory;

    }

    public bool DoesInventoryContain(ItemType type,int id,bool consumeItem = false)
    {
        ItemBase FoundItem = null;

        foreach (var item in _itemInventory)
        {
            if (item.GetItemInfo().Item1 == type && item.GetItemInfo().Item2 == id)
            {
                if (consumeItem)
                {
                    FoundItem = item;
                    break;
                }
                return true;
            }
        }

        if (FoundItem)
        {
            ConsumeItem(FoundItem);
            return true;
        } 

        return false;
    }
    public ItemBase GetItem(ItemType type, int id)
    {
        foreach (var item in _itemInventory)
        {
            if (item.GetItemInfo().Item1 == type && item.GetItemInfo().Item2 == id)
            {
                return item;
            }

        }

        return null;
    }
   
}
