using System;
using UnityEngine;

/// <summary>
/// Pixel generators for the carved-stone look (inspired by the stone bags restored from Qazvin).
/// Pure math, no assets: every function returns a Color32[] (row 0 = bottom) ready for a Texture2D.
/// </summary>
public static class StoneTextures
{
    static readonly Vector2 LightDir = new Vector2(-0.55f, 0.83f); // light from the top-left
    public static readonly Color Turquoise = new Color(0.30f, 0.82f, 0.78f);

    // ------------------------------------------------------------------ stone material

    static float Fbm(float x, float y, int seed)
    {
        float sum = 0f, amp = 0.5f, freq = 1f;
        for (int i = 0; i < 4; i++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq + seed * 13.17f, y * freq + seed * 7.73f);
            freq *= 2f;
            amp *= 0.5f;
        }
        return sum / 0.9375f;
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>Dark grey-green stone like the museum pieces, with mottling and mineral specks.</summary>
    public static Color StoneColor(float x, float y, int seed)
    {
        float n = Fbm(x * 0.022f, y * 0.022f, seed);
        float fine = Mathf.PerlinNoise(x * 0.31f + seed * 3.1f, y * 0.31f - seed * 1.7f);
        Color c = Color.Lerp(new Color(0.20f, 0.22f, 0.21f), new Color(0.42f, 0.44f, 0.42f), n);
        c = Mul(c, 0.9f + 0.2f * fine);
        float speck = Hash((int)x, (int)y, seed);
        if (speck > 0.985f) c = Mul(c, 1.35f);
        else if (speck < 0.012f) c = Mul(c, 0.7f);
        return c;
    }

    static Color Mul(Color c, float f)
    {
        return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }

    // ------------------------------------------------------------------ shapes (signed distance, < 0 = inside)

    public static float RoundRect(float px, float py, float w, float h, float margin, float radius)
    {
        float hx = w * 0.5f - margin, hy = h * 0.5f - margin;
        float qx = Mathf.Abs(px - w * 0.5f) - hx + radius;
        float qy = Mathf.Abs(py - h * 0.5f) - hy + radius;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    // ------------------------------------------------------------------ generic carved-stone renderer

    /// <param name="raised">true = slab sticking out (panels); false = carved hollow (slots)</param>
    /// <param name="groove">distance from the edge of an engraved border line (0 = none)</param>
    /// <param name="rim">optional glowing inlay color around the edge (selected slot)</param>
    public static Color32[] RenderStone(int w, int h, Func<float, float, float> sd, int seed,
                                        float bevel, bool raised, float groove, float darken,
                                        Color? rim, float rimWidth, Vector2[] rosettes, float rosetteRadius)
    {
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float d = sd(fx, fy);
                float alpha = Mathf.Clamp01(0.5f - d);
                if (alpha <= 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }

                float inside = -d;
                Color c = Mul(StoneColor(fx, fy, seed), darken);

                // Bevel: edges facing the light are brighter (reversed for carved hollows)
                if (inside < bevel)
                {
                    float gx = sd(fx + 1f, fy) - sd(fx - 1f, fy);
                    float gy = sd(fx, fy + 1f) - sd(fx, fy - 1f);
                    float len = Mathf.Sqrt(gx * gx + gy * gy) + 1e-5f;
                    float facing = (gx * LightDir.x + gy * LightDir.y) / len;
                    float t = 1f - inside / bevel;
                    c = Mul(c, 1f + 0.5f * (raised ? facing : -facing) * t);
                }

                // Dark outline at the very edge
                if (inside < 1.6f) c = Mul(c, 0.5f);

                // Engraved border line with a soft highlight on its lower lip
                if (groove > 0f)
                {
                    float g = inside - groove;
                    if (Mathf.Abs(g) < 1.3f) c = Mul(c, 0.48f);
                    else if (g >= 1.3f && g < 2.8f) c = Mul(c, 1.16f);
                }

                // Carved rosettes (small engraved rings with a center dot)
                if (rosettes != null)
                {
                    foreach (var r in rosettes)
                    {
                        float dx = fx - r.x, dy = fy - r.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (Mathf.Abs(dist - rosetteRadius) < 1.3f || dist < 2.2f) c = Mul(c, 0.48f);
                        else if (Mathf.Abs(dist - rosetteRadius * 0.55f) < 1f) c = Mul(c, 0.7f);
                    }
                }

                // Turquoise inlay glow
                if (rim.HasValue && inside < rimWidth)
                {
                    float t = 1f - inside / rimWidth;
                    c = Color.Lerp(c, rim.Value, Mathf.Clamp01(t * t * 1.1f));
                }

                c.a = alpha;
                px[y * w + x] = c;
            }
        }
        return px;
    }

    // ------------------------------------------------------------------ specific UI pieces

    public static Color32[] Panel(int w, int h, int seed, bool withRosettes)
    {
        float margin = 2f, radius = Mathf.Min(22f, h * 0.3f), groove = Mathf.Min(15f, h * 0.18f);
        Vector2[] ros = null;
        if (withRosettes)
        {
            float inset = groove + 13f;
            ros = new[] { new Vector2(inset, inset), new Vector2(w - inset, inset),
                          new Vector2(inset, h - inset), new Vector2(w - inset, h - inset) };
        }
        return RenderStone(w, h, (x, y) => RoundRect(x, y, w, h, margin, radius), seed,
                           13f, true, groove, 1f, null, 0f, ros, 7f);
    }

    public static Color32[] Slot(int size, int seed, bool selected)
    {
        return RenderStone(size, size, (x, y) => RoundRect(x, y, size, size, 2f, 14f), seed,
                           12f, false, 0f, 0.72f, selected ? Turquoise : (Color?)null, 18f, null, 0f);
    }

    /// <summary>Half-ring like the bags' carved handle. Width w; height is returned.</summary>
    public static Color32[] Handle(int w, int seed, out int h)
    {
        float cx = w * 0.5f, cy = 4f;
        float ro = w * 0.5f - 4f, ri = ro * 0.64f;
        float rm = (ro + ri) * 0.5f, th = (ro - ri) * 0.5f;
        h = Mathf.CeilToInt(cy + ro + 4f);
        Func<float, float, float> sd = (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            float ring = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - rm) - th;
            return Mathf.Max(ring, cy - y);
        };
        return RenderStone(w, h, sd, seed, 12f, true, th * 0.42f, 0.95f, null, 0f, null, 0f);
    }

    // ------------------------------------------------------------------ 3D bag albedo

    /// <summary>
    /// Square texture mapped onto the bag's faces (u across the width, v up the height).
    /// Carvings: a band of diamonds, framing lines, and an engraved arch with dots following the handle.
    /// </summary>
    public static Color32[] BagAlbedo(int size, int seed, float archCenterV, float archRadiusU, float archRadiusV)
    {
        var px = new Color32[size * size];
        float s = size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                float dMin = 1e9f;

                bool inBand = u > 0.06f && u < 0.94f;
                if (inBand)
                {
                    dMin = Mathf.Min(dMin, Mathf.Abs(v - 0.10f) * s);
                    dMin = Mathf.Min(dMin, Mathf.Abs(v - 0.44f) * s);

                    // zig-zag forming a row of diamonds
                    float f = u * 7f - Mathf.Floor(u * 7f);
                    float tri = Mathf.Abs(f - 0.5f) * 2f;
                    float vz1 = 0.13f + 0.28f * tri;
                    float vz2 = 0.41f - 0.28f * tri;
                    const float slopeFix = 4.05f;
                    dMin = Mathf.Min(dMin, Mathf.Abs(v - vz1) * s / slopeFix);
                    dMin = Mathf.Min(dMin, Mathf.Abs(v - vz2) * s / slopeFix);

                    // dot in the middle of each diamond
                    float cu = (Mathf.Floor(u * 7f) + 0.5f) / 7f;
                    float dd = Mathf.Sqrt((u - cu) * (u - cu) + (v - 0.27f) * (v - 0.27f));
                    dMin = Mathf.Min(dMin, Mathf.Abs(dd - 0.012f) * s);
                }

                // side frame lines
                if (v > 0.10f && v < 0.44f)
                {
                    dMin = Mathf.Min(dMin, Mathf.Abs(u - 0.06f) * s);
                    dMin = Mathf.Min(dMin, Mathf.Abs(u - 0.94f) * s);
                }

                // engraved arch following the handle, two lines with dots between
                if (v >= archCenterV)
                {
                    float ex = (u - 0.5f) / archRadiusU, ey = (v - archCenterV) / archRadiusV;
                    float e = Mathf.Sqrt(ex * ex + ey * ey);
                    float scale = Mathf.Min(archRadiusU, archRadiusV) * s;
                    dMin = Mathf.Min(dMin, Mathf.Abs(e - 0.955f) * scale);
                    dMin = Mathf.Min(dMin, Mathf.Abs(e - 1.045f) * scale);

                    float ang = Mathf.Atan2(ey, ex);
                    float step = Mathf.PI / 12f;
                    float nearest = Mathf.Round(ang / step) * step;
                    float dotU = 0.5f + Mathf.Cos(nearest) * archRadiusU;
                    float dotV = archCenterV + Mathf.Sin(nearest) * archRadiusV;
                    float dd = Mathf.Sqrt((u - dotU) * (u - dotU) + (v - dotV) * (v - dotV)) * s;
                    if (dd < 2.6f) dMin = 0f;
                }

                Color c = Mul(StoneColor(x * 1.6f, y * 1.6f, seed), 0.88f);
                if (dMin < 1.7f) c = Mul(c, 0.42f);          // carved groove
                else if (dMin < 3.4f) c = Mul(c, 0.8f);      // soft shadow at the groove edge
                px[y * size + x] = c;
            }
        }
        return px;
    }
}
