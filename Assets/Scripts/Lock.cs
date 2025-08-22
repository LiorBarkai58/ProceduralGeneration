using UnityEngine;

public class Lock : InteractableBase
{
    protected override void InteractionImplementation(GameObject instigator)
    {
        if (instigator.TryGetComponent(out Inventory inv))
        {
            bool shouldDestroyKey = true;

            if (inv.DoesInventoryContain(ItemType,ItemID, shouldDestroyKey))
            {
                Destroy(gameObject);
            }
                
        }
    }
}
