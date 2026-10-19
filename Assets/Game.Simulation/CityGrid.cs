namespace AdventurerCity.Simulation
{
    public readonly struct CellRect
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Depth;

        public CellRect(int x, int y, int width, int depth)
        {
            X = x;
            Y = y;
            Width = width;
            Depth = depth;
        }

        public int XMax => X + Width;
        public int YMax => Y + Depth;

        public bool Contains(int x, int y) => x >= X && x < XMax && y >= Y && y < YMax;
    }

    public sealed class CityGrid
    {
        public const int Empty = 0;
        public const int Road = -1;

        readonly int[] cells;

        public CityGrid(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new int[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public int this[int x, int y] => cells[Index(x, y)];

        public int Index(int x, int y) => y * Width + x;

        public int GetCell(int index) => cells[index];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsRoad(int x, int y) => InBounds(x, y) && this[x, y] == Road;

        public bool IsFree(CellRect rect)
        {
            if (rect.X < 0 || rect.Y < 0 || rect.XMax > Width || rect.YMax > Height) return false;
            for (var y = rect.Y; y < rect.YMax; y++)
            for (var x = rect.X; x < rect.XMax; x++)
                if (this[x, y] != Empty)
                    return false;
            return true;
        }

        internal void SetCell(int index, int value) => cells[index] = value;

        internal void Fill(CellRect rect, int value)
        {
            for (var y = rect.Y; y < rect.YMax; y++)
            for (var x = rect.X; x < rect.XMax; x++)
                cells[Index(x, y)] = value;
        }
    }
}
