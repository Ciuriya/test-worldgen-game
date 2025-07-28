using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
internal struct ZoneMeshInfo {
    internal Transform Parent;
    internal string Name;
    internal JobMeshInfo Info;
    internal Bounds Bounds;
    internal Material Material;
}

[Serializable]
[BurstCompile]
internal struct JobMeshInfo {
    public bool IsEdge;
    internal float3 Center;
    internal int EdgeIndex;
    internal UVModifiers UVModifiers;
    internal bool IsValid;
}

[Serializable]
[BurstCompile]
internal struct UVModifiers {
    internal float3 PointOne;
    internal float3 PointTwo;
    internal bool FlipUV;
    internal float2 TileSize;
    internal float3 UVOffset;
}