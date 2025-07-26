using Unity.Mathematics;
using UnityEngine;

public struct ZoneMeshInfo {
    public Zone Zone;
    public ZoneRoomWrapper ZoneRoomWrapper;
    public Transform Parent;
    public string Name;
    public Vector3 Center;
    public Vector2[] Points;
    public Vector3[] Positions;
    public ushort[] Triangles;
    public Bounds Bounds;
    public int EdgeIndex;
    public Material Material;
    public UVModifiers UVModifiers;

    public readonly bool IsValid() => Zone != null;
    
    public readonly IndexMapWrapper GetIndexMap(bool isEdge, int index = 0) {
        if (isEdge) {
            if (!ZoneRoomWrapper.Room.PickDifferentIndexMaps) index = 0;
            if (ZoneRoomWrapper.WallIndexMaps.Count > index) 
                return ZoneRoomWrapper.WallIndexMaps[index];
            if (ZoneRoomWrapper.WallIndexMaps.Count > 0) 
                return ZoneRoomWrapper.WallIndexMaps[index % ZoneRoomWrapper.WallIndexMaps.Count];

            return null;
        }

        return ZoneRoomWrapper.FloorIndexMap;
    }
}

public struct UVModifiers {
    public Vector3 PointOne;
    public Vector3 PointTwo;
    public bool FlipUV;
    public Vector2 TileSize;
    public float3 UVOffset;
}