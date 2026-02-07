using UnityEngine;

public class GridObject : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }
    protected GridSystem gridSystem;

    public virtual void Initialize(GridSystem grid, Vector2Int gridPos)
    {
        gridSystem = grid;
        GridPosition = gridPos;
    }
}
