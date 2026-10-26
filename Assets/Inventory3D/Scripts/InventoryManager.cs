using UnityEngine;

/// <summary>
/// Adds, stacks, selects and uses items (tutorial 11:02 - 30:04 and 31:44 - 32:45).
/// Same public methods as the video, so other scripts call:
///   InventoryManager.instance.AddItem(item)            -> true if it fit
///   InventoryManager.instance.GetSelectedItem(false)   -> what's in hand
///   InventoryManager.instance.GetSelectedItem(true)    -> use up one of it
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    public int maxStackedItems = 64;
    [Tooltip("How many of the first slots are the toolbar (selectable with 1-9 and the scroll wheel).")]
    public int toolbarSize = 7;
    [Tooltip("Toolbar slots first, then the main inventory slots.")]
    public InventorySlot[] inventorySlots;
    public GameObject inventoryItemPrefab;
    [Tooltip("Items the player starts with, e.g. a pickaxe and shovel.")]
    public Item[] startItems;

    [Tooltip("How much scrolling moves one slot. Raise it if a trackpad scrolls too fast.")]
    public float scrollStep = 0.5f;

    int selectedSlot = -1;
    float scrollAccumulator;
    public int SelectedSlot => selectedSlot;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        ChangeSelectedSlot(0);
        if (startItems != null)
            foreach (var item in startItems)
                if (item != null) AddItem(item);
    }

    void Update()
    {
        int toolbar = Mathf.Min(toolbarSize, inventorySlots.Length);
        if (toolbar <= 0) return;

        // Number keys 1..toolbarSize
        int number = InputHelper.NumberKeyDown();
        if (number >= 1 && number <= toolbar)
            ChangeSelectedSlot(number - 1);

        // Scroll wheel (new for 3D): scroll down = next slot, wraps around.
        // Accumulated so a trackpad's many tiny scroll events don't race through the slots.
        scrollAccumulator += InputHelper.Scroll;
        if (Mathf.Abs(scrollAccumulator) >= scrollStep)
        {
            int next = (selectedSlot - (int)Mathf.Sign(scrollAccumulator) + toolbar) % toolbar;
            ChangeSelectedSlot(next);
            scrollAccumulator = 0f;
        }
    }

    public void ChangeSelectedSlot(int newValue)
    {
        if (newValue < 0 || newValue >= inventorySlots.Length) return;
        if (selectedSlot >= 0) inventorySlots[selectedSlot].Deselect();
        inventorySlots[newValue].Select();
        selectedSlot = newValue;
    }

    /// <summary>Returns false if the inventory is full (so the pickup stays in the world).</summary>
    public bool AddItem(Item item)
    {
        if (item == null) return false;

        // 1) Top up an existing stack of the same item
        if (item.stackable)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                InventoryItem itemInSlot = inventorySlots[i].CurrentItem;
                if (itemInSlot != null && itemInSlot.item == item && itemInSlot.count < maxStackedItems)
                {
                    itemInSlot.count++;
                    itemInSlot.RefreshCount();
                    return true;
                }
            }
        }

        // 2) Otherwise use the first empty slot
        InventorySlot empty = FindEmptySlot();
        if (empty != null)
        {
            SpawnNewItem(item, empty);
            return true;
        }

        return false;
    }

    public InventorySlot FindEmptySlot()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
            if (inventorySlots[i].CurrentItem == null) return inventorySlots[i];
        return null;
    }

    void SpawnNewItem(Item item, InventorySlot slot)
    {
        GameObject newItemGo = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItem inventoryItem = newItemGo.GetComponent<InventoryItem>();
        inventoryItem.InitialiseItem(item);
    }

    /// <param name="use">true = remove one from the stack (placing a block, eating, etc.)</param>
    public Item GetSelectedItem(bool use)
    {
        if (selectedSlot < 0 || selectedSlot >= inventorySlots.Length) return null;

        InventoryItem itemInSlot = inventorySlots[selectedSlot].CurrentItem;
        if (itemInSlot == null) return null;

        Item item = itemInSlot.item;
        if (use)
        {
            itemInSlot.count--;
            if (itemInSlot.count <= 0) itemInSlot.MarkRemoved();
            else itemInSlot.RefreshCount();
        }
        return item;
    }
}
