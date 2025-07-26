using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mesh;

public class WorldMapMeshData {

    public bool IsEdge { get; private set; }

    private ZoneMeshInfo _zone;
    private ZoneMeshInfo _neighbor;
    private ushort[] _flatTriangles;
    private readonly Transform _currentTransform;
    private Vector3 _currentGlobalRef = Vector3.zero;
    private readonly Material _meshDefaultMaterial;
    private readonly WorldMapMeshHelper _helper;

    public WorldMapMeshData(Zone zone, Transform parent, Transform currentTransform, Vector3 center,
                            Vector2[] points, Vector3[] positions, Material meshDefaultMaterial, WorldMapMeshHelper helper) {
        _currentTransform = currentTransform;
        _helper = helper;
        _meshDefaultMaterial = meshDefaultMaterial;
        _currentGlobalRef = _helper.RotateVertexToMatchParentRotation(new Vector3(center.x, center.y, 0), 
                                                                      _currentTransform);

        for (int i = 0; i < positions.Length; ++i)
            positions[i] = _helper.TranslateVectorToLocal(positions[i], GetGlobalRef(), currentTransform);
        
        _zone = new ZoneMeshInfo {
            Zone = zone,
            ZoneRoomWrapper = _helper.GetZoneRoomWrapper(zone),
            Parent = parent,
            Name = zone.Room ? zone.Room.name : "No Zone",
            Center = center,
            Points = points,
            Positions = positions,
            Bounds = _helper.CalculateBounds(positions),
            EdgeIndex = 0
        };

        _zone.Material = GetMaterial(_zone);
    }

    public void SetupEdgeMesh(Zone neighbor, int edgeIndex, int neighborEdgeIndex) {
        IsEdge = true;
        _neighbor = new ZoneMeshInfo {
            Zone = neighbor,
            ZoneRoomWrapper = _helper.GetZoneRoomWrapper(neighbor),
            Center = neighbor != null ? neighbor.Center : Vector3.zero,
            EdgeIndex = neighborEdgeIndex
        };

        _neighbor.Material = GetMaterial(_neighbor);
        _neighbor.UVModifiers = GetUVModifiers(_neighbor, true);

        _zone.Name += " Edge";
        _zone.EdgeIndex = edgeIndex;
        _zone.UVModifiers = GetUVModifiers(_zone, false);
    }

    public void SetupFloorMesh() {
        IsEdge = false;
        _zone.UVModifiers = GetUVModifiers(_zone, false);
    }

    public void SetTriangles(params ushort[][] triangles) {
        // generate tris if we don't have some explicitly set
        if (triangles.Length == 0) {
            Triangulator triangulator = new Triangulator(_zone.Points);
            _flatTriangles = Array.ConvertAll(triangulator.Triangulate(), val => checked((ushort) val));
            _zone.Triangles = _flatTriangles;
        } else {
            _flatTriangles = triangles.Flatten();
            _zone.Triangles = triangles[0];

            if (_neighbor.IsValid())
                _neighbor.Triangles = triangles[1];
        }
    }

    public ZoneMeshInfo GetZoneInfo() => _zone;
    public ZoneMeshInfo GetNeighborInfo() => _neighbor;
    public Vector3 GetGlobalRef() => _currentGlobalRef;
    public ushort[] GetFlatTriangles() => _flatTriangles;
    public Material[] GetMaterials() {
        Material[] materials = new Material[IsEdge && _neighbor.IsValid() ? 2 : 1];

        materials[0] = _zone.Material;
        if (IsEdge && _neighbor.IsValid()) materials[1] = _neighbor.Material;

        return materials;
    }

    public void SetVertexLayout(ref MeshData meshData) {
        NativeArray<VertexAttributeDescriptor> vertexLayout = _helper.GetVertexLayout();
        meshData.SetVertexBufferParams(_zone.Positions.Length, vertexLayout);
        vertexLayout.Dispose();
    }

    private Material GetMaterial(ZoneMeshInfo zoneInfo) => 
        UnityEngine.Object.Instantiate(zoneInfo.ZoneRoomWrapper?.Room.Material ?? _meshDefaultMaterial);

    private UVModifiers GetUVModifiers(ZoneMeshInfo zoneInfo, bool isNeighbor) {
        if (!zoneInfo.IsValid() || zoneInfo.ZoneRoomWrapper == null) return new UVModifiers {};

        IndexMapWrapper indexMap = zoneInfo.GetIndexMap(IsEdge, zoneInfo.EdgeIndex);
		UVModifiers mods = new UVModifiers {
			PointOne = _helper.TranslateVectorToLocal(_zone.Points[0], GetGlobalRef(), _currentTransform),
			PointTwo = _helper.TranslateVectorToLocal(_zone.Points[1], GetGlobalRef(), _currentTransform),
		};

        // neighbor inverts this since we're using the zone's data (we don't hold all of the neighbor's data)
        // meaning we're calculating side 1 and inverting it to get side 2
        bool shouldFlipUV = _helper.ShouldFlipUV(mods.PointOne, mods.PointTwo, _zone.Center);
		mods.FlipUV = IsEdge && (isNeighbor ? !shouldFlipUV : shouldFlipUV);

        mods.TileSize = indexMap.GetTileSize(IsEdge, _zone.Bounds, mods.PointOne, mods.PointTwo);
        mods.UVOffset = indexMap.GetUVOffset(IsEdge, _zone.Bounds, mods.PointOne, mods.PointTwo);

        return mods;
	}
}
