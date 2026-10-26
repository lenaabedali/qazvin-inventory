using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One square in the toolbar or main inventory (tutorial 6:38 - 7:01 and 23:08 - 26:04).
/// Improvements over the video: dropping onto a full slot swaps the two items,
/// and dropping onto the same stackable item merges the stacks.
/// </summary>
public class InventorySlot : MonoBehaviour, IDropHandler
{
    public Image image;
    [Tooltip("Optional: sprites to swap between (the stone style uses a turquoise-inlay sprite when selected).")]
    public Sprite normalSprite;
    public Sprite selectedSprite;
    [Tooltip("Tint colors (used like the tutorial when no sprites are set).")]
    public Color selectedColor = Color.white;
    public Color notSelectedColor = Color.white;

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (normalSprite == null) normalSprite = image.sprite;
        Deselect();
    }

    public void Select()
    {
        if (selectedSprite != null) image.sprite = selectedSprite;
        image.color = selectedColor;
    }

    public void Deselect()
    {
        if (normalSprite != null) image.sprite = normalSprite;
        image.color = notSelectedColor;
    }

    /// <summary>
    /// The item in this slot, or null. Works even while the main inventory is hidden
    /// (GetComponentInChildren skips inactive objects, which caused duplicate items).
    /// </summary>
    public InventoryItem CurrentItem
    {
        get
        {
            foreach (Transform child in transform)
            {
                var it = child.GetComponent<InventoryItem>();
                if (it != null && !it.IsRemoved) return it;
            }
            return null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;
        var dropped = eventData.pointerDrag.GetComponent<InventoryItem>();
        if (dropped == null) return;

        var current = CurrentItem;

        // Empty slot: just move there.
        if (current == null || current == dropped)
        {
            dropped.parentAfterDrag = transform;
            return;
        }

        // Same stackable item: merge as much as fits.
        int max = InventoryManager.instance != null ? InventoryManager.instance.maxStackedItems : 64;
        if (current.item == dropped.item && current.item.stackable && current.count < max)
        {
            int moved = Mathf.Min(max - current.count, dropped.count);
            current.count += moved;
            dropped.count -= moved;
            current.RefreshCount();
            dropped.RefreshCount();
            if (dropped.count <= 0) dropped.MarkRemoved();
            return;
        }

        // Different items: swap them.
        current.transform.SetParent(dropped.parentAfterDrag, false);
        current.SnapToParent();
        dropped.parentAfterDrag = transform;
    }
}
