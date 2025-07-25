using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class IndexMapWrapper {

    public enum WrapMode {
        Repeat,
        NoRepeat,
        Fit,
    }

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

    public float3 GetUVOffset(bool isWall, Bounds bounds, Vector3 pointOne, Vector3 pointTwo) {
        Vector3 size = bounds.size;

        if (isWall)
            size.x = Vector2.Distance(new Vector2(pointOne.x, pointOne.z),
                                      new Vector2(pointTwo.x, pointTwo.z));

        float3 offset = isWall ? new Vector3(0, -size.y / 2, 0) : -size / 2;

        float xOffset = size.x * ((CenterPositionInPercent.x - 50) / 100f);
        float yOffset = (isWall ? size.y : size.z) * ((CenterPositionInPercent.y - 50) / 100f);

        offset += new float3(xOffset, isWall ? yOffset : 0, isWall ? 0 : yOffset);

        return offset;
    }
    
    public Vector2 GetTileSize(bool isWall, Bounds bounds, Vector3 pointOne, Vector3 pointTwo) {
        switch (TextureWrapMode) {
            case WrapMode.Fit:
                return CalculateFitModeTileSize(isWall, bounds, pointOne, pointTwo);
            default: break;
        }

        return new Vector2(DefaultTileSize, DefaultTileSize);
    }

    private Vector2 CalculateFitModeTileSize(bool isWall, Bounds bounds, Vector3 pointOne, Vector3 pointTwo) {
        float length = bounds.size.x;

        if (isWall) 
            length = Vector2.Distance(new Vector2(pointOne.x, pointOne.z),
                                      new Vector2(pointTwo.x, pointTwo.z));
        
        return new Vector2(length / IndexMapTexture.width,
                           (isWall ? bounds.size.y : bounds.size.z) / IndexMapTexture.height);
    }
}
