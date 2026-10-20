using System.Collections.Generic;
using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class GridMath
    {
        public const float CellSize = 2f;

        public static Vector3 CellCenter(int x, int y) => new Vector3((x + 0.5f) * CellSize, 0f, (y + 0.5f) * CellSize);

        public static Vector3 RectCenter(CellRect rect) =>
            new Vector3((rect.X + rect.Width * 0.5f) * CellSize, 0f, (rect.Y + rect.Depth * 0.5f) * CellSize);

        public static Vector2Int WorldToCell(Vector3 point) =>
            new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.z / CellSize));
    }

    public static class Palette
    {
        public static readonly Color Ground = new Color(0.38f, 0.55f, 0.30f);
        public static readonly Color Road = new Color(0.58f, 0.52f, 0.42f);
        public static readonly Color Scaffold = new Color(0.72f, 0.70f, 0.66f);
        public static readonly Color GhostOk = new Color(0.35f, 0.85f, 0.35f);
        public static readonly Color GhostBad = new Color(0.90f, 0.30f, 0.25f);
        public static readonly Color Sky = new Color(0.62f, 0.76f, 0.88f);
        public static readonly Color Idle = new Color(0.35f, 0.35f, 0.35f);

        public static Color Building(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Storehouse: return new Color(0.22f, 0.42f, 0.86f);
                case BuildingKind.House: return new Color(0.96f, 0.90f, 0.78f);
                case BuildingKind.Woodcutter: return new Color(0.08f, 0.30f, 0.10f);
                case BuildingKind.Farm: return new Color(0.98f, 0.82f, 0.12f);
                case BuildingKind.Sawmill: return new Color(0.88f, 0.45f, 0.10f);
                case BuildingKind.Bakery: return new Color(0.86f, 0.18f, 0.32f);
                case BuildingKind.Guild: return new Color(0.52f, 0.26f, 0.78f);
                case BuildingKind.Forge: return new Color(0.36f, 0.40f, 0.48f);
                default: return Color.magenta;
            }
        }
    }

    public static class Materials
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader shader;

        public static Material Get(Color color)
        {
            if (cache.TryGetValue(color, out var material)) return material;
            if (shader == null)
                shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { color = color };
            cache.Add(color, material);
            return material;
        }

        public static GameObject CreateCube(string name, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            return cube;
        }
    }
}
