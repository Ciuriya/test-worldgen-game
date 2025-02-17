using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    public override void Extrude() {
        if (!CanExtrude()) return;

        //GameObject worldMeshObject = CreateWorldMesh(transform, "World Mesh");
        //worldMeshObject.transform.position += transform.position;

        foreach (Zone zone in World.Zones)
            CreateZoneMesh(zone, transform);
    }

    /*
    private GameObject CreateWorldMesh(Transform parent, string name) {
        Mesh mesh = new Mesh();
        mesh.subMeshCount = World.Zones.Count;

        Material[] materials = new Material[mesh.subMeshCount];

        List<CustomVertex> vertices = new List<CustomVertex>();
        List<int> triangles = new List<int>();
        SubMeshDescriptor[] subMeshDescriptors = new SubMeshDescriptor[World.Zones.Count];
        bool renderFront = true, renderBack = false;

        int currentStartVertexIndex = 0;
        int currentStartTriangleIndex = 0;
        for (int i = 0; i < World.Zones.Count; ++i) {
            Zone zone = World.Zones[i];
            Vector3[] zonePositions = new Vector3[zone.Corners.Count * 2];
            Vector2[] zonePoints = new Vector2[zone.Corners.Count];

            for (int j = 0; j < zone.Corners.Count; ++j)
                zonePoints[j] = zone.Corners[j].Coord;

            Color32[] zoneColors = zone.Room?.Colors ?? new Color32[] { new Color(Random.Range(0, 1f), Random.Range(0, 1f), Random.Range(0, 1f), 0.2f) };

            CreateVerticesFromPoints(ref zonePositions, zonePoints, zonePoints.Length);
            CustomVertex[] zoneVertices = ConvertVector3ListToCustomVertexList(zonePositions, zoneColors);

            Triangulator triangulator = new Triangulator(zonePoints);
            int[] tris = triangulator.Triangulate();
            int[] zoneTriangles = new int[zonePoints.Length * 6 + (renderFront ? tris.Length : 0) + (renderBack ? tris.Length : 0)];
            int countTris = 0;

            if (renderFront) AssignFrontTriangles(ref zoneTriangles, ref countTris, tris);
            if (renderBack) AssignBackTriangles(ref zoneTriangles, ref countTris, zonePoints.Length, tris);
            AssignPerimeterTriangles(ref zoneTriangles, ref countTris, zonePoints.Length);

            subMeshDescriptors[i] = new SubMeshDescriptor() {
                indexStart = currentStartTriangleIndex,
                indexCount = zoneTriangles.Length,
                topology = MeshTopology.Triangles,
                baseVertex = currentStartVertexIndex
            };

            vertices.AddRange(zoneVertices);
            triangles.AddRange(zoneTriangles);

            currentStartVertexIndex += zoneVertices.Length;
            currentStartTriangleIndex += zoneTriangles.Length;

            materials[i] = zone.Room?.Material ?? MeshDefaultMaterial;
        }

        NativeArray<VertexAttributeDescriptor> vertexLayout = GetVertexLayout();
        mesh.SetVertexBufferParams(vertices.Count, GetVertexLayout());
        vertexLayout.Dispose();

        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, 0, MeshUpdateFlags.DontRecalculateBounds);

        mesh.SetIndexBufferParams(triangles.Count, IndexFormat.UInt32);
        mesh.SetIndexBufferData(triangles, 0, 0, triangles.Count, MeshUpdateFlags.DontRecalculateBounds);

        mesh.SetSubMeshes(subMeshDescriptors, MeshUpdateFlags.DontRecalculateBounds);

        mesh.RecalculateBounds();

        // taken from here: https://medium.com/@fra3point/runtime-normals-recalculation-in-unity-a-complete-approach-db42490a5644
        NormalSolver.RecalculateNormals(mesh, 0);
        NormalSolver.RecalculateTangents(mesh);

        return CreateGameObjectFromMesh(mesh, parent, name, materials);
    }
    */

    // todo: ensure other zones are culled out
    private void CreateZoneMesh(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;

        // ok so ideally these would be our... hallways
        // we could also generate new voronois outright but that might be too complex for this
        // could we also scale the points out WHILE generating the voronoi...?
        // so like, we'd mess with the initial implementation to do this? maybe with the edges after generating it?
        // maybe we could explode the graph after it generated, adjust the edges to match and then mark them as hallways kinda
        // could even try to scale it back down after fr...
        // also we need to do jobs
        Rect graphBounds = World.Voronoi.plotBounds;
        float xDist = (zone.Center.x - graphBounds.xMin) / graphBounds.width;
        float yDist = (zone.Center.y - graphBounds.yMin) / graphBounds.height;

        Vector2 dist = new Vector2(xDist / World.Data.MapSize.x, yDist / World.Data.MapSize.y) * 5;

        for (int i = 0; i < cornerCount; ++i) {
            Vector2 coordOne = zone.Corners[i].Coord + dist;
            Vector2 coordTwo = zone.Corners[cornerCount == i + 1 ? 0 : i + 1].Coord + dist;

            GameObject edgeObject = CreateEdgeMesh(zone, parent, coordOne, coordTwo);
            edgeObject.transform.position += transform.position;
        }
    }

    private GameObject CreateEdgeMesh(Zone zone, Transform parent, Vector2 cornerOne, Vector2 cornerTwo) {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        NativeArray<VertexAttributeDescriptor> vertexLayout = GetVertexLayout();
        meshData.SetVertexBufferParams(4, vertexLayout);
        vertexLayout.Dispose();

        NativeArray<CustomVertex> vertices = meshData.GetVertexData<CustomVertex>();

        Vector3 posOne = new Vector3(cornerOne.x, cornerOne.y, ExtrusionHeight - ExtrusionDepth); // front
        Vector3 posTwo = new Vector3(cornerOne.x, cornerOne.y, ExtrusionHeight + ExtrusionDepth); // back
        Vector3 posThree = new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionHeight - ExtrusionDepth); // front
        Vector3 posFour = new Vector3(cornerTwo.x, cornerTwo.y, ExtrusionHeight + ExtrusionDepth); // back

        Vector3 averageX = posThree - posOne + posFour - posTwo;
        Vector3 averageY = posThree - posFour + posOne - posTwo;
        // idk if it's pointing the right way or if it's even accurate tbh
        Vector3 normal = Vector3.Cross(averageX, averageY);
        half4 tangent = new half4(new float4(averageX, -1f));
        Color color = zone.Room?.Color ?? new Color(Random.Range(0, 1f), Random.Range(0, 1f), Random.Range(0, 1f), 0.2f);

        CustomVertex vertex = new CustomVertex() {
            normal = normal, 
            tangent = tangent,
            color = color
        };

        vertex.position = posOne;
        vertex.texCoord0 = new half(0);
        vertices[0] = vertex;

        vertex.position = posTwo;
        vertex.texCoord0 = new half2(new float2(1f, 0f));
        vertices[1] = vertex;

        vertex.position = posThree;
        vertex.texCoord0 = new half2(new float2(0f, 1f));
        vertices[2] = vertex;

        vertex.position = posFour;
        vertex.texCoord0 = new half(1);
        vertices[3] = vertex;

        meshData.SetIndexBufferParams(6, IndexFormat.UInt16);
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();
        triangleIndices[0] = 0;
        triangleIndices[1] = 2;
        triangleIndices[2] = 1;
        triangleIndices[3] = 1;
        triangleIndices[4] = 2;
        triangleIndices[5] = 3;

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, 6));

        Mesh mesh = new Mesh();

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);

        mesh.RecalculateBounds(); // maybe find a way to do this, it's main thread only I think :sob:

        return CreateGameObjectFromMesh(mesh, parent, name, new Material[] { zone.Room?.Material ?? MeshDefaultMaterial });
    }

    /*
    private CustomVertex[] ConvertVector3ListToCustomVertexList(Vector3[] vertices, Color32[] colors) {
        CustomVertex[] customVertices = new CustomVertex[vertices.Length];

        int colorIndex = 0;
        for (int i = 0; i < vertices.Length; ++i) {
            customVertices[i] = new CustomVertex {
                pos = vertices[i],
                color = colors[colorIndex]
            };

            colorIndex++;
            if (colorIndex == colors.Length) colorIndex = 0;
        }
        
        return customVertices;
    }
    */
}
