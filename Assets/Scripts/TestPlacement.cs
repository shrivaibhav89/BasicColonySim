using UnityEngine;

public class TestPlacement : MonoBehaviour
{
    public BuildingPlacer buildingPlacer;
    public GameObject housePrefab;
    public GameObject farmPrefab;
    public GameObject woodcutterPrefab;

    public GameObject quarryPrefab;
    public GameObject storagePrefab;
    public GameObject townHallPrefab;
    void Start()
    {
        // Place town hall at center
        Vector3 center = new Vector3(5, 0, 5);
        Instantiate(townHallPrefab, center, Quaternion.identity);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) buildingPlacer.StartPlacement(housePrefab);
        if (Input.GetKeyDown(KeyCode.Alpha2)) buildingPlacer.StartPlacement(farmPrefab);
        if (Input.GetKeyDown(KeyCode.Alpha3)) buildingPlacer.StartPlacement(woodcutterPrefab);
        if (Input.GetKeyDown(KeyCode.Alpha4)) buildingPlacer.StartPlacement(quarryPrefab);
        if (Input.GetKeyDown(KeyCode.Alpha5)) buildingPlacer.StartPlacement(storagePrefab);
        if (Input.GetKeyDown(KeyCode.Alpha6)) buildingPlacer.StartPlacement(townHallPrefab);
    }
}