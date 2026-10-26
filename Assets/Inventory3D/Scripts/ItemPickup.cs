using UnityEngine;

/// <summary>
/// An item lying in the 3D world. Walk into it to pick it up.
/// 3D version of the tutorial's loot script (33:24 - 34:18).
/// Needs: a trigger collider + kinematic Rigidbody on this object, and your player tagged "Player".
/// Use ItemPickup.Spawn(...) to create one from code.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public Item item;
    public int count = 1;
    [Tooltip("Seconds before it can be picked up (stops you instantly re-grabbing something you dropped).")]
    public float pickupDelay = 0.3f;
    public bool spinAndBob = true;

    float spawnTime;
    float baseY;

    void Awake() => spawnTime = Time.time;
    void Start() => baseY = transform.position.y;

    void Update()
    {
        if (!spinAndBob) return;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        var p = transform.position;
        p.y = baseY + Mathf.Sin((Time.time - spawnTime) * 2.5f) * 0.08f;
        transform.position = p;
    }

    // Stay (not just Enter) so it gets picked up once the delay ends or once you free up space.
    void OnTriggerEnter(Collider other) => TryPickup(other);
    void OnTriggerStay(Collider other) => TryPickup(other);

    void TryPickup(Collider other)
    {
        if (count <= 0 || item == null) return;
        if (Time.time - spawnTime < pickupDelay) return;
        if (!other.CompareTag("Player")) return;
        if (InventoryManager.instance == null) return;

        while (count > 0 && InventoryManager.instance.AddItem(item))
            count--;

        if (count <= 0) Destroy(gameObject);
    }

    /// <summary>Creates a floating, spinning pickup for an item.</summary>
    public static ItemPickup Spawn(Item item, Vector3 position, int count = 1, float pickupDelay = 0.3f, bool dropToGround = false)
    {
        if (dropToGround &&
            Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.3f;
        }

        string itemName = item.name;
        GameObject go = ItemVisuals.CreateModel(item, item.worldPrefab != null ? 0.3f : 0.5f);
        go.name = itemName + " (pickup)";
        go.transform.position = position;

        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f / Mathf.Max(0.0001f, go.transform.lossyScale.x);

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;
        pickup.count = count;
        pickup.pickupDelay = pickupDelay;
        return pickup;
    }
}
