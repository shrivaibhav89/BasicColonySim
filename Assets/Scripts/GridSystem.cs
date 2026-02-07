using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 1f;
    
    // Track which tiles are occupied
    private bool[,] occupiedTiles;
    
    void Awake()
    {
        // Initialize grid
        occupiedTiles = new bool[gridWidth, gridHeight];
    }
    
    // Convert world position to grid coordinates
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
    }
    
    // Convert grid coordinates to world position
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = gridPos.x * cellSize + cellSize * 0.5f;
        float z = gridPos.y * cellSize + cellSize * 0.5f;
        return new Vector3(x, 0, z);
    }
    
    // Check if a tile is valid and not occupied
    public bool IsValidPlacement(Vector2Int gridPos)
    {
        // Check bounds
        if (gridPos.x < 0 || gridPos.x >= gridWidth) return false;
        if (gridPos.y < 0 || gridPos.y >= gridHeight) return false;
        
        // Check if occupied
        return !occupiedTiles[gridPos.x, gridPos.y];
    }
    
    // Mark a tile as occupied
    public void SetOccupied(Vector2Int gridPos, bool occupied)
    {
        if (gridPos.x >= 0 && gridPos.x < gridWidth && 
            gridPos.y >= 0 && gridPos.y < gridHeight)
        {
            occupiedTiles[gridPos.x, gridPos.y] = occupied;
        }
    }
}