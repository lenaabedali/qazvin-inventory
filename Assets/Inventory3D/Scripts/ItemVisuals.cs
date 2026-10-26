using UnityEngine;

/// <summary>
/// Builds a decoration-only 3D model of an item (used for pickups and the item in your hand).
/// Uses the item's worldPrefab if it has one, otherwise shows its icon as a flat sprite (Minecraft style).
/// </summary>
public static class ItemVisuals
{
    public static GameObject CreateModel(Item item, float size)
    {
        GameObject go;
        if (item.worldPrefab != null)
        {
            go = Object.Instantiate(item.worldPrefab);
            go.transform.localScale = item.worldPrefab.transform.localScale * size;
        }
        else
        {
            go = new GameObject(item.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = item.image;
            float w = 1f;
            if (item.image != null)
                w = Mathf.Max(item.image.bounds.size.x, item.image.bounds.size.y);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, w));
        }

        Strip(go);
        return go;
    }

    /// <summary>Removes physics and gameplay scripts so the copy is just a picture.</summary>
    public static void Strip(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Breakable>(true)) SafeDestroy(c);
        foreach (var c in go.GetComponentsInChildren<ItemPickup>(true)) SafeDestroy(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
        foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(c);
    }

    public static void SafeDestroy(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }
}
