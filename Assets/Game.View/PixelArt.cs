using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class PixelArt
    {
        public const int PixelsPerUnit = 16;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Color32 Outline = new Color32(28, 22, 26, 255);
        static Material litMaterial;

        public static Material LitMaterial
        {
            get
            {
                if (litMaterial != null) return litMaterial;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default") ?? Shader.Find("Sprites/Default");
                litMaterial = new Material(shader);
                return litMaterial;
            }
        }

        public static SpriteRenderer Create(string name, Transform parent, Sprite sprite, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = LitMaterial;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static int SortOrder(float y) => -Mathf.RoundToInt(y * 20f);

        public static Sprite Grass() => Cached("grass", () =>
        {
            var c = new Canvas(16, 16);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var n = Hash(x, y, 1);
                var shade = n < 0.15f ? new Color32(74, 118, 58, 255) : n > 0.85f ? new Color32(104, 152, 72, 255) : new Color32(88, 136, 64, 255);
                c.Set(x, y, shade);
            }
            c.Set(3, 11, new Color32(232, 214, 96, 255));
            c.Set(12, 4, new Color32(222, 128, 152, 255));
            c.Set(9, 13, new Color32(118, 166, 82, 255));
            return c.ToSprite(new Vector2(0.5f, 0.5f), true);
        });

        public static Sprite Road() => Cached("road", () =>
        {
            var c = new Canvas(16, 16);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var n = Hash(x, y, 2);
                c.Set(x, y, n < 0.2f ? new Color32(150, 124, 92, 255) : n > 0.9f ? new Color32(186, 164, 126, 255) : new Color32(168, 142, 106, 255));
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f), true);
        });

        public static Sprite Stone(Color32 baseColor) => Cached("stone" + baseColor, () =>
        {
            var c = new Canvas(16, 16);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var seam = x % 8 == 0 || y % 8 == 0 && (x / 8 + y / 8) % 2 == 0;
                c.Set(x, y, seam ? Mul(baseColor, 0.7f) : Mul(baseColor, 0.9f + 0.2f * Hash(x, y, 3)));
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f), true);
        });

        public static Sprite Square() => Cached("square", () =>
        {
            var c = new Canvas(4, 4);
            c.Fill(0, 0, 4, 4, new Color32(255, 255, 255, 255));
            return c.ToSprite(new Vector2(0.5f, 0.5f), false, 4);
        });

        public static Sprite Dot() => Cached("dot", () =>
        {
            var c = new Canvas(8, 8);
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(4f, 4f)) / 4f;
                var a = (byte)Mathf.Clamp(255f * (1f - d * d), 0f, 255f);
                c.Set(x, y, new Color32(255, 255, 255, a));
            }
            return c.ToSprite(new Vector2(0.5f, 0.5f), false, 8);
        });

        public static Sprite Tree(int variant) => Cached("tree" + variant, () =>
        {
            var c = new Canvas(16, 22);
            c.Fill(7, 0, 2, 7, new Color32(96, 64, 40, 255));
            var leaf = variant == 0 ? new Color32(46, 104, 52, 255) : new Color32(60, 118, 46, 255);
            for (var y = 5; y < 22; y++)
            for (var x = 0; x < 16; x++)
            {
                var d = new Vector2((x - 7.5f) / 7.5f, (y - 13f) / 8.5f).sqrMagnitude;
                if (d > 1f) continue;
                var light = (x + y * 0.5f) / 24f + Hash(x, y, 4) * 0.25f;
                c.Set(x, y, d > 0.82f ? Outline : Mul(leaf, 0.8f + light * 0.5f));
            }
            return c.ToSprite(new Vector2(0.5f, 0f), false);
        });

        public static int WallPixels(BuildingKind kind, int level)
        {
            switch (kind)
            {
                case BuildingKind.Farm: return 2;
                case BuildingKind.House: return 6 + level * 3;
                case BuildingKind.Watchtower: return 30;
                case BuildingKind.Guild: return 16;
                case BuildingKind.Storehouse: return 12;
                default: return 10;
            }
        }

        public static bool HasChimney(BuildingKind kind, int level) =>
            kind == BuildingKind.Bakery || kind == BuildingKind.Forge || kind == BuildingKind.House && level >= 2;

        public static Sprite Building(BuildingKind kind, int width, int depth, int level, Color roofColor)
        {
            var key = $"b{kind}{width}x{depth}l{level}";
            return Cached(key, () =>
            {
                var wall = WallPixels(kind, level);
                var w = width * PixelsPerUnit;
                var h = depth * PixelsPerUnit + wall;
                var c = new Canvas(w, h);
                var roof = (Color32)roofColor;

                if (kind == BuildingKind.Farm)
                {
                    for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var row = y % 4 < 2;
                        c.Set(x, y, row ? new Color32(176, 132, 72, 255) : Mul(roof, 0.75f + 0.25f * Hash(x, y, 5)));
                    }
                    c.Fill(1, h - 9, 6, 8, new Color32(150, 92, 56, 255));
                    c.Fill(1, h - 3, 6, 2, new Color32(120, 70, 44, 255));
                    c.Border(0, 0, w, h, Mul(roof, 0.45f));
                    return c.ToSprite(new Vector2(0f, 0f), false);
                }

                var wallColor = kind == BuildingKind.Forge || kind == BuildingKind.Watchtower || kind == BuildingKind.Storehouse
                    ? new Color32(150, 146, 140, 255)
                    : new Color32(214, 190, 150, 255);
                for (var y = 0; y < wall; y++)
                for (var x = 0; x < w; x++)
                {
                    var plank = kind == BuildingKind.Forge || kind == BuildingKind.Watchtower || kind == BuildingKind.Storehouse
                        ? (x / 4 + y / 3) % 2 == 0 ? 0.95f : 0.85f
                        : x % 5 == 0 ? 0.8f : 1f;
                    c.Set(x, y, Mul(wallColor, plank));
                }

                var door = Mathf.Min(6, w / 3);
                c.Fill(w / 2 - door / 2, 0, door, Mathf.Min(wall - 1, 8), new Color32(92, 58, 38, 255));
                for (var x = 4; x + 4 < w; x += 12)
                {
                    if (Mathf.Abs(x + 2 - w / 2) < door) continue;
                    if (wall >= 8) c.Fill(x, wall - 7, 4, 4, new Color32(250, 214, 120, 255));
                }

                for (var y = wall; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var ridge = Mathf.Abs(y - (wall + (h - wall) / 2));
                    var tile = (y - wall) % 4 == 0 ? 0.78f : 1f;
                    var slope = y > wall + (h - wall) / 2 ? 0.88f : 1.06f;
                    c.Set(x, y, ridge == 0 ? Mul(roof, 0.65f) : Mul(roof, tile * slope));
                }

                if (kind == BuildingKind.Guild)
                {
                    c.Fill(w / 2 - 1, h - 4, 2, 4, Outline);
                    c.Fill(w / 2 + 1, h - 8, 7, 5, new Color32(232, 196, 72, 255));
                }
                if (kind == BuildingKind.Storehouse)
                    for (var x = 3; x + 6 < w; x += 9)
                        c.Fill(x, 1, 6, 5, new Color32(170, 120, 70, 255));
                if (kind == BuildingKind.Watchtower)
                    for (var x = 0; x < w; x += 4)
                        c.Fill(x, h - 3, 2, 3, Outline);
                if (HasChimney(kind, level))
                {
                    var cx = w - 7;
                    c.Fill(cx, h - 10, 4, 8, new Color32(110, 96, 90, 255));
                    c.Fill(cx, h - 3, 4, 1, Outline);
                }

                c.Border(0, 0, w, h, Outline);
                c.Line(0, wall, w, Outline);
                return c.ToSprite(new Vector2(0f, 0f), false);
            });
        }

        public static Sprite Scaffold(int width, int depth) => Cached($"scaffold{width}x{depth}", () =>
        {
            var w = width * PixelsPerUnit;
            var h = depth * PixelsPerUnit;
            var c = new Canvas(w, h);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                c.Set(x, y, Mul(new Color32(176, 150, 112, 255), 0.85f + 0.15f * Hash(x, y, 6)));
            for (var x = 0; x < w; x += 8) c.Fill(x, 0, 2, h, new Color32(128, 92, 58, 255));
            for (var y = 0; y < h; y += 8) c.Line(0, y, w, new Color32(128, 92, 58, 255));
            c.Border(0, 0, w, h, Outline);
            return c.ToSprite(new Vector2(0f, 0f), false);
        });

        public static Sprite Figure(bool hero, bool beast) => Cached($"fig{hero}{beast}", () =>
        {
            if (beast)
            {
                var b = new Canvas(20, 12);
                b.Fill(3, 4, 13, 6, new Color32(255, 255, 255, 255));
                b.Fill(14, 6, 5, 5, new Color32(255, 255, 255, 255));
                b.Fill(4, 0, 2, 4, new Color32(220, 220, 220, 255));
                b.Fill(13, 0, 2, 4, new Color32(220, 220, 220, 255));
                b.Fill(0, 7, 3, 2, new Color32(220, 220, 220, 255));
                b.Set(17, 8, new Color32(200, 40, 40, 255));
                b.Outline(Outline);
                return b.ToSprite(new Vector2(0.5f, 0f), false);
            }

            var c = new Canvas(12, 18);
            c.Fill(4, 0, 2, 4, new Color32(200, 200, 200, 255));
            c.Fill(7, 0, 2, 4, new Color32(200, 200, 200, 255));
            c.Fill(2, 4, 8, 8, new Color32(255, 255, 255, 255));
            c.Fill(1, 6, 1, 5, new Color32(230, 230, 230, 255));
            c.Fill(10, 6, 1, 5, new Color32(230, 230, 230, 255));
            if (hero)
            {
                c.Fill(3, 12, 6, 5, new Color32(240, 200, 160, 255));
                c.Set(4, 14, Outline);
                c.Set(7, 14, Outline);
            }
            else
            {
                c.Fill(3, 12, 6, 5, new Color32(210, 210, 210, 255));
                c.Set(4, 14, new Color32(220, 40, 40, 255));
                c.Set(7, 14, new Color32(220, 40, 40, 255));
            }
            c.Outline(Outline);
            return c.ToSprite(new Vector2(0.5f, 0f), false);
        });

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            sprite = make();
            cache[key] = sprite;
            return sprite;
        }

        static float Hash(int x, int y, int seed)
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xFFFF) / 65535f;
        }

        static Color32 Mul(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        sealed class Canvas
        {
            readonly int width;
            readonly int height;
            readonly Color32[] pixels;

            public Canvas(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color32[width * height];
            }

            public void Set(int x, int y, Color32 color)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                pixels[y * width + x] = color;
            }

            public void Fill(int x0, int y0, int w, int h, Color32 color)
            {
                for (var y = y0; y < y0 + h; y++)
                for (var x = x0; x < x0 + w; x++)
                    Set(x, y, color);
            }

            public void Line(int x0, int y, int w, Color32 color) => Fill(x0, y, w, 1, color);

            public void Border(int x0, int y0, int w, int h, Color32 color)
            {
                Fill(x0, y0, w, 1, color);
                Fill(x0, y0 + h - 1, w, 1, color);
                Fill(x0, y0, 1, h, color);
                Fill(x0 + w - 1, y0, 1, h, color);
            }

            public void Outline(Color32 color)
            {
                var copy = (Color32[])pixels.Clone();
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (copy[y * width + x].a != 0) continue;
                    var near = false;
                    for (var dy = -1; dy <= 1 && !near; dy++)
                    for (var dx = -1; dx <= 1 && !near; dx++)
                    {
                        if (dx != 0 && dy != 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (copy[ny * width + nx].a != 0 && copy[ny * width + nx].r + copy[ny * width + nx].g > 0) near = true;
                    }
                    if (near) pixels[y * width + x] = color;
                }
            }

            public Sprite ToSprite(Vector2 pivot, bool tiled, int ppu = PixelsPerUnit)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp
                };
                texture.SetPixels32(pixels);
                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, ppu, 0, SpriteMeshType.FullRect);
            }
        }
    }
}
