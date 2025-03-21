using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    public override void Extrude() {
        if (!CanExtrude()) return;

        foreach (Zone zone in World.Zones)
            CreateZoneMesh(zone, transform);
    }

    private void CreateZoneMesh(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;

        GameObject zoneFloor = CreateZoneFloorMesh(zone, parent);

        // generating edges all around
        for (int i = 0; i < cornerCount; ++i) {
            Vector2 coordOne = zone.Corners[i].Coord;
            Vector2 coordTwo = zone.Corners[cornerCount == i + 1 ? 0 : i + 1].Coord;

            // baaaaaarely moving them towards the center so they don't have z-fighting
            coordOne += (zone.Center - coordOne).normalized * 0.0001f;
            coordTwo += (zone.Center - coordTwo).normalized * 0.0001f;

            GameObject edgeObject = CreateEdgeMesh(zone, zoneFloor.transform, coordOne, coordTwo);
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

        return CreateMesh(zone, parent, zone.Room ? zone.Room.name : "No Zone", points, vertices, null, true);
    }

    private GameObject CreateEdgeMesh(Zone zone, Transform parent, Vector2 cornerOne, Vector2 cornerTwo) {
        Vector3[] positions = new Vector3[4] {
            new Vector3(cornerOne.x, cornerOne.y, -ExtrusionDepth), // front
            new Vector3(cornerOne.x, cornerOne.y, ExtrusionDepth), // back
            new Vector3(cornerTwo.x, cornerTwo.y, -ExtrusionDepth), // front
            new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionDepth)  // back
        };

        return CreateMesh(zone, parent, (zone.Room ? zone.Room.name : "No Zone") + " Edge", 
                          new Vector2[] { cornerOne, cornerTwo }, positions, new ushort[] { 0, 2, 1, 1, 2, 3 }, false);
    }

    private GameObject CreateMesh(Zone zone, Transform parent, string name, Vector2[] points, Vector3[] positions, 
                                  ushort[] triangles = null, bool overrideNormalsForFlatSurface = false) {
        if (triangles == null) {
            Triangulator triangulator = new Triangulator(points);
            triangles = Array.ConvertAll(triangulator.Triangulate(), val => checked((ushort) val));
        }

        Vector3 globalCoords = new Vector3(zone.Center.x, zone.Center.y, 0);
        RotateVerticeToMatchParentRotation(ref globalCoords);

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        NativeArray<VertexAttributeDescriptor> vertexLayout = GetVertexLayout();
        meshData.SetVertexBufferParams(positions.Length, vertexLayout);
        vertexLayout.Dispose();

        NativeArray<CustomVertex> vertices = meshData.GetVertexData<CustomVertex>();

        for (int i = 0; i < positions.Length; ++i) {
            Vector3 updatedPosition = positions[i];
            RotateVerticeToMatchParentRotation(ref updatedPosition);
            updatedPosition -= globalCoords;
            positions[i] = updatedPosition;
        }

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

        Bounds bounds = new Bounds(new Vector3((max.x - min.x) / 2f + min.x,
                                               (max.y - min.y) / 2f + min.y,
                                               (max.z - min.z) / 2f + min.z),
                                   max - min);

        Vector3 extrusionHeight = new Vector3(0, 0, ExtrusionHeight);
        RotateVerticeToMatchParentRotation(ref extrusionHeight);

        CalculateNormals(positions, triangles, out Vector3 normal, out half4 tangent, 
                         overrideNormalsForFlatSurface ? globalCoords + extrusionHeight : Vector3.zero);

        Color color = zone.Room?.Color ?? new Color(Random.Range(0, 1f), Random.Range(0, 1f), Random.Range(0, 1f), 1.0f);

        CustomVertex vertex = new CustomVertex() {
            normal = normal,
            tangent = tangent,
            color = color
        };

        for (int i = 0; i < positions.Length; ++i) {
            vertex.position = positions[i];
            vertex.texCoord0 = new half2(new half((positions[i].x - bounds.min.x) / bounds.size.x), 
                                         new half((positions[i].y - bounds.min.y) / bounds.size.y));
            vertices[i] = vertex;
        }

        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt16);
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();

        triangleIndices.CopyFrom(triangles);

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));

        Mesh mesh = new Mesh() { bounds = bounds, name = "Custom Mesh" };

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);
        vertices.Dispose();

        Material mat = Instantiate(zone.Room?.Material ?? MeshDefaultMaterial);
        if (zone.Room != null) mat.SetTexture("_MainTex", zone.Room.Texture);
        else mat.SetTexture("_MainTex", null);

        GameObject meshObject = CreateGameObjectFromMesh(mesh, parent, name, new Material[] { mat });
        meshObject.transform.position += globalCoords + extrusionHeight;
        return meshObject;
    }

    // note: this is a very precise function currently (averages all tris), it can be lightened up as needed
    private void CalculateNormals(Vector3[] positions, ushort[] triangles, out Vector3 normal, out half4 tangent, Vector3 zoneCenter) {
        bool useZoneCenter = zoneCenter != Vector3.zero;
        Vector3 averageX = Vector3.zero;
        Vector3 averageY = Vector3.zero;
        int count = 0;

        for (int i = 0; i < triangles.Length; i += 3) {
            // we're using the zone center because it kept giving normals that were all zeroes (since they were on the same surface)
            Vector3 p1 = useZoneCenter ? zoneCenter : positions[triangles[i]];
            Vector3 p2 = positions[triangles[i + 1]];
            Vector3 p3 = positions[triangles[i + 2]];

            averageX += (p2 - p1).normalized;
            averageY += (p3 - p1).normalized;

            count++;
        }

        averageX /= count;
        averageY /= count;

        normal = -Vector3.Cross(averageX, averageY).normalized;
        tangent = new half4(new float4(averageX, -1f));
    }
}
