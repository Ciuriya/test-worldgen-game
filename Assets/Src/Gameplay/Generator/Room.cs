using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldGen/Room")]
public class Room : ScriptableObject {

    [Tooltip("The name displayed whenever this room's name is shown to the user")]
    public string DisplayName;

    [Tooltip("The material representing this room to the user")]
    public Material Material;

    [Tooltip("The list of possible index maps used to texture this room's walls.\nPicked according to PickSequentially.")]
    public List<IndexMapWrapper> WallIndexMaps;

    [Tooltip("The list of possible index maps used to texture this room's floor.\nPicked according to PickSequentially.")]
    public List<IndexMapWrapper> FloorIndexMaps;

    [Tooltip("Should a different index map be picked for each wall?")]
    public bool PickDifferentIndexMaps;

    [Tooltip("Should we always pick in order or pick randomly?")]
    public bool PickSequentially;

    public IndexMapWrapper GetWallIndexMap(int index = 0) {
        if (WallIndexMaps.Count == 0) return null;

        if (!PickSequentially) index = Random.Range(0, WallIndexMaps.Count);
        else if (WallIndexMaps.Count <= index) return null;

        return WallIndexMaps[index];
    }
    
    public IndexMapWrapper GetFloorIndexMap() =>
        FloorIndexMaps.Count > 0 ? FloorIndexMaps[Random.Range(0, FloorIndexMaps.Count)] : null;
}
