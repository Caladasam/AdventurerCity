using AdventurerCity.Simulation;
using UnityEngine;

namespace AdventurerCity.View
{
    public static class GridMath
    {
        public const float CellSize = 1f;

        public static Vector3 CellCorner(int x, int y) => new Vector3(x * CellSize, y * CellSize, 0f);

        public static Vector3 CellCenter(int x, int y) => new Vector3((x + 0.5f) * CellSize, (y + 0.5f) * CellSize, 0f);

        public static Vector3 RectCenter(CellRect rect) =>
            new Vector3((rect.X + rect.Width * 0.5f) * CellSize, (rect.Y + rect.Depth * 0.5f) * CellSize, 0f);

        public static Vector2Int WorldToCell(Vector3 point) =>
            new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
    }

    public static class Palette
    {
        public static readonly Color Background = new Color(0.16f, 0.22f, 0.16f);
        public static readonly Color GhostOk = new Color(0.35f, 0.95f, 0.45f, 0.45f);
        public static readonly Color GhostBad = new Color(0.95f, 0.30f, 0.25f, 0.45f);
        public static readonly Color Idle = new Color(0.62f, 0.62f, 0.66f);

        public static Color Building(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Storehouse: return new Color(0.30f, 0.46f, 0.82f);
                case BuildingKind.House: return new Color(0.78f, 0.36f, 0.26f);
                case BuildingKind.Woodcutter: return new Color(0.24f, 0.46f, 0.24f);
                case BuildingKind.Farm: return new Color(0.92f, 0.78f, 0.30f);
                case BuildingKind.Sawmill: return new Color(0.88f, 0.52f, 0.20f);
                case BuildingKind.Bakery: return new Color(0.86f, 0.30f, 0.40f);
                case BuildingKind.Guild: return new Color(0.56f, 0.32f, 0.80f);
                case BuildingKind.Forge: return new Color(0.40f, 0.42f, 0.50f);
                case BuildingKind.Watchtower: return new Color(0.58f, 0.40f, 0.26f);
                case BuildingKind.Temple: return new Color(0.92f, 0.86f, 0.62f);
                case BuildingKind.Tavern: return new Color(0.62f, 0.30f, 0.18f);
                case BuildingKind.Market: return new Color(0.22f, 0.55f, 0.52f);
                case BuildingKind.TownHall: return new Color(0.80f, 0.62f, 0.22f);
                case BuildingKind.Barracks: return new Color(0.50f, 0.20f, 0.20f);
                case BuildingKind.TrainingYard: return new Color(0.62f, 0.52f, 0.36f);
                default: return Color.magenta;
            }
        }
    }
}
