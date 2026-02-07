using UnityEngine;

[RequireComponent(typeof(GridSystem))]
public class GridVisualizer : MonoBehaviour
{
    public Material gridMaterial;
    public bool showGrid = true;
    
    private GridSystem gridSystem;
    private GameObject gridLinesParent;
    
    void Start()
    {
        gridSystem = GetComponent<GridSystem>();
        CreateGridLines();
    }
    
    void CreateGridLines()
    {
        gridLinesParent = new GameObject("GridLines");
        gridLinesParent.transform.SetParent(transform);
        
        // Create vertical lines
        for (int x = 0; x <= gridSystem.gridWidth; x++)
        {
            CreateLine(
                new Vector3(x * gridSystem.cellSize, 0.01f, 0),
                new Vector3(x * gridSystem.cellSize, 0.01f, gridSystem.gridHeight * gridSystem.cellSize)
            );
        }
        
        // Create horizontal lines
        for (int z = 0; z <= gridSystem.gridHeight; z++)
        {
            CreateLine(
                new Vector3(0, 0.01f, z * gridSystem.cellSize),
                new Vector3(gridSystem.gridWidth * gridSystem.cellSize, 0.01f, z * gridSystem.cellSize)
            );
        }
    }
    
    void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(gridLinesParent.transform);
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        
        if (gridMaterial != null)
        {
            line.material = gridMaterial;
        }
    }
    
    void Update()
    {
        // Toggle grid with G key
        if (Input.GetKeyDown(KeyCode.G))
        {
            showGrid = !showGrid;
            gridLinesParent.SetActive(showGrid);
        }
    }
}