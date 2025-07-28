using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using static WorldMapMeshHelper;
using static UnityEngine.Mesh;
using Unity.Jobs;
using System.Collections.Generic;
using System.Collections;
using System;

public class WorldMapExtruder : Extruder {

    [Tooltip("The world to extrude")]
    public World World;
    public bool IsGenerating { get; private set; }
    public double GenerationTime { get; private set; }

    private WorldMapMeshHelper _helper = null;
    private JobHandle _currentJob;
    private WorldMapMeshData[] _currentMeshDataArray;
    private NativeArray<MeshData> _currentResultArray;
    internal static readonly NativeArray<ushort> ZoneTriangles = new NativeArray<ushort>(
        new ushort[] { 1, 2, 0, 3, 2, 1 }, Allocator.Persistent);
    internal static readonly NativeArray<ushort> NeighborTriangles = new NativeArray<ushort>(
        new ushort[] { 0, 2, 1, 1, 2, 3 }, Allocator.Persistent);
    internal static readonly NativeArray<ushort> FlatTriangles = new NativeArray<ushort>(
        new ushort[] { 1, 2, 0, 3, 2, 1, 0, 2, 1, 1, 2, 3 }, Allocator.Persistent);

    public override void Extrude() {
        if (!CanExtrude()) return;

        IsGenerating = true;
        GenerationTime = Time.realtimeSinceStartupAsDouble;
        _helper = new WorldMapMeshHelper(World);
        _helper.Setup();

        GenerateFloorMeshes();
    }
    
    private void GenerateFloorMeshes() {
        _currentMeshDataArray = new WorldMapMeshData[World.Zones.Count];
        NativeArray<JobMeshInfo> zoneDataArray = new NativeArray<JobMeshInfo>(World.Zones.Count, Allocator.TempJob);
        NativeArray<JobMeshInfo> neighborDataArray = new NativeArray<JobMeshInfo>(World.Zones.Count, Allocator.TempJob);

        for (int i = 0; i < World.Zones.Count; ++i) {
            _currentMeshDataArray[i] = CreateZoneFloorData(World.Zones[i], transform);
            zoneDataArray[i] = _currentMeshDataArray[i].GetZoneInfo().Info;
            neighborDataArray[i] = _currentMeshDataArray[i].GetNeighborInfo().Info;
        }

        StartGeneratingMeshes(zoneDataArray, neighborDataArray, false);
    }

    private void FinalizeFloorMeshes() {
        List<WorldMapMeshData> edgeDataList = new List<WorldMapMeshData>();

        for (int i = 0; i < _currentMeshDataArray.Length; ++i) {
            GameObject floorObject = CreateMesh(_currentMeshDataArray[i]);
            edgeDataList.AddRange(CreateZoneEdgesData(World.Zones[i], floorObject.transform));
        }

        CleanupAfterGeneratingMeshes();
        GenerateEdgeMeshes(edgeDataList.ToArray());
    }

    private void GenerateEdgeMeshes(WorldMapMeshData[] edgeDataArray) {
        _currentMeshDataArray = edgeDataArray;
        NativeArray<JobMeshInfo> zoneDataArray = new NativeArray<JobMeshInfo>(edgeDataArray.Length, Allocator.TempJob);
        NativeArray<JobMeshInfo> neighborDataArray = new NativeArray<JobMeshInfo>(edgeDataArray.Length, Allocator.TempJob);

        for (int i = 0; i < edgeDataArray.Length; ++i) {
            zoneDataArray[i] = _currentMeshDataArray[i].GetZoneInfo().Info;
            neighborDataArray[i] = _currentMeshDataArray[i].GetNeighborInfo().Info;
        }

        StartGeneratingMeshes(zoneDataArray, neighborDataArray, true);
    }

    private void FinalizeEdgeMeshes() {
        for (int i = 0; i < _currentMeshDataArray.Length; ++i)
            CreateMesh(_currentMeshDataArray[i]);
    }

    private void StartGeneratingMeshes(NativeArray<JobMeshInfo> zoneArray, NativeArray<JobMeshInfo> neighborArray, bool isEdge) {
        if (!zoneArray.IsCreated || !neighborArray.IsCreated)
            return;

        _currentResultArray = new NativeArray<MeshData>(zoneArray.Length, Allocator.TempJob);
        for (int i = 0; i < zoneArray.Length; ++i)
            _currentResultArray[i] = _currentMeshDataArray[i].GetMeshDataArray()[0];

        BuildMeshPointsAndPositions(_currentMeshDataArray, out NativeArray<JobMeshDataKeys> keys,
                                                           out NativeArray<float2> points,
                                                           out NativeArray<float3> positions);

        var job = new CreateWorldMapMeshJob() {
            ZoneArray = zoneArray,
            NeighborArray = neighborArray,
            MeshDataKeys = keys,
            PointsArray = points,
            PositionsArray = positions,
            ResultArray = _currentResultArray,
            EmptyArray = new NativeArray<ushort>(0, Allocator.TempJob)
        };

        _currentJob = job.ScheduleBatch(zoneArray.Length, 32);
        StartCoroutine(WaitForJobToFinish(isEdge));
    }

    private IEnumerator WaitForJobToFinish(bool isEdge) {
        while (!_currentJob.IsCompleted)
            yield return null;

        _currentJob.Complete();

        try {
            if (isEdge) FinalizeEdgeMeshes();
            else FinalizeFloorMeshes();
        } catch (Exception ex) {
            Debug.LogError(ex.Message + "\n" + ex.StackTrace);

            if (!isEdge) CleanupAfterGeneratingMeshes();
        } finally {
            if (isEdge) { 
                CleanupAfterGeneratingMeshes();
                IsGenerating = false;
                GenerationTime = Time.realtimeSinceStartupAsDouble - GenerationTime;
            }
        }
    }

    private WorldMapMeshData CreateZoneFloorData(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;
        float2[] points = new float2[cornerCount];
        float3[] vertices = new float3[cornerCount];

        for (int i = 0; i < cornerCount; ++i) {
            points[i] = zone.Corners[i].Coord;
            vertices[i] = new float3(points[i].x, points[i].y, ExtrusionDepth);
        }

        WorldMapMeshData data = new WorldMapMeshData(zone, parent, transform, (Vector3) zone.Center, 
                                                     new NativeArray<float2>(points, Allocator.TempJob), 
                                                     new NativeArray<float3>(vertices, Allocator.TempJob), 
                                                     MeshDefaultMaterial, _helper);

        data.SetupFloorMesh(zone);

        return data;
    }

    private List<WorldMapMeshData> CreateZoneEdgesData(Zone zone, Transform parent) {
        int cornerCount = zone.Corners.Count;
        if (cornerCount == 0) return new();

        List<WorldMapMeshData> meshDataList = new List<WorldMapMeshData>();

        // generating edges all around
        for (int i = 0; i < cornerCount; ++i) {
            Corner cornerOne = zone.Corners[i];
            Corner cornerTwo = zone.Corners[(i + 1) % cornerCount];

            if (!_helper.CanProcessZoneEdge(cornerOne, cornerTwo)) continue;

            Zone neighbor = zone.Neighbors.Find(z => z.Corners.Contains(cornerOne) && z.Corners.Contains(cornerTwo));

            meshDataList.Add(CreateZoneEdgeData(zone, neighbor, i, 
                                                _helper.FindLeadingEdgeIndex(neighbor, cornerOne, cornerTwo),
                                                parent, cornerOne.Coord, cornerTwo.Coord));
        }

        return meshDataList;
    }

    private WorldMapMeshData CreateZoneEdgeData(Zone zone, Zone neighbor, int edgeIndex, int neighborEdgeIndex,
                                                Transform parent, float2 cornerOne, float2 cornerTwo) {
        float3[] positions = new float3[4] {
            new float3(cornerOne.x, cornerOne.y, -ExtrusionDepth), // front
            new float3(cornerOne.x, cornerOne.y, ExtrusionDepth), // back
            new float3(cornerTwo.x, cornerTwo.y, -ExtrusionDepth), // front
            new float3(cornerTwo.x, cornerTwo.y, ExtrusionDepth)  // back
        };

        float2[] points = new float2[] { cornerOne, cornerTwo };
        float3 center = ((cornerOne + cornerTwo) * 0.5f).ConvertTo3D();

        WorldMapMeshData data = new WorldMapMeshData(zone, parent, transform, center, 
                                                     new NativeArray<float2>(points, Allocator.TempJob), 
                                                     new NativeArray<float3>(positions, Allocator.TempJob), 
                                                     MeshDefaultMaterial, _helper);

        data.SetupEdgeMesh(zone, neighbor, edgeIndex, neighborEdgeIndex);

        return data;
    }

    private GameObject CreateMesh(WorldMapMeshData data) {
        float3 extrusionHeight = new float3(0, 0, ExtrusionHeight);
        extrusionHeight = RotateVertexToMatchParentRotation(extrusionHeight, data.GetTransform());

        // create mesh and load data into it
        Mesh mesh = new Mesh() { bounds = data.GetZoneInfo().Bounds };

        ApplyAndDisposeWritableMeshData(data.GetMeshDataArray(), mesh, MeshUpdateFlags.DontRecalculateBounds);

        GameObject meshObject = CreateGameObjectFromMesh(mesh, data.GetZoneInfo().Parent, data.GetZoneInfo().Name, data.GetMaterials());
        meshObject.transform.position += (Vector3) (data.GetGlobalRef() + extrusionHeight);

        data.Dispose();

        return meshObject;
    }

    private void BuildMeshPointsAndPositions(WorldMapMeshData[] dataArray, out NativeArray<JobMeshDataKeys> keys,
                                                                           out NativeArray<float2> points,
                                                                           out NativeArray<float3> positions) {
        keys = new NativeArray<JobMeshDataKeys>(dataArray.Length, Allocator.TempJob);
        List<float2> pointsList = new List<float2>();
        List<float3> positionsList = new List<float3>();

        for (int i = 0; i < dataArray.Length; ++i) {
            WorldMapMeshData data = dataArray[i];
            NativeArray<float2> dataPoints = data.GetPoints();
            NativeArray<float3> dataPositions = data.GetPositions();

            keys[i] = new JobMeshDataKeys {
                PointIndex = pointsList.Count,
                PointsLength = dataPoints.Length,
                PositionsIndex = positionsList.Count,
                PositionsLength = dataPositions.Length
            };

            pointsList.AddRange(dataPoints);
            positionsList.AddRange(dataPositions);
        }

        points = new NativeArray<float2>(pointsList.ToArray(), Allocator.TempJob);
        positions = new NativeArray<float3>(positionsList.ToArray(), Allocator.TempJob);
    }
    
    private void CleanupAfterGeneratingMeshes() {
        _currentMeshDataArray = null;

        if (_currentResultArray.IsCreated)
            _currentResultArray.Dispose();
    }

    public void OnDestroy() {
        if (ZoneTriangles.IsCreated)
            ZoneTriangles.Dispose();

        if (NeighborTriangles.IsCreated)
            NeighborTriangles.Dispose();

        if (FlatTriangles.IsCreated)
            FlatTriangles.Dispose();
    }
}
