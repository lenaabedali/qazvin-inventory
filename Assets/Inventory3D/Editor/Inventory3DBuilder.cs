using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu: Tools > Inventory 3D
///   Build Demo Scene                    - new scene with a player, blocks, pickups and the full inventory UI
///   Add Inventory UI To Current Scene   - just the UI + manager (tutorial chapters 1-3) for your own game
/// </summary>
public static class Inventory3DBuilder
{
    const string Root = "Assets/Inventory3D/Generated";
    const string DemoScenePath = "Assets/Inventory3D/Demo/Inventory3D_Demo.unity";

    // Same sizes as the video: 55x55 slots, 7 per row
    const float SlotSize = 55f;
    const float Spacing = 6f;
    const int Pad = 10;
    const int Columns = 7;
    const int MainRows = 3;

    const int MainTopPad = 34;        // room for the carved title
    const float MainCenterY = 30f;
    const float HandleWidth = 150f;

    // ------------------------------------------------------------------ menus

    [MenuItem("Tools/Inventory 3D/Build Demo Scene", priority = 1)]
    public static void BuildDemoScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("Inventory 3D builder v5 (stone): building demo scene...");

        // Open the new scene FIRST. Opening a scene unloads unused assets, which would invalidate
        // references to items created before it ("MissingReferenceException: Item has been destroyed").
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var oldCam = GameObject.Find("Main Camera");
        if (oldCam != null) Object.DestroyImmediate(oldCam);

        // Create (or update) the demo item assets. We don't keep references to them: Unity can unload
        // assets in the middle of a build, so everything is re-loaded from disk right where it's used.
        CreateDemoItems();

        // Ground: top surface at y = -0.5 so 1x1 blocks sit on whole-number grid positions
        var groundMat = MakeMaterial("Ground", new Color(0.36f, 0.52f, 0.3f), null);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -1f, 0f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // Some blocks to break
        var blocks = new GameObject("Blocks").transform;
        for (int x = -3; x <= 3; x++) PlaceBlock(LoadBlock("Grass Block"), new Vector3(x, 0, 5), blocks);
        for (int x = -2; x <= 2; x++) PlaceBlock(LoadBlock("Grass Block"), new Vector3(x, 1, 5), blocks);
        for (int y = 0; y <= 2; y++) PlaceBlock(LoadBlock("Stone Block"), new Vector3(5, y, 2), blocks);
        PlaceBlock(LoadBlock("Stone Block"), new Vector3(6, 0, 2), blocks);
        PlaceBlock(LoadBlock("Stone Block"), new Vector3(5, 0, 3), blocks);
        for (int y = 0; y <= 3; y++) PlaceBlock(LoadBlock("Wood Block"), new Vector3(-5, y, 3), blocks);

        // Some pickups lying around (walk into them). They're spawned at Play time by PickupSpawner,
        // because creating them in the editor was unreliable in Unity 6.6.
        var spawner = new GameObject("Pickups").AddComponent<PickupSpawner>();
        spawner.pickups = new[]
        {
            new PickupSpawner.Entry { item = LoadItem("Grass Block"), position = new Vector3(-1.5f, 0f, 1f), count = 1 },
            new PickupSpawner.Entry { item = LoadItem("Grass Block"), position = new Vector3(-2.5f, 0f, 1.5f), count = 1 },
            new PickupSpawner.Entry { item = LoadItem("Stone Block"), position = new Vector3(1.5f, 0f, 1f), count = 5 },
            new PickupSpawner.Entry { item = LoadItem("Wood Block"), position = new Vector3(0f, 0f, 2.5f), count = 3 },
        };

        // Player (pivot at the feet)
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0f, -3f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        // Stand-in character model: a capsule body and a visor so you can see which way it faces
        var bodyMat = MakeMaterial("Player Body", new Color(0.25f, 0.5f, 0.9f), null);
        var visorMat = MakeMaterial("Player Visor", new Color(0.1f, 0.1f, 0.15f), null);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        visor.transform.SetParent(player.transform, false);
        visor.transform.localPosition = new Vector3(0f, 1.5f, 0.33f);
        visor.transform.localScale = new Vector3(0.5f, 0.15f, 0.2f);
        visor.GetComponent<Renderer>().sharedMaterial = visorMat;

        // Hand: where the selected item shows up. In your game, put ItemHolder on the hand bone instead.
        var hand = new GameObject("Hand");
        hand.transform.SetParent(player.transform, false);
        hand.transform.localPosition = new Vector3(0.5f, 0.9f, 0.3f);
        hand.AddComponent<ItemHolder>();

        // Stone bag carried in the other hand, modeled on the bags restored from Qazvin
        StoneStyle.EnsureBagPrefab();
        StoneStyle.AddBagToCharacter(player.transform, new Vector3(-0.5f, 0.95f, 0f));

        // Camera is NOT a child of the player, so it doesn't spin when the character turns
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0.6f, 2.6f, -7f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        camGo.AddComponent<AudioListener>();

        var controller = player.AddComponent<ThirdPersonController>();
        controller.cam = cam;
        var user = player.AddComponent<ItemUser>();
        user.cam = cam;
        user.player = player.transform;

        // Inventory UI, starting with the three tools (like the video's start items)
        var manager = BuildInventoryUI(new[] { "Pickaxe", "Shovel", "Axe" }, true);
        manager.GetComponent<InventoryToggle>().disableWhileOpen = new MonoBehaviour[] { controller };

        EnsureFolder(Path.GetDirectoryName(DemoScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;

        Debug.Log("Inventory 3D demo scene built at " + DemoScenePath + ". Press Play. " +
                  "Tab = inventory, 1-7 / scroll = select, Left click = break, Right click = place, Q = drop.");
    }

    [MenuItem("Tools/Inventory 3D/Add Inventory UI To Current Scene", priority = 2)]
    public static void AddInventoryUIToCurrentScene()
    {
        var manager = BuildInventoryUI(new string[0], false);
        Selection.activeGameObject = manager.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("Inventory UI added. Next: tag your player \"Player\", add ItemUser to it (set Cam and Player), " +
                  "add ItemHolder to your character's hand, and drag your camera/movement scripts into " +
                  "Inventory Manager > InventoryToggle > Disable While Open.");
    }

    // ------------------------------------------------------------------ UI (tutorial chapters 1-3)

    static InventoryManager BuildInventoryUI(string[] startItemNames, bool showHints)
    {
        EnsureFolder(Root + "/Prefabs");
        Font font = GetFont();

        float width = Columns * SlotSize + (Columns - 1) * Spacing + 2 * Pad;
        float mainHeight = MainRows * SlotSize + (MainRows - 1) * Spacing + Pad + MainTopPad;
        float toolbarHeight = SlotSize + 2 * Pad;

        // Carved-stone sprites, inspired by the stone bags restored from Qazvin
        StoneStyle.EnsureUISprites(new Vector2(width, toolbarHeight), new Vector2(width, mainHeight), SlotSize, HandleWidth);

        GameObject slotPrefab = CreateSlotPrefab();
        GameObject itemPrefab = CreateItemPrefab(font);
        EnsureEventSystem();

        // Canvas: Screen Space Overlay + scale with screen size
        var canvasGo = new GameObject("Inventory Canvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Main Inventory Group goes FIRST in the hierarchy so the toolbar draws above the dark background
        var group = NewUI("Main Inventory Group", canvasGo.transform);
        Stretch(group);

        var dark = NewUI("Dark Background", group);
        Stretch(dark);
        dark.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.025f, 0.62f);
        var darkButton = dark.gameObject.AddComponent<Button>();
        darkButton.transition = Selectable.Transition.None;
        var nav = darkButton.navigation;
        nav.mode = Navigation.Mode.None;
        darkButton.navigation = nav;

        // Stone handle on top, so the open inventory looks like one of the Qazvin stone bags.
        // It comes before the panel in the hierarchy so the panel covers its base.
        var handleSprite = StoneStyle.LoadUISprite("Handle");
        var handle = NewUI("Bag Handle", group);
        handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.pivot = new Vector2(0.5f, 0f);
        float handleHeight = HandleWidth * handleSprite.rect.height / handleSprite.rect.width;
        handle.sizeDelta = new Vector2(HandleWidth, handleHeight);
        handle.anchoredPosition = new Vector2(0f, MainCenterY + mainHeight * 0.5f - 10f);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.sprite = handleSprite;
        handleImg.raycastTarget = false;

        var main = NewUI("Main Inventory", group);
        main.anchorMin = main.anchorMax = main.pivot = new Vector2(0.5f, 0.5f);
        main.sizeDelta = new Vector2(width, mainHeight);
        main.anchoredPosition = new Vector2(0f, MainCenterY);
        AddStoneImage(main.gameObject, "Main Panel");
        AddGrid(main.gameObject, MainTopPad);

        // Title carved into the stone: dark letters with a light lower lip
        var title = NewUI("Title", main);
        title.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(0f, MainTopPad - 4f);
        title.anchoredPosition = new Vector2(0f, -3f);
        var titleText = AddText(title.gameObject, font, "INVENTORY", 18, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.1f, 0.11f, 0.1f, 0.9f);
        var carve = title.gameObject.AddComponent<Shadow>();
        carve.effectColor = new Color(1f, 1f, 1f, 0.16f);
        carve.effectDistance = new Vector2(0f, -1.5f);

        var mainSlots = new List<InventorySlot>();
        for (int i = 0; i < Columns * MainRows; i++) mainSlots.Add(InstantiateSlot(slotPrefab, main));

        // Toolbar, anchored bottom center
        var toolbar = NewUI("Toolbar", canvasGo.transform);
        toolbar.anchorMin = toolbar.anchorMax = new Vector2(0.5f, 0f);
        toolbar.pivot = new Vector2(0.5f, 0f);
        toolbar.sizeDelta = new Vector2(width, toolbarHeight);
        toolbar.anchoredPosition = new Vector2(0f, 20f);
        AddStoneImage(toolbar.gameObject, "Toolbar");
        AddGrid(toolbar.gameObject, Pad);

        var toolbarSlots = new List<InventorySlot>();
        for (int i = 0; i < Columns; i++) toolbarSlots.Add(InstantiateSlot(slotPrefab, toolbar));

        // Crosshair (where ItemUser aims)
        var cross = NewUI("Crosshair", canvasGo.transform);
        cross.anchorMin = cross.anchorMax = cross.pivot = new Vector2(0.5f, 0.5f);
        cross.sizeDelta = new Vector2(8f, 8f);
        var crossImg = cross.gameObject.AddComponent<Image>();
        crossImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        crossImg.color = new Color(1f, 1f, 1f, 0.85f);
        crossImg.raycastTarget = false;

        if (showHints)
        {
            var hints = NewUI("Controls Hint", canvasGo.transform);
            hints.anchorMin = hints.anchorMax = hints.pivot = new Vector2(0f, 1f);
            hints.sizeDelta = new Vector2(900f, 40f);
            hints.anchoredPosition = new Vector2(20f, -16f);
            var t = AddText(hints.gameObject, font,
                "WASD move   Tab inventory   1-7 / scroll select   Left click break   Right click place   Q drop",
                18, TextAnchor.UpperLeft);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        }

        // Manager + toggle
        var managerGo = new GameObject("Inventory Manager");
        var manager = managerGo.AddComponent<InventoryManager>();
        var allSlots = new List<InventorySlot>(toolbarSlots);
        allSlots.AddRange(mainSlots);
        manager.inventorySlots = allSlots.ToArray();
        manager.inventoryItemPrefab = itemPrefab;
        manager.toolbarSize = Columns;
        var startItems = new List<Item>();
        foreach (var itemName in startItemNames)
        {
            var item = LoadItem(itemName);
            if (item != null) startItems.Add(item);
        }
        manager.startItems = startItems.ToArray();

        var toggle = managerGo.AddComponent<InventoryToggle>();
        toggle.mainInventoryGroup = group.gameObject;
        toggle.disableWhileOpen = new MonoBehaviour[0];
        UnityEventTools.AddPersistentListener(darkButton.onClick, toggle.Close);

        group.gameObject.SetActive(false);

        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Inventory UI");
        Undo.RegisterCreatedObjectUndo(managerGo, "Build Inventory UI");
        return manager;
    }

    static GameObject CreateSlotPrefab()
    {
        var rt = NewUI("InventorySlot", null);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        var img = AddStoneImage(rt.gameObject, "Slot");
        var slot = rt.gameObject.AddComponent<InventorySlot>();
        slot.image = img;
        slot.normalSprite = img.sprite;
        slot.selectedSprite = StoneStyle.LoadUISprite("Slot Selected");
        slot.selectedColor = Color.white;
        slot.notSelectedColor = Color.white;

        var prefab = PrefabUtility.SaveAsPrefabAsset(rt.gameObject, Root + "/Prefabs/InventorySlot.prefab");
        Object.DestroyImmediate(rt.gameObject);
        return prefab;
    }

    static GameObject CreateItemPrefab(Font font)
    {
        var rt = NewUI("InventoryItem", null);
        rt.sizeDelta = new Vector2(SlotSize - 12f, SlotSize - 12f);
        var img = rt.gameObject.AddComponent<Image>();
        img.preserveAspect = true;

        var countRt = NewUI("Count", rt);
        countRt.anchorMin = new Vector2(0f, 0f);
        countRt.anchorMax = new Vector2(1f, 0.6f);
        countRt.offsetMin = new Vector2(0f, -6f);
        countRt.offsetMax = new Vector2(5f, 0f);
        var countText = AddText(countRt.gameObject, font, "1", 18, TextAnchor.LowerRight);
        countText.fontStyle = FontStyle.Bold;
        countText.color = new Color(0.96f, 0.92f, 0.82f);
        var outline = countRt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        countRt.gameObject.SetActive(false);

        var inventoryItem = rt.gameObject.AddComponent<InventoryItem>();
        inventoryItem.image = img;
        inventoryItem.countText = countText;

        var prefab = PrefabUtility.SaveAsPrefabAsset(rt.gameObject, Root + "/Prefabs/InventoryItem.prefab");
        Object.DestroyImmediate(rt.gameObject);
        return prefab;
    }

    static InventorySlot InstantiateSlot(GameObject prefab, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        return go.GetComponent<InventorySlot>();
    }

    static void AddGrid(GameObject go, int topPad)
    {
        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(SlotSize, SlotSize);
        grid.spacing = new Vector2(Spacing, Spacing);
        grid.padding = new RectOffset(Pad, Pad, topPad, Pad);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image AddStoneImage(GameObject go, string spriteName)
    {
        var img = go.AddComponent<Image>();
        img.sprite = StoneStyle.LoadUISprite(spriteName);
        img.type = Image.Type.Simple;
        img.color = Color.white;
        return img;
    }

    static Text AddText(GameObject go, Font font, string text, int size, TextAnchor anchor)
    {
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindAnyObjectByType<EventSystem>();
#else
        var existing = Object.FindObjectOfType<EventSystem>();
#endif
        if (existing != null) return;

        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    // ------------------------------------------------------------------ demo items

    static Item LoadItem(string name) => AssetDatabase.LoadAssetAtPath<Item>(Root + "/Items/" + name + ".asset");
    static GameObject LoadBlock(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");

    static void CreateDemoItems()
    {
        SaveIcon("Grass Block", BlockPixels(new Color(0.55f, 0.38f, 0.22f), new Color(0.35f, 0.7f, 0.25f), 1, false));
        SaveIcon("Stone Block", BlockPixels(new Color(0.55f, 0.55f, 0.58f), null, 2, false));
        SaveIcon("Wood Block", BlockPixels(new Color(0.62f, 0.45f, 0.26f), null, 3, true));
        SaveIcon("Pickaxe", ToolPixels(ToolShape.Pickaxe));
        SaveIcon("Shovel", ToolPixels(ToolShape.Shovel));
        SaveIcon("Axe", ToolPixels(ToolShape.Axe));

        MakeBlockPrefab("Grass Block", MakeMaterial("Grass Block", Color.white, LoadIconTexture("Grass Block")), ActionType.Dig);
        MakeBlockPrefab("Stone Block", MakeMaterial("Stone Block", Color.white, LoadIconTexture("Stone Block")), ActionType.Mine);
        MakeBlockPrefab("Wood Block", MakeMaterial("Wood Block", Color.white, LoadIconTexture("Wood Block")), ActionType.Chop);
        AssetDatabase.SaveAssets();

        MakeItem("Grass Block", ItemType.BuildingBlock, ActionType.None, true, 6f, true);
        MakeItem("Stone Block", ItemType.BuildingBlock, ActionType.None, true, 6f, true);
        MakeItem("Wood Block", ItemType.BuildingBlock, ActionType.None, true, 6f, true);
        MakeItem("Pickaxe", ItemType.Tool, ActionType.Mine, false, 5f, false);
        MakeItem("Shovel", ItemType.Tool, ActionType.Dig, false, 5f, false);
        MakeItem("Axe", ItemType.Tool, ActionType.Chop, false, 5f, false);
        AssetDatabase.SaveAssets();

        LinkDrop("Grass Block");
        LinkDrop("Stone Block");
        LinkDrop("Wood Block");
        AssetDatabase.SaveAssets();
    }

    static Sprite LoadIcon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Icons/" + name + ".png");
    static Texture2D LoadIconTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Icons/" + name + ".png");

    static void LinkDrop(string name)
    {
        var prefab = LoadBlock(name);
        prefab.GetComponent<Breakable>().dropItem = LoadItem(name);
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
    }

    static void PlaceBlock(GameObject prefab, Vector3 pos, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
    }

    static void MakeItem(string name, ItemType type, ActionType action, bool stackable, float range, bool isBlock)
    {
        EnsureFolder(Root + "/Items");
        string path = Root + "/Items/" + name + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<Item>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<Item>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.image = LoadIcon(name);
        item.type = type;
        item.actionType = action;
        item.stackable = stackable;
        item.range = range;
        item.worldPrefab = isBlock ? LoadBlock(name) : null;
        EditorUtility.SetDirty(item);
    }

    static void MakeBlockPrefab(string name, Material mat, ActionType required)
    {
        EnsureFolder(Root + "/Prefabs");
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<Breakable>().requiredAction = required;
        PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }

    /// <summary>Copies the render pipeline's default material (works in Built-in, URP and HDRP).</summary>
    static Material MakeMaterial(string name, Color color, Texture texture)
    {
        EnsureFolder(Root + "/Materials");
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(temp);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        if (texture != null) mat.mainTexture = texture;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ pixel-art icons

    const int S = 32;

    static void SaveIcon(string name, Color32[] pixels)
    {
        EnsureFolder(Root + "/Icons");
        string path = Root + "/Icons/" + name + ".png";

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = S;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static Color32[] BlockPixels(Color baseColor, Color? topColor, int seed, bool planks)
    {
        var px = new Color32[S * S];
        var rnd = new System.Random(seed);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                Color c = baseColor;
                if (topColor.HasValue && y >= 23 + (x * 7 + seed) % 3) c = topColor.Value;
                if (planks && y % 8 == 0) c = Scale(c, 0.7f);
                c = Scale(c, 0.85f + (float)rnd.NextDouble() * 0.3f);
                if (x < 2 || y < 2 || x > S - 3 || y > S - 3) c = Scale(c, 0.6f);
                px[y * S + x] = c;
            }
        }
        return px;
    }

    enum ToolShape { Pickaxe, Shovel, Axe }

    static Color32[] ToolPixels(ToolShape shape)
    {
        var px = new Color32[S * S]; // transparent
        Color handle = new Color(0.5f, 0.33f, 0.18f);
        Color metal = new Color(0.78f, 0.8f, 0.85f);

        switch (shape)
        {
            case ToolShape.Pickaxe:
                Line(px, 5, 5, 22, 22, handle, 2);
                Line(px, 13, 28, 22, 23, metal, 3);
                Line(px, 22, 23, 28, 13, metal, 3);
                break;
            case ToolShape.Shovel:
                Line(px, 4, 4, 19, 19, handle, 2);
                Disc(px, 23, 23, 5.5f, metal);
                break;
            case ToolShape.Axe:
                Line(px, 5, 5, 23, 23, handle, 2);
                Disc(px, 17, 25, 5f, metal);
                Line(px, 17, 22, 23, 23, handle, 2);
                break;
        }

        AddOutline(px, new Color(0.1f, 0.1f, 0.12f));
        return px;
    }

    static void Line(Color32[] px, int x0, int y0, int x1, int y1, Color c, int thickness)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2;
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            for (int dy = 0; dy < thickness; dy++)
                for (int dx = 0; dx < thickness; dx++)
                    Set(px, cx + dx, cy + dy, c);
        }
    }

    static void Disc(Color32[] px, int cx, int cy, float r, Color c)
    {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(px, x, y, c);
    }

    static void AddOutline(Color32[] px, Color outline)
    {
        var copy = (Color32[])px.Clone();
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                if (copy[y * S + x].a != 0) continue;
                bool touches = false;
                for (int dy = -1; dy <= 1 && !touches; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                        if (copy[ny * S + nx].a != 0) { touches = true; break; }
                    }
                if (touches) px[y * S + x] = outline;
            }
        }
    }

    static void Set(Color32[] px, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= S || y >= S) return;
        px[y * S + x] = c;
    }

    static Color Scale(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

    // ------------------------------------------------------------------ helpers

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
