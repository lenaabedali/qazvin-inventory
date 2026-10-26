using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The draggable icon that sits inside an InventorySlot (tutorial 5:21 - 6:38, 9:29 - 10:56, 19:00 - 21:17).
/// </summary>
public class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const float Padding = 6f;

    [Header("UI")]
    public Image image;
    public Text countText;

    [HideInInspector] public Item item;
    [HideInInspector] public int count = 1;
    [HideInInspector] public Transform parentAfterDrag;

    /// <summary>True once this item has been used up and is about to be destroyed.</summary>
    public bool IsRemoved { get; private set; }

    public void InitialiseItem(Item newItem)
    {
        item = newItem;
        image.sprite = newItem.image;
        RefreshCount();
        SnapToParent();
    }

    public void RefreshCount()
    {
        countText.text = count.ToString();
        countText.gameObject.SetActive(count > 1);
    }

    /// <summary>Removes this item from its slot right away and destroys it.</summary>
    public void MarkRemoved()
    {
        IsRemoved = true;
        Destroy(gameObject);
    }

    /// <summary>Stretches the icon to fill its parent slot with a little padding.</summary>
    public void SnapToParent()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(Padding, Padding);
        rt.offsetMax = new Vector2(-Padding, -Padding);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // ---------- Drag and drop ----------

    public void OnBeginDrag(PointerEventData eventData)
    {
        image.raycastTarget = false;           // so the slot underneath can receive OnDrop
        parentAfterDrag = transform.parent;

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform top = canvas != null ? canvas.rootCanvas.transform : transform.root;
        transform.SetParent(top, true);        // draw above everything while dragging
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        image.raycastTarget = true;
        if (IsRemoved) return; // merged into another stack

        Transform target = parentAfterDrag;

        // Rare case: a new item was picked up into our old slot while we were dragging.
        var slot = target != null ? target.GetComponent<InventorySlot>() : null;
        if (slot != null && slot.CurrentItem != null && InventoryManager.instance != null)
        {
            var empty = InventoryManager.instance.FindEmptySlot();
            if (empty != null) target = empty.transform;
        }

        transform.SetParent(target, false);
        SnapToParent();
    }
}
