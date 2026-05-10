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

        List<Vector2Int> openSet = new List<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        openSet.Add(start);
        gScore[start] = 0f;

        bool found = false;
        while (openSet.Count > 0)
        {
            Vector2Int current = GetBestNode(openSet, gScore, goal);
            if (current == goal)
            {
                found = true;
                break;
            }
            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector2Int offset in Neighbors)
            {
                Vector2Int next = current + offset;
                if (closedSet.Contains(next))
                {
                    continue;
                }

                if (!gridSystem.IsWalkable(next) && next != goal)
                {
                    continue;
                }

                if (offset.x != 0 && offset.y != 0)
                {
                    Vector2Int sideA = new Vector2Int(current.x + offset.x, current.y);
                    Vector2Int sideB = new Vector2Int(current.x, current.y + offset.y);
                    bool sideABlocked = !gridSystem.IsWalkable(sideA) && sideA != goal;
                    bool sideBBlocked = !gridSystem.IsWalkable(sideB) && sideB != goal;
                    if (sideABlocked || sideBBlocked)
                    {
                        continue;
                    }
                }

                float moveCost = GetMoveCost(offset, gridSystem, next);
                float tentative = gScore[current] + moveCost;
                if (!gScore.TryGetValue(next, out float known) || tentative < known)
                {
                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    if (!openSet.Contains(next))
                    {
                        openSet.Add(next);
                    }
                }
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

    private static Vector2Int GetBestNode(List<Vector2Int> openSet, Dictionary<Vector2Int, float> gScore, Vector2Int goal)
    {
        Vector2Int best = openSet[0];
        float bestScore = GetScore(best, gScore, goal);
        for (int i = 1; i < openSet.Count; i++)
        {
            Vector2Int candidate = openSet[i];
            float score = GetScore(candidate, gScore, goal);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static float GetScore(Vector2Int node, Dictionary<Vector2Int, float> gScore, Vector2Int goal)
    {
        float g = gScore.TryGetValue(node, out float value) ? value : float.MaxValue;
        float h = Mathf.Abs(goal.x - node.x) + Mathf.Abs(goal.y - node.y);
        return g + h;
    }

    private static float GetMoveCost(Vector2Int offset, GridSystem gridSystem, Vector2Int next)
    {
        float baseCost = (offset.x != 0 && offset.y != 0) ? 1.4142135f : 1f;
        if (gridSystem != null && gridSystem.IsRoadAt(next))
        {
            // Strong road preference without forcing impossible detours.
            return baseCost * 0.35f;
        }

        return baseCost;
    }
}
