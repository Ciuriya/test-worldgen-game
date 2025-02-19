using UnityEngine;

[CreateAssetMenu(menuName = "WorldGen/Room")]
public class Room : ScriptableObject {

    [Tooltip("The name displayed whenever this room's name is shown to the user")]
    public string DisplayName;

    [Tooltip("The material representing this room to the user")]
    public Material Material;

    [Tooltip("The texture applied to this room? idk this is a bit sus")]
    public Texture Texture;

    [Tooltip("The color representing this room to the user")]
    public Color Color;
}
