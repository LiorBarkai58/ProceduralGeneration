using UnityEngine;

public class ItemBase : MonoBehaviour
{
    [SerializeField] protected ItemType type;
    [SerializeField] protected int ID = 0;
    public (ItemType,int) GetItemInfo()
    {
        return (type,ID);
    }
}
