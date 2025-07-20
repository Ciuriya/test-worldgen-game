using UnityEngine;

[System.Serializable]
public class IndexMapWrapper {

    [Tooltip("The atlas this index map takes from")]
    public Texture AtlasTexture;

    [Tooltip("The grid size of this atlas")]
    public int AtlasGridSize;

    [Tooltip("The index map that dictates what gets shown")]
    public Texture IndexMapTexture;

}
