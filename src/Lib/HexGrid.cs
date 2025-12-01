using System.Collections.Generic;
using Godot;

public static class HexGrid
{
    public const float HEX_SIZE = 1.05f;

    public static readonly Dictionary<string, Vector3I> Directions = new()
        {
            { "NorthWest", new Vector3I(-1, 0, 1) },
            { "North", new Vector3I(0, -1, 1) },
            { "NorthEast", new Vector3I(1, -1, 0) },
            { "SouthWest", new Vector3I(-1, 1, 0) },
            { "South", new Vector3I(0, 1, -1) },
            { "SouthEast", new Vector3I(1, 0, -1) }
        };

    public static IEnumerable<Vector3I> GenerateHexCoordinates(int mapSize)
    {
        var coords = new List<Vector3I>();
        for (int q = -mapSize; q <= mapSize; q++)
        {
            int r1 = Mathf.Max(-mapSize, -q - mapSize);
            int r2 = Mathf.Min(mapSize, -q + mapSize);
            for (int r = r1; r <= r2; r++)
            {
                int s = -q - r;
                coords.Add(new Vector3I(q, r, s));
            }
        }

        coords.Sort((a, b) =>
            {
                if (a.Y != b.Y)
                    return a.Y.CompareTo(b.Y);  // Changed to ascending Y
                return a.X.CompareTo(b.X);      // Changed to ascending X
            });

        return coords;
    }

    public static Vector3 HexToWorld(Vector3I hexCoord)
    {
        float x = HEX_SIZE * (1.5f * hexCoord.X);
        float z = HEX_SIZE * (Mathf.Sqrt(3.0f) * (hexCoord.Y + hexCoord.X * 0.5f));
        return new Vector3(x, 0, z);
    }

    public static Vector3I WorldToHex(Vector3 worldPos)
    {
        float q = (2.0f / 3.0f * worldPos.X) / HEX_SIZE;
        float r = (-1.0f / 3.0f * worldPos.X + Mathf.Sqrt(3.0f) / 3.0f * worldPos.Z) / HEX_SIZE;
        float s = -q - r;
        return RoundToHex(new Vector3(q, r, s));
    }

    public static IEnumerable<Vector3I> GetHexesInRange(Vector3I center, int range)
    {
        // Use yield return for deferred execution - no list allocation
        for (int q = -range; q <= range; q++)
        {
            int r1 = Mathf.Max(-range, -q - range);
            int r2 = Mathf.Min(range, -q + range);

            for (int r = r1; r <= r2; r++)
            {
                var s = -q - r;
                var coord = new Vector3I(center.X + q, center.Y + r, center.Z + s);
                var distance = GetDistance(center, coord);

                // Check if this coordinate is within range and is a valid tile
                if (distance <= range)
                {
                    yield return coord;
                }
            }
        }
    }

    private static Vector3I RoundToHex(Vector3 fractional)
    {
        float q = Mathf.Round(fractional.X);
        float r = Mathf.Round(fractional.Y);
        float s = Mathf.Round(fractional.Z);

        float qDiff = Mathf.Abs(q - fractional.X);
        float rDiff = Mathf.Abs(r - fractional.Y);
        float sDiff = Mathf.Abs(s - fractional.Z);

        if (qDiff > rDiff && qDiff > sDiff)
            q = -r - s;
        else if (rDiff > sDiff)
            r = -q - s;
        else
            s = -q - r;

        return new Vector3I((int)q, (int)r, (int)s);
    }

    public static int GetDistance(Vector3I a, Vector3I b)
    {
        var diff = a - b;
        return (Mathf.Abs(diff.X) + Mathf.Abs(diff.Y) + Mathf.Abs(diff.Z)) / 2;
    }
}
