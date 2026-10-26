using UnityEngine;

/// <summary>
/// Uses the selected item by aiming with the crosshair (center of the screen).
///   Left click  = break what you're aiming at (if your tool can)
///   Right click = place the selected block
///   Q           = drop one of the selected item in front of the player
/// 3D replacement for the tutorial's tilemap building system (32:45 - 34:50).
///
/// Third person: set "player" so the ray skips your own character and reach is measured
/// from the player, not the camera. Leave it empty for a first-person game.
/// </summary>
public class ItemUser : MonoBehaviour
{
    public Camera cam;
    [Tooltip("Your character. Its colliders are ignored by the aim ray, and reach is measured from it.")]
    public Transform player;
    [Tooltip("Height above the player's pivot that reach is measured from (roughly chest height).")]
    public float reachOriginHeight = 1.2f;
    [Tooltip("Reach when holding nothing.")]
    public float handRange = 4f;
    public bool snapToGrid = true;
    [Tooltip("Blocks are centered on multiples of this. Block prefabs should be this size.")]
    public float gridSize = 1f;
    public LayerMask hitMask = ~0;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    Vector3 ReachOrigin => player != null ? player.position + Vector3.up * reachOriginHeight : cam.transform.position;

    void Update()
    {
        if (InventoryToggle.IsOpen || Cursor.lockState != CursorLockMode.Locked) return;
        var inventory = InventoryManager.instance;
        if (inventory == null) return;

        if (InputHelper.DropDown) DropSelected(inventory);

        bool primary = InputHelper.PrimaryDown;
        bool secondary = InputHelper.SecondaryDown;
        if (!primary && !secondary) return;

        Item item = inventory.GetSelectedItem(false);
        float range = item != null ? item.range : handRange;

        // Aim from the camera through the crosshair, but measure reach from the player
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxDistance = range + Vector3.Distance(cam.transform.position, ReachOrigin);
        if (!AimRaycast(ray, maxDistance, out RaycastHit hit)) return;
        if (Vector3.Distance(ReachOrigin, hit.point) > range) return;

        if (primary)
        {
            var breakable = hit.collider.GetComponentInParent<Breakable>();
            if (breakable != null && breakable.CanBreakWith(item)) breakable.Break();
        }
        else if (item != null && item.type == ItemType.BuildingBlock && item.worldPrefab != null)
        {
            Place(item, hit, inventory);
        }
    }

    /// <summary>Closest hit along the ray that isn't part of the player.</summary>
    bool AimRaycast(Ray ray, float maxDistance, out RaycastHit closest)
    {
        closest = default;
        bool found = false;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(ray, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (player != null && hit.collider.transform.IsChildOf(player)) continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                closest = hit;
                found = true;
            }
        }
        return found;
    }

    void Place(Item item, RaycastHit hit, InventoryManager inventory)
    {
        Vector3 pos = hit.point + hit.normal * (gridSize * 0.5f);
        if (snapToGrid) pos = Snap(pos);

        // Don't place inside the player or another block
        Vector3 halfExtents = Vector3.one * gridSize * 0.45f;
        if (Physics.CheckBox(pos, halfExtents, Quaternion.identity, hitMask, QueryTriggerInteraction.Ignore)) return;

        GameObject placed = Instantiate(item.worldPrefab, pos, Quaternion.identity);
        var breakable = placed.GetComponent<Breakable>();
        if (breakable != null && breakable.dropItem == null) breakable.dropItem = item;

        inventory.GetSelectedItem(true); // use one up
    }

    void DropSelected(InventoryManager inventory)
    {
        Item item = inventory.GetSelectedItem(true);
        if (item == null) return;

        Vector3 pos;
        if (player != null)
        {
            pos = player.position + Vector3.up * 1f + player.forward * 1.4f;
        }
        else
        {
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            pos = cam.transform.position + forward * 1.6f;
        }
        ItemPickup.Spawn(item, pos, 1, 1.5f, true);
    }

    Vector3 Snap(Vector3 p)
    {
        return new Vector3(
            Mathf.Round(p.x / gridSize) * gridSize,
            Mathf.Round(p.y / gridSize) * gridSize,
            Mathf.Round(p.z / gridSize) * gridSize);
    }
}
