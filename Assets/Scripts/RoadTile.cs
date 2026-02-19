using UnityEngine;

public class RoadTile : GridObject
{
    public const int WoodCost = 2;

    void OnDestroy()
    {
        if (gridSystem != null)
        {
            gridSystem.SetOccupied(GridPosition, false);
            gridSystem.SetRoad(GridPosition, false);
        }
    }
}
