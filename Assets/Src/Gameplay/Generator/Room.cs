using UnityEngine;

[CreateAssetMenu(menuName = "WorldGen/Room")]
public class Room : ScriptableObject {

    [Tooltip("The name displayed whenever this room's name is shown to the user")]
    public string DisplayName;

    [Tooltip("The material representing this room to the user")]
    public Material Material;

    [Tooltip("The atlas this room's index map takes from")]
    public Texture AtlasTexture;

    [Tooltip("The grid size of this atlas")]
    public int AtlasGridSize;

    [Tooltip("The index map that dictates this room's layout")]
    public Texture IndexMapTexture;
}
