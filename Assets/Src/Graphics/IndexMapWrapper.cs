using MyBox;
using UnityEngine;

[System.Serializable]
public class IndexMapWrapper {

    [Tooltip("The atlas this index map takes from")]
    public Texture AtlasTexture;

    [Tooltip("The grid size of this atlas")]
    public int AtlasGridSize;

    [Tooltip("The index map that dictates what gets shown")]
    public Texture IndexMapTexture;

    [Tooltip("Color to use when there's nothing to display")]
    public Color DefaultColor;

    [Tooltip("The default tile size to use to display this texture.\nWill be used if the wrap mode allows for it.")]
    public float DefaultTileSize;

    [Tooltip("Where should the texture be centered?\n50% meaning the center of the texture is in the middle.")]
    public Vector2 CenterPositionInPercent = new Vector2(50, 50);

    [Tooltip("How should this texture wrap?")]
    public WrapMode TextureWrapMode;

    public enum WrapMode {
        Repeat,
        NoRepeat,
        Stretch,
    }
}
