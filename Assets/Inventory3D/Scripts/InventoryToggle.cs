using UnityEngine;

/// <summary>
/// Opens/closes the main inventory with Tab (or I) and unlocks the cursor while it's open,
/// so drag and drop works in a first-person game. Replaces the tutorial's on-screen button (3:50 - 5:13).
/// </summary>
public class InventoryToggle : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public GameObject mainInventoryGroup;
    [Tooltip("Scripts to switch off while the inventory is open, e.g. mouse look / player movement.")]
    public MonoBehaviour[] disableWhileOpen;

    void Start()
    {
        SetOpen(false);
    }

    void Update()
    {
        if (InputHelper.InventoryToggleDown) SetOpen(!IsOpen);
        else if (IsOpen && InputHelper.EscapeDown) SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (mainInventoryGroup != null) mainInventoryGroup.SetActive(open);

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (disableWhileOpen != null)
            foreach (var script in disableWhileOpen)
                if (script != null) script.enabled = !open;
    }

    /// <summary>Hooked up to the dark background's Button, like in the video.</summary>
    public void Close() => SetOpen(false);
}
