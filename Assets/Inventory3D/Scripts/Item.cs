using UnityEngine;

public enum ItemType { BuildingBlock, Tool, Other }
public enum ActionType { None, Dig, Mine, Chop }

/// <summary>
/// Data for one kind of item. Same idea as the tutorial's Item ScriptableObject,
/// but the 2D Tile is replaced with a 3D prefab.
/// Create new ones with: right-click in Project > Create > Inventory 3D > Item.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory 3D/Item")]
public class Item : ScriptableObject
{
    [Header("Inventory UI")]
    public Sprite image;
    public bool stackable = true;

    [Header("3D World")]
    [Tooltip("Model used when this item is placed, dropped or held. Blocks should be 1x1x1. " +
             "Leave empty for tools: the icon is shown as a flat 3D sprite instead.")]
    public GameObject worldPrefab;

    [Header("Gameplay")]
    public ItemType type = ItemType.BuildingBlock;
    [Tooltip("For tools: which kind of Breakable this tool can break.")]
    public ActionType actionType = ActionType.None;
    [Tooltip("How far (in meters) the player can reach with this item.")]
    public float range = 5f;
}
