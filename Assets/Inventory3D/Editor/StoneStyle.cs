using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the carved-stone inventory sprites and the 3D stone bag (modeled on the bags restored from Qazvin).
/// Menu: Tools > Inventory 3D > Add Stone Bag To Selected Character
/// </summary>
public static class StoneStyle
{
    const string Root = "Assets/Inventory3D/Generated";
    const string UIFolder = Root + "/Stone UI";
    const string BagFolder = Root + "/Stone Bag";
    const string BagPrefabPath = BagFolder + "/Stone Bag.prefab";

    // Bag shape in meters: a bell-shaped slab with a handle opening cut through the top
    const float BagWidth = 0.44f;
    const float BagSideHeight = 0.18f;   // straight sides
    const float BagArchHeight = 0.20f;   // rounded top
    const float BagHandleBand = 0.045f;  // thickness of stone around the handle opening
    const float BagHoleBottom = 0.22f;   // bottom of the handle opening
    const float BagThickness = 0.07f;
    const float BagHeight = BagSideHeight + BagArchHeight;

    // ------------------------------------------------------------------ UI sprites

    /// <summary>Renders the stone UI sprites at 2x the on-screen size so they stay crisp.</summary>
    public static void EnsureUISprites(Vector2 toolbarSize, Vector2 mainSize, float slotSize, float handleWidth)
    {
        EnsureFolder(UIFolder);
        int tw = Mathf.RoundToInt(toolbarSize.x * 2), th = Mathf.RoundToInt(toolbarSize.y * 2);
        int mw = Mathf.RoundToInt(mainSize.x * 2), mh = Mathf.RoundToInt(mainSize.y * 2);
        int ss = Mathf.RoundToInt(slotSize * 2), hw = Mathf.RoundToInt(handleWidth * 2);

        SaveTexture(UIFolder, "Toolbar", tw, th, StoneTextures.Panel(tw, th, 11, false), true);
        SaveTexture(UIFolder, "Main Panel", mw, mh, StoneTextures.Panel(mw, mh, 12, true), true);
        SaveTexture(UIFolder, "Slot", ss, ss, StoneTextures.Slot(ss, 21, false), true);
        SaveTexture(UIFolder, "Slot Selected", ss, ss, StoneTextures.Slot(ss, 21, true), true);
        var handle = StoneTextures.Handle(hw, 31, out int hh);
        SaveTexture(UIFolder, "Handle", hw, hh, handle, true);
    }

    public static Sprite LoadUISprite(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(UIFolder + "/" + name + ".png");

    // ------------------------------------------------------------------ 3D bag

    [MenuItem("Tools/Inventory 3D/Add Stone Bag To Selected Character", priority = 3)]
    public static void AddBagToSelected()
    {
        var target = Selection.activeTransform;
        if (target == null)
        {
            EditorUtility.DisplayDialog("Add Stone Bag", "Select your character (or its hand bone) in the Hierarchy first.", "OK");
            return;
        }
        EnsureBagPrefab();
        var pivot = AddBagToCharacter(target, new Vector3(-0.5f, 0.95f, 0f));
        Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Add Stone Bag");
        Selection.activeGameObject = pivot.gameObject;
        Debug.Log("Stone bag added. Move \"Bag Pivot\" to where the character's hand is (or drag it onto the hand bone).");
    }

    /// <summary>Adds a swinging stone bag hanging from a pivot at localHandPosition.</summary>
    public static Transform AddBagToCharacter(Transform character, Vector3 localHandPosition)
    {
        var pivot = new GameObject("Bag Pivot").transform;
        pivot.SetParent(character, false);
        pivot.localPosition = localHandPosition;
        pivot.gameObject.AddComponent<BagSway>().character = character;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagPrefabPath);
        var bag = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
        bag.transform.localPosition = new Vector3(0f, -BagHeight, 0f); // handle top sits in the hand
        bag.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // flat side faces outward
        return pivot;
    }

    public static void EnsureBagPrefab()
    {
        EnsureFolder(BagFolder);

        // Carved texture, mapped across the bag's faces
        float archCenterV = BagSideHeight / BagHeight;
        float archRadiusU = (BagWidth * 0.5f - BagHandleBand * 0.5f) / BagWidth;
        float archRadiusV = (BagArchHeight - BagHandleBand * 0.5f) / BagHeight;
        SaveTexture(BagFolder, "Stone Bag Carving", 512, 512,
                    StoneTextures.BagAlbedo(512, 41, archCenterV, archRadiusU, archRadiusV), false);

        // Material: copy of the render pipeline's default (URP Lit here)
        string matPath = BagFolder + "/Stone Bag.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(temp);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.color = Color.white;
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BagFolder + "/Stone Bag Carving.png");
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.18f);
        EditorUtility.SetDirty(mat);

        // Mesh
        string meshPath = BagFolder + "/Stone Bag Mesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(BuildBagMesh(), meshPath);
        AssetDatabase.SaveAssets();

        // Prefab
        var go = new GameObject("Stone Bag");
        go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        PrefabUtility.SaveAsPrefabAsset(go, BagPrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Bell-shaped slab (flat bottom, straight sides, rounded top) with a handle opening through it.
    /// Both outlines are convex around a point inside the opening, so the face is built as a ring
    /// of quads between them, then extruded with rims on the outside and inside the handle.
    /// </summary>
    static Mesh BuildBagMesh()
    {
        const int N = 120;
        Vector2 center = new Vector2(0f, (BagHoleBottom + BagSideHeight + BagArchHeight - BagHandleBand) * 0.5f);

        var outer = new List<Vector2>();
        outer.Add(new Vector2(-BagWidth * 0.5f, 0f));
        outer.Add(new Vector2(BagWidth * 0.5f, 0f));
        AddArc(outer, BagWidth * 0.5f, BagArchHeight, BagSideHeight, 64);   // includes side tops

        float ia = BagWidth * 0.5f - BagHandleBand, ib = BagArchHeight - BagHandleBand;
        var hole = new List<Vector2>();
        float t = (BagHoleBottom - BagSideHeight) / ib;
        float startAngle = Mathf.Asin(Mathf.Clamp(t, -1f, 1f));
        for (int i = 0; i <= 64; i++)
        {
            float ang = Mathf.Lerp(startAngle, Mathf.PI - startAngle, i / 64f);
            hole.Add(new Vector2(Mathf.Cos(ang) * ia, BagSideHeight + Mathf.Sin(ang) * ib));
        }

        var o = new Vector2[N];
        var h = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            float ang = i * Mathf.PI * 2f / N;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            o[i] = RayHit(outer, center, dir);
            h[i] = RayHit(hole, center, dir);
        }

        var mb = new MeshBuilder();
        float z = BagThickness * 0.5f;
        var front = new int[N, 2];
        var back = new int[N, 2];
        for (int i = 0; i < N; i++)
        {
            front[i, 0] = mb.Vertex(new Vector3(h[i].x, h[i].y, z), FaceUV(h[i], false));
            front[i, 1] = mb.Vertex(new Vector3(o[i].x, o[i].y, z), FaceUV(o[i], false));
            back[i, 0] = mb.Vertex(new Vector3(h[i].x, h[i].y, -z), FaceUV(h[i], true));
            back[i, 1] = mb.Vertex(new Vector3(o[i].x, o[i].y, -z), FaceUV(o[i], true));
        }
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            mb.Quad(front[i, 0], front[i, 1], front[j, 1], front[j, 0], Vector3.forward);
            mb.Quad(back[i, 0], back[i, 1], back[j, 1], back[j, 0], Vector3.back);
        }

        // Rims (own vertices so the face edges stay sharp)
        var rimOutF = new int[N + 1];
        var rimOutB = new int[N + 1];
        var rimInF = new int[N + 1];
        var rimInB = new int[N + 1];
        for (int k = 0; k <= N; k++)
        {
            int i = k % N;
            float u = k / (float)N * 4f;
            rimOutF[k] = mb.Vertex(new Vector3(o[i].x, o[i].y, z), new Vector2(u, 0.15f));
            rimOutB[k] = mb.Vertex(new Vector3(o[i].x, o[i].y, -z), new Vector2(u, 0f));
            rimInF[k] = mb.Vertex(new Vector3(h[i].x, h[i].y, z), new Vector2(u, 0.15f));
            rimInB[k] = mb.Vertex(new Vector3(h[i].x, h[i].y, -z), new Vector2(u, 0f));
        }
        for (int k = 0; k < N; k++)
        {
            int i = k % N, j = (k + 1) % N;
            Vector2 mo = (o[i] + o[j]) * 0.5f - center;
            Vector2 mh = center - (h[i] + h[j]) * 0.5f;
            mb.Quad(rimOutF[k], rimOutB[k], rimOutB[k + 1], rimOutF[k + 1], new Vector3(mo.x, mo.y, 0f));
            mb.Quad(rimInF[k], rimInB[k], rimInB[k + 1], rimInF[k + 1], new Vector3(mh.x, mh.y, 0f));
        }

        var mesh = mb.ToMesh();
        mesh.name = "Stone Bag";
        return mesh;
    }

    static Vector2 FaceUV(Vector2 p, bool mirrored)
    {
        float u = p.x / BagWidth + 0.5f;
        return new Vector2(mirrored ? 1f - u : u, p.y / BagHeight);
    }

    /// <summary>Upper half of an ellipse from the right side round to the left, raised by yOffset.</summary>
    static void AddArc(List<Vector2> pts, float a, float b, float yOffset, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float ang = Mathf.PI * i / segments;
            pts.Add(new Vector2(Mathf.Cos(ang) * a, yOffset + Mathf.Sin(ang) * b));
        }
    }

    /// <summary>Where a ray from origin first crosses the closed polygon.</summary>
    static Vector2 RayHit(List<Vector2> poly, Vector2 origin, Vector2 dir)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
            Vector2 e = b - a;
            float denom = dir.x * e.y - dir.y * e.x;
            if (Mathf.Abs(denom) < 1e-8f) continue;
            Vector2 ao = a - origin;
            float tRay = (ao.x * e.y - ao.y * e.x) / denom;
            float s = (ao.x * dir.y - ao.y * dir.x) / denom;
            if (tRay > 0f && s >= 0f && s <= 1f && tRay < best) best = tRay;
        }
        return origin + dir * (best == float.MaxValue ? 0f : best);
    }

    class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public int Vertex(Vector3 p, Vector2 uv)
        {
            vertices.Add(p);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        /// <summary>Adds a triangle, flipping its winding if needed so it faces 'outward'.</summary>
        void Triangle(int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(n, outward) < 0f) { int tmp = b; b = c; c = tmp; }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        public void Quad(int a, int b, int c, int d, Vector3 outward)
        {
            Triangle(a, b, c, outward);
            Triangle(a, c, d, outward);
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ------------------------------------------------------------------ helpers

    static void SaveTexture(string folder, string name, int w, int h, Color32[] pixels, bool sprite)
    {
        string path = folder + "/" + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
        else
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
        }
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
