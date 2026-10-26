using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Small wrapper so the inventory works with both the old Input Manager
/// and the new Input System package (Unity 6 projects often default to the new one).
/// </summary>
public static class InputHelper
{
#if ENABLE_LEGACY_INPUT_MANAGER
    public static bool InventoryToggleDown => Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
    public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);
    public static bool DropDown => Input.GetKeyDown(KeyCode.Q);
    public static bool JumpDown => Input.GetKeyDown(KeyCode.Space);
    public static bool PrimaryDown => Input.GetMouseButtonDown(0);
    public static bool SecondaryDown => Input.GetMouseButtonDown(1);
    public static float Scroll => Input.mouseScrollDelta.y;
    public static Vector2 LookDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    public static Vector2 Move => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    /// <summary>Returns 1-9 if that number key was pressed this frame, otherwise -1.</summary>
    public static int NumberKeyDown()
    {
        for (int i = 1; i <= 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + (i - 1))) return i;
        return -1;
    }
#elif ENABLE_INPUT_SYSTEM
    static Keyboard K => Keyboard.current;
    static Mouse M => Mouse.current;

    public static bool InventoryToggleDown => K != null && (K.tabKey.wasPressedThisFrame || K.iKey.wasPressedThisFrame);
    public static bool EscapeDown => K != null && K.escapeKey.wasPressedThisFrame;
    public static bool DropDown => K != null && K.qKey.wasPressedThisFrame;
    public static bool JumpDown => K != null && K.spaceKey.wasPressedThisFrame;
    public static bool PrimaryDown => M != null && M.leftButton.wasPressedThisFrame;
    public static bool SecondaryDown => M != null && M.rightButton.wasPressedThisFrame;
    public static float Scroll => M != null ? M.scroll.ReadValue().y : 0f;
    public static Vector2 LookDelta => M != null ? M.delta.ReadValue() * 0.1f : Vector2.zero;

    public static Vector2 Move
    {
        get
        {
            if (K == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (K.aKey.isPressed || K.leftArrowKey.isPressed) x -= 1f;
            if (K.dKey.isPressed || K.rightArrowKey.isPressed) x += 1f;
            if (K.sKey.isPressed || K.downArrowKey.isPressed) y -= 1f;
            if (K.wKey.isPressed || K.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }

    public static int NumberKeyDown()
    {
        if (K == null) return -1;
        for (int i = 1; i <= 9; i++)
            if (K[Key.Digit1 + (i - 1)].wasPressedThisFrame) return i;
        return -1;
    }
#else
    public static bool InventoryToggleDown => false;
    public static bool EscapeDown => false;
    public static bool DropDown => false;
    public static bool JumpDown => false;
    public static bool PrimaryDown => false;
    public static bool SecondaryDown => false;
    public static float Scroll => 0f;
    public static Vector2 LookDelta => Vector2.zero;
    public static Vector2 Move => Vector2.zero;
    public static int NumberKeyDown() => -1;
#endif
}
