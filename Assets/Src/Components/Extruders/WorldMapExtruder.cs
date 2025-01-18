using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.Rendering;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using UnityEngine.Rendering.Universal;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;

    public override void Extrude() {
        if (!CanExtrude()) return;

        GameObject worldMeshObject = CreateWorldMesh(transform, "World Mesh");
        worldMeshObject.transform.position += transform.position;
    }

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

        mesh.SetVertexBufferParams(vertices.Count, GetVertexLayout());
        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, 0, MeshUpdateFlags.DontRecalculateBounds);

        mesh.SetIndexBufferParams(triangles.Count, IndexFormat.UInt32);
        mesh.SetIndexBufferData(triangles, 0, 0, triangles.Count, MeshUpdateFlags.DontRecalculateBounds);

        mesh.SetSubMeshes(subMeshDescriptors, MeshUpdateFlags.DontRecalculateBounds);
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return CreateGameObjectFromMesh(mesh, parent, name, materials);
    }

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
}
