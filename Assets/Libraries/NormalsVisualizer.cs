#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(MeshFilter))]
public class NormalsVisualizer : Editor {
    private const string EDITOR_PREF_KEY = "mesh_normals_length";
    private const string EDITOR_PREF_BOOL = "mesh_show_normals";
    private Mesh mesh;
    private MeshFilter meshFilter;
    private Vector3[] vertices;
    private Vector3[] normals;
    private float normalsLength = 1f;
    private bool showNormals;

    private void OnEnable() {
        meshFilter = target as MeshFilter;
        if (meshFilter != null)
            mesh = meshFilter.sharedMesh;

        normalsLength = EditorPrefs.GetFloat(EDITOR_PREF_KEY);
        showNormals = EditorPrefs.GetBool(EDITOR_PREF_BOOL);

        normals = new Vector3[] { };
    }

    private void OnSceneGUI() {
        if (!showNormals || mesh == null)
            return;

        Handles.zTest = CompareFunction.LessEqual;
        Handles.matrix = meshFilter.transform.localToWorldMatrix;
        vertices = mesh.vertices;
        normals = mesh.normals;

        for (int i = 0; i < mesh.vertexCount; i++) {
            Vector3 vertexPosition = vertices[i];
            Vector3 lineEndPosition = vertices[i] + normals[i].normalized * normalsLength;

            Handles.color = Color.yellow;
            Handles.DrawLine(vertexPosition, lineEndPosition);

            Handles.color = Color.cyan;
            Handles.DrawSolidDisc(lineEndPosition, lineEndPosition, 0.1f);
        }
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        EditorGUI.BeginChangeCheck();
        {
            showNormals = EditorGUILayout.Toggle("Show Normals", showNormals);
            normalsLength = EditorGUILayout.FloatField("Normals Length", normalsLength);
            EditorGUILayout.Vector3Field("First Normal", normals.Length > 0 ? normals[0] : Vector3.zero);

            if (normalsLength < 0)
                normalsLength = 0;
        }
        if (EditorGUI.EndChangeCheck()) {
            EditorPrefs.SetBool(EDITOR_PREF_BOOL, showNormals);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY, normalsLength);
        }
    }
}

#endif