using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mesh;

internal class WorldMapMeshData {

    private ZoneMeshInfo _zone;
    private ZoneMeshInfo _neighbor;
    private NativeArray<float2> _points;
    private NativeArray<float3> _positions;
    private float3 _globalRef;
    private readonly Transform _currentTransform;
    private readonly Material _meshDefaultMaterial;
    private readonly MeshDataArray _meshDataArray;
    private readonly WorldMapMeshHelper _helper;

    internal WorldMapMeshData(Zone zone, Transform parent, Transform currentTransform, float3 center,
                            NativeArray<float2> points, NativeArray<float3> positions, Material meshDefaultMaterial, 
                            WorldMapMeshHelper helper) {
        _currentTransform = currentTransform;
        _helper = helper;
        _meshDefaultMaterial = meshDefaultMaterial;
        _meshDataArray = AllocateWritableMeshData(1);
        _globalRef = WorldMapMeshHelper.RotateVertexToMatchParentRotation(new Vector3(center.x, center.y, 0), _currentTransform);

        for (int i = 0; i < positions.Length; ++i)
            positions[i] = WorldMapMeshHelper.TranslateVectorToLocal(positions[i], _globalRef, currentTransform);

        _zone = new ZoneMeshInfo {
            Parent = parent,
            Name = zone.Room ? zone.Room.name : "No Zone",
            Info = new JobMeshInfo {
                IsEdge = false,
                Center = center,
                EdgeIndex = 0,
                IsValid = true
            },
            Bounds = WorldMapMeshHelper.CalculateBounds(positions)
        };

        _points = points;
        _positions = positions;
    }

    internal void SetupEdgeMesh(Zone zone, Zone neighbor, int edgeIndex, int neighborEdgeIndex) {
        _zone.Info.IsEdge = true;

        ZoneRoomWrapper zoneWrapper = _helper.GetZoneRoomWrapper(zone);
        _zone.Name += " Edge";
        _zone.Info.EdgeIndex = edgeIndex;
        _zone.Material = GetMaterial(_zone, zoneWrapper, false);
        _zone.Info.UVModifiers = GetUVModifiers(zone, _zone, zoneWrapper);
        
        if (neighbor != null) {
            _neighbor.Info = new JobMeshInfo {
                IsEdge = true,
                Center = neighbor != null ? (Vector3) neighbor.Center : Vector3.zero,
                EdgeIndex = neighborEdgeIndex,
                IsValid = true
            };

            ZoneRoomWrapper neighborWrapper = _helper.GetZoneRoomWrapper(neighbor);
            _neighbor.Material = GetMaterial(_neighbor, neighborWrapper, true);
            _neighbor.Info.UVModifiers = GetUVModifiers(neighbor, _neighbor, neighborWrapper);
        }
    }

    internal void SetupFloorMesh(Zone zone) {
        _zone.Info.IsEdge = false;
        _zone.Material = GetMaterial(_zone, _helper.GetZoneRoomWrapper(zone), false);
        _zone.Info.UVModifiers = GetUVModifiers(zone, _zone, _helper.GetZoneRoomWrapper(zone));
    }

    internal ZoneMeshInfo GetZoneInfo() => _zone;
    internal ZoneMeshInfo GetNeighborInfo() => _neighbor;
    internal Transform GetTransform() => _currentTransform;
    internal float3 GetGlobalRef() => _globalRef;
    internal NativeArray<float2> GetPoints() => _points;
    internal NativeArray<float3> GetPositions() => _positions;
    internal MeshDataArray GetMeshDataArray() => _meshDataArray;
    internal bool IsEdge() => _zone.Info.IsEdge;
    internal Material[] GetMaterials() {
        bool hasValidNeighbor = IsEdge() && _neighbor.Info.IsValid;
        Material[] materials = new Material[hasValidNeighbor ? 2 : 1];

        materials[0] = _zone.Material;
        if (hasValidNeighbor) materials[1] = _neighbor.Material;

        return materials;
    }

    private Material GetMaterial(ZoneMeshInfo zoneInfo, ZoneRoomWrapper wrapper, bool isNeighbor) {
        Material material = UnityEngine.Object.Instantiate(wrapper?.Room.Material ?? _meshDefaultMaterial);

        if (wrapper != null 
            && (IsEdge() || wrapper.FloorIndexMap != null)) {
                IndexMapWrapper map;

                map = wrapper.GetIndexMap(IsEdge(), zoneInfo.Info.EdgeIndex);

                if (map != null) {
                    material.SetTexture("_MainTex", map.AtlasTexture);
                    material.SetTexture("_IndexTex", map.IndexMapTexture);
                    material.SetVector("_IndexTex_TexelSize", new Vector4(1f / map.IndexMapTexture.width,
                                                                          1f / map.IndexMapTexture.height,
                                                                          map.IndexMapTexture.width,
                                                                          map.IndexMapTexture.height));

                    int cols = Mathf.CeilToInt(map.AtlasTexture.width / (float) map.AtlasGridSize);
                    int rows = Mathf.CeilToInt(map.AtlasTexture.height / (float) map.AtlasGridSize);

                    material.SetVector("_AtlasDims", new Vector2(cols, rows));
                    material.SetFloat("_Repeat", map.ShouldRepeat ? 1f : 0f);
                    material.SetColor("_Color", map.DefaultColor);
                }
        }

        if (IsEdge()) {
            material.SetFloat("_UV", isNeighbor ? 1 : 0);
            material.SetFloat("_Cull", (float) CullMode.Back);
        }

        return material;
    }

    private UVModifiers GetUVModifiers(Zone zone, ZoneMeshInfo zoneInfo, ZoneRoomWrapper wrapper) {
        if (!zoneInfo.Info.IsValid || wrapper == null) return new UVModifiers {};

        IndexMapWrapper indexMap = wrapper.GetIndexMap(IsEdge(), zoneInfo.Info.EdgeIndex);
		UVModifiers mods = new UVModifiers {
			PointOne = WorldMapMeshHelper.TranslateVectorToLocal(_points[0].ConvertTo3D(), 
                                                                 _globalRef, _currentTransform),
			PointTwo = WorldMapMeshHelper.TranslateVectorToLocal(_points[1].ConvertTo3D(), 
                                                                 _globalRef, _currentTransform),
		};

        bool shouldFlipUV = WorldMapMeshHelper.ShouldFlipUV(mods.PointOne, mods.PointTwo, 
                                                            WorldMapMeshHelper.TranslateVectorToLocal((Vector3) zone.Center,
                                                                                                      _globalRef,
                                                                                                      _currentTransform));
		mods.FlipUV = IsEdge() && shouldFlipUV;
        mods.StartOffset = IsEdge() && wrapper.Room.MergeWalls 
                           && wrapper.MergeWallsStartOffsets.Count > zoneInfo.Info.EdgeIndex ? 
                           wrapper.MergeWallsStartOffsets[zoneInfo.Info.EdgeIndex] : 0;

        mods.TileSize = indexMap.GetTileSize(IsEdge(), _zone.Bounds, mods.PointOne, mods.PointTwo);

        if (IsEdge() && wrapper.Room.MergeWalls) 
            mods.TileSize = new float2(wrapper.MergeWallsTileSize, wrapper.MergeWallsTileSize);
        
        mods.UVOffset = indexMap.GetUVOffset(IsEdge(), _zone.Bounds, mods.PointOne, mods.PointTwo);

        return mods;
	}

    internal void Dispose() {
        if (_points.IsCreated)
            _points.Dispose();

        if (_positions.IsCreated)
            _positions.Dispose();
    }
}
