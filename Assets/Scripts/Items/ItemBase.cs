using UnityEngine;

public class ItemBase : MonoBehaviour,IInteractable
{
    [SerializeField] protected ItemType type;
    [SerializeField] protected int ID = 0;
    public (ItemType,int) GetItemInfo()
    {
        return (type,ID);
    }


    public void OnInteract(GameObject instigator)
    {
        if (instigator.TryGetComponent(out Inventory inv))
        {
            inv.AddItem(this);
        }
    }
}
