using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    private Dictionary<Zone, ZoneRoomWrapper> zoneRoomWrappers;

    public override void Extrude() {
        if (!CanExtrude()) return;

        List<(Corner, Corner)> edgesGenerated = new List<(Corner, Corner)>();

        SetupRoomWrappers();

        foreach (Zone zone in World.Zones)
            CreateZoneMesh(zone, transform, ref edgesGenerated);
    }

    // this is needed to ensure consistency
    // when we generate an edge, we generate it on both sides at once
    // this means we need both zones at once, even before the other zone is processed here
    // so we need to pre-emptively set which index maps the zones will be using
    // otherwise we would be re-generating some at times and causing weirdness
    private void SetupRoomWrappers() {
        zoneRoomWrappers = new Dictionary<Zone, ZoneRoomWrapper>();

        foreach (Zone zone in World.Zones)
            if (zone.Room != null)
                zoneRoomWrappers.Add(zone, new ZoneRoomWrapper() {
                    Room = zone.Room,
                    WallIndexMaps = new List<IndexMapWrapper>(),
                    FloorIndexMap = zone.Room.GetFloorIndexMap()
                });

        foreach (Zone zone in zoneRoomWrappers.Keys) {
            ZoneRoomWrapper wrapper = zoneRoomWrappers[zone];
            int cornerCount = zone.Corners.Count;

            for (int i = 0; i < cornerCount; ++i)
                if ((i > 0 && wrapper.Room.PickDifferentIndexMaps) || i == 0) {
                    IndexMapWrapper picked = wrapper.Room.GetWallIndexMap(i);

                    if (picked != null) wrapper.WallIndexMaps.Add(picked);
                }
        }
    }

    private void CreateZoneMesh(Zone zone, Transform parent, ref List<(Corner, Corner)> edgesGenerated) {
        int cornerCount = zone.Corners.Count;
        GameObject zoneFloor = CreateZoneFloorMesh(zone, parent);

        // generating edges all around
        for (int i = 0; i < cornerCount; ++i) {
            Corner cornerOne = zone.Corners[i];
            Corner cornerTwo = zone.Corners[cornerCount == i + 1 ? 0 : i + 1];
            (Corner, Corner) cornerPair = SortCornerPair(cornerOne, cornerTwo);

            if (edgesGenerated.Contains(cornerPair)) continue;

            Zone neighbor = zone.Neighbors.Find(z => z.Corners.Contains(cornerOne) && z.Corners.Contains(cornerTwo));
            CreateEdgeMesh(zone, neighbor, i, neighbor != null ? FindLeadingEdgeIndex(neighbor, cornerOne, cornerTwo) : 0,
                           zoneFloor.transform, cornerOne.Coord, cornerTwo.Coord);

            edgesGenerated.Add(cornerPair);
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

        var zoneRoomWrapper = zoneRoomWrappers.ContainsKey(zone) ? zoneRoomWrappers[zone] : null;

        return CreateMesh(zone.Center, parent, zone.Room ? zone.Room.name : "No Zone", 
                          points, vertices, new ZoneRoomWrapper[] { zoneRoomWrapper }, 
                          new Vector3[] { zone.Center });
    }

    private GameObject CreateEdgeMesh(Zone zone, Zone neighbor, int edgeIndex, int neighborEdgeIndex,
                                      Transform parent, Vector2 cornerOne, Vector2 cornerTwo) {
        Vector3[] positions = new Vector3[4] {
            new Vector3(cornerOne.x, cornerOne.y, -ExtrusionDepth), // front
            new Vector3(cornerOne.x, cornerOne.y, ExtrusionDepth), // back
            new Vector3(cornerTwo.x, cornerTwo.y, -ExtrusionDepth), // front
            new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionDepth)  // back
        };

        Vector3 center = (cornerOne + cornerTwo) / 2f;

        var zoneRoomWrapper = zoneRoomWrappers.ContainsKey(zone) ? zoneRoomWrappers[zone] : null;
        var neighborRoomWrapper = neighbor != null ? 
                                    (zoneRoomWrappers.ContainsKey(neighbor) ? zoneRoomWrappers[neighbor] : null) 
                                    : null;

        return CreateMesh(center, 
                          parent, (zone.Room ? zone.Room.name : "No Zone") + " Edge", 
                          new Vector2[] { cornerOne, cornerTwo }, positions, 
                          new ZoneRoomWrapper[] { zoneRoomWrapper, neighborRoomWrapper },
                          new Vector3[] { zone.Center, neighbor != null ? neighbor.Center : Vector3.zero },
                          new int[] { edgeIndex, neighborEdgeIndex },
                          true, true,
                          new ushort[] { 1, 2, 0, 3, 2, 1 },
                          new ushort[] { 0, 2, 1, 1, 2, 3 });
    }

    private GameObject CreateMesh(Vector3 center, Transform parent, string name, Vector2[] points, Vector3[] positions,
                                  ZoneRoomWrapper[] rooms, Vector3[] zoneCenters, int[] edgeIndexes = null,
                                  bool isWall = false, bool renderOneFacePerSubmesh = false, params ushort[][] trianglesParam) {
        ushort[] allTriangles;

        // generate tris if we don't have some explicitly set
        if (trianglesParam.Length == 0) {
            Triangulator triangulator = new Triangulator(points);
            allTriangles = Array.ConvertAll(triangulator.Triangulate(), val => checked((ushort) val));
        } else allTriangles = trianglesParam.SelectMany(a => a).ToArray();

        Vector3 globalCoords = new Vector3(center.x, center.y, 0);
        RotateVerticeToMatchParentRotation(ref globalCoords);

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        // set descriptor layout for vertices
        NativeArray<VertexAttributeDescriptor> vertexLayout = GetVertexLayout();
        meshData.SetVertexBufferParams(positions.Length, vertexLayout);
        vertexLayout.Dispose();

        NativeArray<CustomVertex> vertices = meshData.GetVertexData<CustomVertex>();

        // adjust positions from global to local coords
        for (int i = 0; i < positions.Length; ++i) {
            Vector3 updatedPosition = positions[i];
            RotateVerticeToMatchParentRotation(ref updatedPosition);
            updatedPosition -= globalCoords;
            positions[i] = updatedPosition;
        }

        Bounds bounds = CalculateBounds(positions);

        // calc normals
        Vector3 extrusionHeight = new Vector3(0, 0, ExtrusionHeight);
        RotateVerticeToMatchParentRotation(ref extrusionHeight);

        CalculateNormals(positions, // use the first sub-mesh for normal calc
                         trianglesParam.Length > 0 ? trianglesParam[0] : allTriangles, 
                         out Vector3 normal, out half4 tangent);

        // build vertices
        CustomVertex vertex = new CustomVertex() {
            normal = normal,
            tangent = tangent
        };

        CalculateRoomUVModifiers(isWall, points, rooms, zoneCenters, edgeIndexes, globalCoords, bounds, 
                                 out Vector3 pointOne, out Vector3 pointTwo, out bool flipUV0, out bool flipUV1, 
                                 out Vector2 tileSize0, out Vector2 tileSize1, out float3 uvOffset0, out float3 uvOffset1);

        for (int i = 0; i < positions.Length; ++i) {
            vertex.position = positions[i];
            vertex.texCoord0 = CalculateUVs(vertex.position + uvOffset0, pointOne, pointTwo, isWall, tileSize0, flipUV0);

            if (isWall && rooms[1] != null) 
                vertex.texCoord1 = CalculateUVs(vertex.position + uvOffset1, pointOne, pointTwo, isWall, tileSize1, flipUV1);

            vertices[i] = vertex;
        }

        // load triangles into mesh
        meshData.SetIndexBufferParams(allTriangles.Length, IndexFormat.UInt16);
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();

        triangleIndices.CopyFrom(allTriangles);

        meshData.subMeshCount = trianglesParam.Length > 0 ? trianglesParam.Length : 1;

        List<Material> materials = new List<Material>();

        // create materials
        if (meshData.subMeshCount == 1) materials.Add(Instantiate(rooms[0]?.Room.Material ?? MeshDefaultMaterial));
        else {
            for (int i = 0; i < meshData.subMeshCount; ++i) {
                ZoneRoomWrapper roomWrapper = rooms.Length > i ? rooms[i] : null;
                materials.Add(Instantiate(roomWrapper?.Room.Material ?? MeshDefaultMaterial));
            }
        }

        // setup sub-meshes
        int triangleIndex = 0;
        for (int i = 0; i < meshData.subMeshCount; ++i) {
            // setup tris
            ushort[] subMeshTriangles = trianglesParam.Length == 0 ? allTriangles : trianglesParam[i];

            meshData.SetSubMesh(i, new SubMeshDescriptor(triangleIndex, subMeshTriangles.Length));
            triangleIndex += subMeshTriangles.Length;

            // setup mat
            ZoneRoomWrapper room = rooms.Length > i ? rooms[i] : null;
            Material mat = materials[i];

            PopulateMaterial(i, isWall ? edgeIndexes[i] : 0, mat, room, isWall, renderOneFacePerSubmesh);
        }

        // create mesh and load data into it
        Mesh mesh = new Mesh() { bounds = bounds, name = "Custom Mesh" };

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);
        vertices.Dispose();

        GameObject meshObject = CreateGameObjectFromMesh(mesh, parent, name, materials.ToArray());
        meshObject.transform.position += globalCoords + extrusionHeight;

        return meshObject;
    }

    private Bounds CalculateBounds(Vector3[] positions) {
        Vector3 min = Vector2.positiveInfinity;
        Vector3 max = Vector2.negativeInfinity;

        foreach (Vector3 position in positions) {
            if (position.x < min.x) min.x = position.x;
            if (position.y < min.y) min.y = position.y;
            if (position.z < min.z) min.z = position.z;
            if (position.x > max.x) max.x = position.x;
            if (position.y > max.y) max.y = position.y;
            if (position.z > max.z) max.z = position.z;
        }

        return new Bounds(new Vector3((max.x - min.x) / 2f + min.x,
                                      (max.y - min.y) / 2f + min.y,
                                      (max.z - min.z) / 2f + min.z),
                          max - min);
    }

    private void CalculateNormals(Vector3[] positions, ushort[] triangles, out Vector3 normal, out half4 tangent) {
        Vector3 nSum = Vector3.zero;
        Vector3 tSum = Vector3.zero;

        for (int i = 0; i < triangles.Length; i += 3) {
            Vector3 p0 = positions[triangles[i]];
            Vector3 p1 = positions[triangles[i + 1]];
            Vector3 p2 = positions[triangles[i + 2]];

            // area-weighted face normal
            Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
            nSum += face;

            // use the first edge as a tangent candidate
            tSum += p1 - p0;
        }

        normal = nSum.normalized;

        Vector3 tan3 = Vector3.ProjectOnPlane(tSum, normal).normalized;
        tangent = new half4(new float4(tan3, 1f)); // −1 to flip
    }

    private half2 CalculateUVs(Vector3 position, Vector3 cornerOne, Vector3 cornerTwo, 
                               bool isWall, Vector2 tileSize, bool flip = false) {
        float uValue = position.x;

        if (isWall) {
            float length = Vector2.Distance(new Vector2(cornerOne.x, cornerOne.z),
                                            new Vector2(cornerTwo.x, cornerTwo.z));

            uValue = Vector2.Distance(new Vector2(position.x, position.z),
                                      new Vector2(cornerOne.x, cornerOne.z));

            if (flip) uValue = length - uValue;
        }

        return new half2(
            new half(uValue / tileSize.x),
            new half((isWall ? position.y : position.z) / tileSize.y)
        );
    }
    
    private void CalculateRoomUVModifiers(bool isWall, Vector2[] points, ZoneRoomWrapper[] rooms, Vector3[] zoneCenters,
                                          int[] edgeIndexes, Vector3 globalCoords, Bounds bounds,
                                          out Vector3 pointOne, out Vector3 pointTwo, out bool flipUV0, out bool flipUV1,
                                          out Vector2 tileSize0, out Vector2 tileSize1, out float3 uvOffset0, out float3 uvOffset1) {
        pointOne = points[0];
        pointTwo = points[1];

        IndexMapWrapper wrapper0 = null, wrapper1 = null;

        if (rooms[0] != null) wrapper0 = rooms[0].GetIndexMap(isWall, isWall ? edgeIndexes[0] : 0);
        if (isWall && rooms[1] != null) wrapper1 = rooms[1].GetIndexMap(true, edgeIndexes[1]);

        flipUV0 = isWall && wrapper0 != null && ShouldFlipUV(pointOne, pointTwo, zoneCenters[0]);
        flipUV1 = isWall && wrapper1 != null && ShouldFlipUV(pointOne, pointTwo, zoneCenters[1]);

        RotateVerticeToMatchParentRotation(ref pointOne);
        RotateVerticeToMatchParentRotation(ref pointTwo);

        pointOne -= globalCoords;
        pointTwo -= globalCoords;

        tileSize0 = wrapper0 != null ? wrapper0.GetTileSize(isWall, bounds, pointOne, pointTwo) : Vector2.one;
        tileSize1 = wrapper1 != null ? wrapper1.GetTileSize(isWall, bounds, pointOne, pointTwo) : Vector2.one;
        uvOffset0 = wrapper0 != null ? wrapper0.GetUVOffset(isWall, bounds, pointOne, pointTwo) : float3.zero;
        uvOffset1 = wrapper1 != null ? wrapper1.GetUVOffset(isWall, bounds, pointOne, pointTwo) : float3.zero;
    }

    private bool ShouldFlipUV(Vector2 a, Vector2 b, Vector2 interior) {
        // cross >= 0 = interior on right side
        return ((b.x - a.x) * (interior.y - a.y) -
                (b.y - a.y) * (interior.x - a.x)) >= 0;
    }

    private void PopulateMaterial(int index, int edgeIndex, Material mat, ZoneRoomWrapper room, bool isWall, bool renderOneFacePerSubmesh) {
        if (room != null 
            && (isWall || room.FloorIndexMap != null)) {
                IndexMapWrapper map;

                map = room.GetIndexMap(isWall, edgeIndex);

                if (map != null) {
                    mat.SetTexture("_MainTex", map.AtlasTexture);
                    mat.SetTexture("_IndexTex", map.IndexMapTexture);
                    mat.SetVector("_IndexTex_TexelSize", new Vector4(1f / map.IndexMapTexture.width,
                                                                    1f / map.IndexMapTexture.height,
                                                                    map.IndexMapTexture.width,
                                                                    map.IndexMapTexture.height));

                    int cols = Mathf.CeilToInt(map.AtlasTexture.width / (float) map.AtlasGridSize);
                    int rows = Mathf.CeilToInt(map.AtlasTexture.height / (float) map.AtlasGridSize);

                    mat.SetVector("_AtlasDims", new Vector2(cols, rows));
                    mat.SetFloat("_UV", index);
                    mat.SetFloat("_Repeat", map.ShouldRepeat ? 1f : 0f);
                    mat.SetColor("_Color", map.DefaultColor);
                }
        }

        if (renderOneFacePerSubmesh) 
            mat.SetFloat("_Cull", (float) CullMode.Back);
    }

    private int FindLeadingEdgeIndex(Zone zone, Corner cornerOne, Corner cornerTwo) {
        int indexA = zone.Corners.IndexOf(cornerOne);
        int indexB = zone.Corners.IndexOf(cornerTwo);
        int cornerCount = zone.Corners.Count;

        // pick the index whose next vertex (mod cornerCount) is the other corner
        return (indexA + 1) % cornerCount == indexB ? indexA :
               (indexB + 1) % cornerCount == indexA ? indexB :
               Mathf.Min(indexA, indexB);            // fallback, should not happen
    }

    private static (Corner,Corner) SortCornerPair(Corner a, Corner b) {
        return a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);
    }
}
