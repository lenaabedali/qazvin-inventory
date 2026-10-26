using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shows the selected toolbar item in the character's hand.
/// Third person: put this on a "Hand" object (or your character's hand bone).
/// First person: put it on the camera and raise the offsets.
/// </summary>
public class ItemHolder : MonoBehaviour
{
    public Vector3 localOffset = new Vector3(0f, 0f, 0.15f);
    public Vector3 localEuler = Vector3.zero;
    public float blockSize = 0.3f;
    public float spriteSize = 0.5f;
    public bool castShadows = true;

    Item shown;
    bool initialised;
    GameObject visual;

    void LateUpdate()
    {
        var inventory = InventoryManager.instance;
        if (inventory == null) return;

        Item current = inventory.GetSelectedItem(false);
        if (initialised && current == shown) return;
        initialised = true;
        shown = current;

        if (visual != null) Destroy(visual);
        if (current == null) return;

        visual = ItemVisuals.CreateModel(current, current.worldPrefab != null ? blockSize : spriteSize);
        visual.name = "Held " + current.name;
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = localOffset;
        visual.transform.localRotation = Quaternion.Euler(localEuler);

        if (!castShadows)
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
    }
}
