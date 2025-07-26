using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public abstract class Extruder : MonoBehaviour {

    [Tooltip("The mesh's default material")]
    public Material MeshDefaultMaterial;

    [Tooltip("The shadow mode used by the mesh")]
    public ShadowCastingMode ShadowMode;

    [Tooltip("How thick the extrusion should be (starting from ExtrusionHeight going both sides)")]
    [Range(0, 10)] public float ExtrusionDepth;

    [Tooltip("At what height should the extrusion start")]
    [Range(-10, 10)] public float ExtrusionHeight;

    [Tooltip("What offset should the extrusion have relative to the parent?")]
    public Vector2 Offset;

    [Tooltip("The mesh's layer")]
    public string Layer;

    [Tooltip("Whether or not the extrusion should happen automatically on start")]
    public bool ExtrudeOnStart;

    [HideInInspector] public List<GameObject> Extrusions = new List<GameObject>();

    public virtual void Start() {
        if (!ExtrudeOnStart || !CanExtrude()) return;

        Extrude();
    }

    public bool CanExtrude() {
        return true;
    }

    public abstract void Extrude();

    protected GameObject Create3DMeshObject(Vector2[] points, Transform parent, string name, Material[] materials, 
                                            bool renderFront = true, bool renderBack = true) {
        Triangulator triangulator = new Triangulator(points);
        int[] tris = triangulator.Triangulate();

        return Create3DMeshObject(points, tris, null, parent, name, materials, renderFront, renderBack);
    }

    protected GameObject Create3DMeshObject(Vector2[] points, int[] tris, Vector2[] uvs, Transform parent, string name, Material[] materials, 
                                            bool renderFront = true, bool renderBack = true) {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[points.Length * 2];

        CreateVerticesFromPoints(ref vertices, points, points.Length);

        int[] triangles = new int[points.Length * 6 + (renderFront ? tris.Length : 0) + (renderBack ? tris.Length : 0)];
        int countTris = 0;

        if (renderFront) AssignFrontTriangles(ref triangles, ref countTris, tris);
        if (renderBack) AssignBackTriangles(ref triangles, ref countTris, points.Length, tris);
        AssignPerimeterTriangles(ref triangles, ref countTris, points.Length);

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return CreateGameObjectFromMesh(mesh, parent, name, materials);
    }

    protected GameObject CreateGameObjectFromMesh(Mesh mesh, Transform parent, string name, Material[] materials) {
        GameObject meshObject = new GameObject(name);
        MeshRenderer renderer = (MeshRenderer) meshObject.AddComponent(typeof(MeshRenderer));
        MeshFilter filter = meshObject.AddComponent(typeof(MeshFilter)) as MeshFilter;

        meshObject.transform.SetParent(parent);
        meshObject.layer = LayerMask.NameToLayer(Layer);
        meshObject.transform.position += (Vector3) Offset;

        renderer.materials = materials?.Length > 0 ? materials : new Material[] { MeshDefaultMaterial };
        renderer.receiveShadows = false;
        renderer.shadowCastingMode = ShadowMode;
        filter.mesh = mesh;

        Extrusions.Add(meshObject);

        return meshObject;
    }

    protected void CreateVerticesFromPoints(ref Vector3[] vertices, Vector2[] points, int backOffset) {
        for (int i = 0; i < points.Length; i++)
            CreateVerticesFromPoint(ref vertices, i, i + backOffset, points);
    }

    private void CreateVerticesFromPoint(ref Vector3[] vertices, int index, int backIndex, Vector2[] points) {
        vertices[index].x = points[index].x;
        vertices[index].y = points[index].y;
        vertices[index].z = ExtrusionHeight - ExtrusionDepth; // front vertex

        vertices[backIndex].x = points[index].x;
        vertices[backIndex].y = points[index].y;
        vertices[backIndex].z = ExtrusionHeight + ExtrusionDepth; // back vertex
    }

    protected void AssignFrontTriangles(ref int[] triangles, ref int countTris, int[] tris) {
        for (int i = 0; i < tris.Length; i += 3) {
            // front vertices
            triangles[i] = tris[i];
            triangles[i + 1] = tris[i + 1];
            triangles[i + 2] = tris[i + 2];
        }

        countTris += tris.Length;
    }

    protected void AssignBackTriangles(ref int[] triangles, ref int countTris, int offset, int[] tris) {
        for (int i = 0; i < tris.Length; i += 3) {
            // back vertices
            triangles[countTris + i] = tris[i + 2] + offset;
            triangles[countTris + i + 1] = tris[i + 1] + offset;
            triangles[countTris + i + 2] = tris[i] + offset;
        }

        countTris += tris.Length;
    }

    protected void AssignPerimeterTriangles(ref int[] triangles, ref int countTris, int pointsLength) {
        for (int i = 0; i < pointsLength; i++) {
            // triangles around the perimeter of the object
            int n = (i + 1) % pointsLength;

            triangles[countTris] = i;
            triangles[countTris + 1] = n;
            triangles[countTris + 2] = i + pointsLength;
            triangles[countTris + 3] = n;
            triangles[countTris + 4] = n + pointsLength;
            triangles[countTris + 5] = i + pointsLength;

            countTris += 6;
        }
    }
}
