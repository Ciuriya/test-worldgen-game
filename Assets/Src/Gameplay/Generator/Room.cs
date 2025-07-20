using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldGen/Room")]
public class Room : ScriptableObject {

    [Tooltip("The name displayed whenever this room's name is shown to the user")]
    public string DisplayName;

    [Tooltip("The material representing this room to the user")]
    public Material Material;

    [Tooltip("The list of possible index maps used to texture this room's walls.\nPicked at random.")]
    public List<IndexMapWrapper> WallIndexMaps;

    [Tooltip("The list of possible index maps used to texture this room's floor.\nPicked at random.")]
    public List<IndexMapWrapper> FloorIndexMaps;

    [Tooltip("Should a different index map be picked for each wall?")]
    public bool PickWallsRandomly;

    public IndexMapWrapper GetWallIndexMap() =>
        WallIndexMaps.Count > 0 ? WallIndexMaps[Random.Range(0, WallIndexMaps.Count)] : null;
    
    public IndexMapWrapper GetFloorIndexMap() =>
        FloorIndexMaps.Count > 0 ? FloorIndexMaps[Random.Range(0, FloorIndexMaps.Count)] : null;
}
