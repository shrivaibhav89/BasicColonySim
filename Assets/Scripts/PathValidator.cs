using UnityEngine;

public static class PathValidator
{
    public static bool HasAdjacentRoad(GridSystem gridSystem, Vector2Int gridPos, int maxDistance = 2)
    {
        if (gridSystem == null)
        {
            return false;
        }

        for (int dx = -maxDistance; dx <= maxDistance; dx++)
        {
            for (int dy = -maxDistance; dy <= maxDistance; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                Vector2Int neighbor = new Vector2Int(gridPos.x + dx, gridPos.y + dy);
                if (gridSystem.IsRoadAt(neighbor))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool HasAdjacentRoadInArea(GridSystem gridSystem, Vector2Int origin, Vector2Int size, int maxDistance = 2)
    {
        if (gridSystem == null)
        {
            return false;
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int gridPos = new Vector2Int(origin.x + x, origin.y + y);
                if (HasAdjacentRoad(gridSystem, gridPos, maxDistance))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
