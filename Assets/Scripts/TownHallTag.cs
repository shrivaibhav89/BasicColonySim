using UnityEngine;

// Attach this to your TownHall prefab
public class TownHallTag : MonoBehaviour {
    void Awake() {
        gameObject.tag = "TownHall";
    }
}
