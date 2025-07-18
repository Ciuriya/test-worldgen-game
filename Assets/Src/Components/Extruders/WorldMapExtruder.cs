using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System;
using System.Collections.Generic;
using System.Linq;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    public override void Extrude() {
        if (!CanExtrude()) return;

        List<(Corner, Corner)> edgesGenerated = new List<(Corner, Corner)>();

        foreach (Zone zone in World.Zones)
            CreateZoneMesh(zone, transform, ref edgesGenerated);
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
            CreateEdgeMesh(zone, neighbor, zoneFloor.transform, cornerOne.Coord, cornerTwo.Coord);

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

        return CreateMesh(zone.Center, parent, zone.Room ? zone.Room.name : "No Zone", 
                          points, vertices, new Room[] { zone.Room });
    }

    private GameObject CreateEdgeMesh(Zone zone, Zone neighbor, Transform parent, Vector2 cornerOne, Vector2 cornerTwo) {
        Vector3[] positions = new Vector3[4] {
            new Vector3(cornerOne.x, cornerOne.y, -ExtrusionDepth), // front
            new Vector3(cornerOne.x, cornerOne.y, ExtrusionDepth), // back
            new Vector3(cornerTwo.x, cornerTwo.y, -ExtrusionDepth), // front
            new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionDepth)  // back
        };

        Vector3 center = (cornerOne + cornerTwo) / 2f;

        return CreateMesh(center, 
                          parent, (zone.Room ? zone.Room.name : "No Zone") + " Edge", 
                          new Vector2[] { cornerOne, cornerTwo }, positions, 
                          new Room[] { zone.Room, neighbor?.Room }, true, true,
                          new ushort[] { 1, 2, 0, 3, 2, 1 },
                          new ushort[] { 0, 2, 1, 1, 2, 3 });
    }

    private GameObject CreateMesh(Vector3 center, Transform parent, string name, Vector2[] points, Vector3[] positions,
                                  Room[] rooms, bool isWall = false, bool renderOneFacePerSubmesh = false,
                                  params ushort[][] trianglesParam) {
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

        for (int i = 0; i < positions.Length; ++i) {
            vertex.position = positions[i];

            Vector3 pointOne = points[0];

            RotateVerticeToMatchParentRotation(ref pointOne);
            pointOne -= globalCoords;

            vertex.texCoord0 = CalculateUVs(vertex.position, pointOne, isWall);
            vertices[i] = vertex;
        }

        // load triangles into mesh
        meshData.SetIndexBufferParams(allTriangles.Length, IndexFormat.UInt16);
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();

        triangleIndices.CopyFrom(allTriangles);

        meshData.subMeshCount = trianglesParam.Length > 0 ? trianglesParam.Length : 1;

        List<Material> materials = new List<Material>();

        // create materials
        if (meshData.subMeshCount == 1) materials.Add(Instantiate(rooms[0]?.Material ?? MeshDefaultMaterial));
        else {
            for (int i = 0; i < meshData.subMeshCount; ++i) {
                Room room = rooms.Length > i ? rooms[i] : null;
                materials.Add(Instantiate(room?.Material ?? MeshDefaultMaterial));
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
            Room room = rooms.Length > i ? rooms[i] : null;
            Material mat = materials[i];

            if (room != null && room.AtlasTexture && room.IndexMapTexture && room.AtlasGridSize > 0) {
                mat.SetTexture("_MainTex", room.AtlasTexture);
                mat.SetTexture("_IndexTex", room.IndexMapTexture);

                int cols = Mathf.CeilToInt(room.AtlasTexture.width / (float) room.AtlasGridSize);
                int rows = Mathf.CeilToInt(room.AtlasTexture.height / (float) room.AtlasGridSize);

                mat.SetVector("_AtlasDims", new Vector2(cols, rows));
            }

            if (renderOneFacePerSubmesh) 
                mat.SetFloat("_Cull", (float) CullMode.Back);
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

    private half2 CalculateUVs(Vector3 position, Vector3 cornerOne, bool isWall) {
        // could be changed to allow varying tile sizes (per room? per face?)
        float tileSize = World.Data.TileSize;

        // remove the y and find the distance between corner and position, aka edge position
        float uValue = isWall ? 
                        Vector2.Distance(new Vector2(position.x, position.z), 
                                         new Vector2(cornerOne.x, cornerOne.z))
                        : position.x;

        return new half2(
            new half(uValue / tileSize),
            new half((isWall ? position.y : position.z) / tileSize)
        );
    }

    private static (Corner,Corner) SortCornerPair(Corner a, Corner b) {
        return a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);
    }
}
