using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    protected abstract void InteractionImplementation();

    public void OnInteract()
    {
        InteractionImplementation();
    }

}
