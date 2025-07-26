using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using static WorldMapMeshHelper;
using static UnityEngine.Mesh;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    private WorldMapMeshHelper _helper = null;

    public override void Extrude() {
        if (!CanExtrude()) return;

        if (_helper == null) _helper = new WorldMapMeshHelper(World);
        else _helper.Empty();

        _helper.Setup();

        foreach (Zone zone in World.Zones)
            CreateZoneMesh(zone, transform);
    }

    private void CreateZoneMesh(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;
        if (cornerCount == 0) return;

        GameObject zoneFloor = CreateZoneFloorMesh(zone, parent);

        // generating edges all around
        for (int i = 0; i < cornerCount; ++i) {
            Corner cornerOne = zone.Corners[i];
            Corner cornerTwo = zone.Corners[(i + 1) % cornerCount];

            if (_helper.CanProcessZoneEdge(cornerOne, cornerTwo)) {
                Zone neighbor = zone.Neighbors.Find(z => z.Corners.Contains(cornerOne) && z.Corners.Contains(cornerTwo));

                CreateEdgeMesh(zone, neighbor, i, _helper.FindLeadingEdgeIndex(neighbor, cornerOne, cornerTwo),
                               zoneFloor.transform, cornerOne.Coord, cornerTwo.Coord);
            }
        }
    }

    private GameObject CreateZoneFloorMesh(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;
        Vector2[] points = new Vector2[cornerCount];
        Vector3[] vertices = new Vector3[cornerCount];

        for (int i = 0; i < cornerCount; ++i) {
            points[i] = zone.Corners[i].Coord;
            vertices[i] = new Vector3(points[i].x, points[i].y, ExtrusionDepth);
        }

        WorldMapMeshData data = new WorldMapMeshData(zone, parent, transform, zone.Center, 
                                                     points, vertices, MeshDefaultMaterial, _helper);

        data.SetupFloorMesh();
        data.SetTriangles();

        return CreateMesh(data);
    }

    private GameObject CreateEdgeMesh(Zone zone, Zone neighbor, int edgeIndex, int neighborEdgeIndex,
                                      Transform parent, Vector2 cornerOne, Vector2 cornerTwo) {
        Vector3[] positions = new Vector3[4] {
            new Vector3(cornerOne.x, cornerOne.y, -ExtrusionDepth), // front
            new Vector3(cornerOne.x, cornerOne.y, ExtrusionDepth), // back
            new Vector3(cornerTwo.x, cornerTwo.y, -ExtrusionDepth), // front
            new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionDepth)  // back
        };

        Vector3 center = (cornerOne + cornerTwo) * 0.5f;

        WorldMapMeshData data = new WorldMapMeshData(zone, parent, transform, center, 
                                                     new Vector2[] { cornerOne, cornerTwo }, 
                                                     positions, MeshDefaultMaterial, _helper);

        data.SetupEdgeMesh(neighbor, edgeIndex, neighborEdgeIndex);
        data.SetTriangles(new ushort[] { 1, 2, 0, 3, 2, 1 },
                          new ushort[] { 0, 2, 1, 1, 2, 3 });

        return CreateMesh(data);
    }

    private GameObject CreateMesh(WorldMapMeshData data) {
        MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        MeshData meshData = meshDataArray[0];

        // set descriptor layout for vertices
        data.SetVertexLayout(ref meshData);

        // calc normals
        Vector3 extrusionHeight = new Vector3(0, 0, ExtrusionHeight);
        extrusionHeight = _helper.RotateVertexToMatchParentRotation(extrusionHeight, transform);

        // only using the first sub-mesh for normal calc
        _helper.CalculateNormals(data.GetZoneInfo().Positions,
                                 data.GetZoneInfo().Triangles,
                                 out Vector3 normal, out half4 tangent);

        // build vertices
        NativeArray<CustomVertex> vertices = meshData.GetVertexData<CustomVertex>();
        BuildVertices(data, ref vertices, new CustomVertex() {
            normal = normal,
            tangent = tangent
        });

        // load triangles into mesh
        meshData.SetIndexBufferParams(data.GetFlatTriangles().Length, IndexFormat.UInt16);
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();

        triangleIndices.CopyFrom(data.GetFlatTriangles());

        // setup sub-meshes
        SetupSubMeshes(ref meshData, data);

        // create mesh and load data into it
        Mesh mesh = new Mesh() { bounds = data.GetZoneInfo().Bounds, name = "Custom Mesh" };

        ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);
        vertices.Dispose();

        GameObject meshObject = CreateGameObjectFromMesh(mesh, data.GetZoneInfo().Parent, data.GetZoneInfo().Name, data.GetMaterials());
        meshObject.transform.position += data.GetGlobalRef() + extrusionHeight;

        return meshObject;
    }
    
    private void BuildVertices(WorldMapMeshData data, ref NativeArray<CustomVertex> vertices, CustomVertex vertex) {
        Vector3[] positions = data.GetZoneInfo().Positions;
        for (int i = 0; i < positions.Length; ++i) {
            vertex.position = positions[i];
            vertex.texCoord0 = _helper.CalculateUVs(data.IsEdge, vertex.position, data.GetZoneInfo().UVModifiers);

            if (data.IsEdge) 
                vertex.texCoord1 = _helper.CalculateUVs(true, vertex.position, data.GetNeighborInfo().UVModifiers);

            vertices[i] = vertex;
        }
    }

    private void SetupSubMeshes(ref MeshData meshData, WorldMapMeshData data) {
        meshData.subMeshCount = data.IsEdge && data.GetNeighborInfo().IsValid() ? 2 : 1;

        int triangleIndex = 0;
        for (int i = 0; i < meshData.subMeshCount; ++i) {
            // setup tris
            ZoneMeshInfo zoneInfo = i == 0 ? data.GetZoneInfo() : data.GetNeighborInfo();
            ushort[] subMeshTriangles = zoneInfo.Triangles;

            meshData.SetSubMesh(i, new SubMeshDescriptor(triangleIndex, subMeshTriangles.Length));
            triangleIndex += subMeshTriangles.Length;

            PopulateMaterial(data, i, zoneInfo);
        }
    }

    private void PopulateMaterial(WorldMapMeshData data, int index, ZoneMeshInfo zoneInfo) {
        if (zoneInfo.ZoneRoomWrapper != null 
            && (data.IsEdge || zoneInfo.ZoneRoomWrapper.FloorIndexMap != null)) {
                IndexMapWrapper map;

                map = zoneInfo.GetIndexMap(data.IsEdge, zoneInfo.EdgeIndex);

                if (map != null) {
                    zoneInfo.Material.SetTexture("_MainTex", map.AtlasTexture);
                    zoneInfo.Material.SetTexture("_IndexTex", map.IndexMapTexture);
                    zoneInfo.Material.SetVector("_IndexTex_TexelSize", new Vector4(1f / map.IndexMapTexture.width,
                                                                                   1f / map.IndexMapTexture.height,
                                                                                   map.IndexMapTexture.width,
                                                                                   map.IndexMapTexture.height));

                    int cols = Mathf.CeilToInt(map.AtlasTexture.width / (float) map.AtlasGridSize);
                    int rows = Mathf.CeilToInt(map.AtlasTexture.height / (float) map.AtlasGridSize);

                    zoneInfo.Material.SetVector("_AtlasDims", new Vector2(cols, rows));
                    zoneInfo.Material.SetFloat("_UV", index);
                    zoneInfo.Material.SetFloat("_Repeat", map.ShouldRepeat ? 1f : 0f);
                    zoneInfo.Material.SetColor("_Color", map.DefaultColor);
                }
        }

        if (data.IsEdge) 
            zoneInfo.Material.SetFloat("_Cull", (float) CullMode.Back);
    }
}
