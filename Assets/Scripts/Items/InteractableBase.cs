using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Item Requirments")]
    public bool RequiresItem;
    public ItemType ItemType;
    public int ItemID;
    protected abstract void InteractionImplementation(GameObject instigator);

    public void OnInteract(GameObject instigator)
    {
        InteractionImplementation(instigator);
    }

}
