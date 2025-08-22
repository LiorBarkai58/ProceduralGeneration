using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Tooltip("If you have item requirements specify them here") , Header("Item Requirements")]
    public ItemType ItemType;
    public int ItemID;
    protected abstract void InteractionImplementation(GameObject instigator);

    public void OnInteract(GameObject instigator)
    {
        InteractionImplementation(instigator);
    }

}
