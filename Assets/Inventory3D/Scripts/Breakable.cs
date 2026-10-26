using UnityEngine;

/// <summary>
/// Put this on any block/object the player can break. It drops a pickup when broken.
/// </summary>
public class Breakable : MonoBehaviour
{
    [Tooltip("Item dropped when broken. Placed blocks fill this in automatically.")]
    public Item dropItem;
    public int dropCount = 1;
    [Tooltip("None = can be broken by hand or anything. Otherwise needs a tool with this action type.")]
    public ActionType requiredAction = ActionType.None;

    public bool CanBreakWith(Item held)
    {
        if (requiredAction == ActionType.None) return true;
        return held != null && held.type == ItemType.Tool && held.actionType == requiredAction;
    }

    public void Break()
    {
        // Switch off our colliders first so the drop can fall to whatever is below us
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (dropItem != null)
            ItemPickup.Spawn(dropItem, transform.position, dropCount, 0.3f, true);
        Destroy(gameObject);
    }
}
