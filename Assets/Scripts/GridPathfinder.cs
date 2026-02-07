using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private static readonly Vector2Int[] Neighbors =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public static List<Vector2Int> FindPath(GridSystem gridSystem, Vector2Int start, Vector2Int goal, bool allowGoalOccupied = true, bool allowStartOccupied = true)
    {
        if (gridSystem == null)
        {
            return new List<Vector2Int>();
        }

        if (!gridSystem.IsWalkable(start) && !allowStartOccupied)
        {
            return new List<Vector2Int>();
        }

        if (!gridSystem.IsWalkable(goal) && !allowGoalOccupied)
        {
            return new List<Vector2Int>();
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        frontier.Enqueue(start);
        visited.Add(start);

        bool found = false;
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal)
            {
                found = true;
                break;
            }

            foreach (Vector2Int offset in Neighbors)
            {
                Vector2Int next = current + offset;
                if (visited.Contains(next))
                {
                    continue;
                }

                if (!gridSystem.IsWalkable(next) && next != goal)
                {
                    continue;
                }

                visited.Add(next);
                frontier.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!found)
        {
            return new List<Vector2Int>();
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int step = goal;
        path.Add(step);
        while (step != start)
        {
            step = cameFrom[step];
            path.Add(step);
        }

        path.Reverse();
        return path;
    }
}
