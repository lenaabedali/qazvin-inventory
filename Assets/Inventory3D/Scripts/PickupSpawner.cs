using UnityEngine;

/// <summary>
/// Spawns item pickups when the game starts. Handy for placing starting loot in a level
/// (the demo scene uses it for the floating blocks you walk into).
/// </summary>
public class PickupSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Item item;
        public Vector3 position;
        public int count = 1;
    }

    public Entry[] pickups = new Entry[0];

    void Start()
    {
        foreach (var e in pickups)
        {
            if (e == null || e.item == null) continue;
            var p = ItemPickup.Spawn(e.item, e.position, Mathf.Max(1, e.count), 0f);
            p.transform.SetParent(transform, true);
        }
    }
}
