using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static PendingName.Extruders.WorldMap.WorldMapMeshHelper;
using static UnityEngine.Mesh;

namespace PendingName.Extruders.WorldMap {

    [Serializable]
    [BurstCompile]
    internal struct JobMeshDataKeys {
        public int PointIndex;
        public int PointsLength;
        public int PositionsIndex;
        public int PositionsLength;
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
    internal struct CreateWorldMapMeshJob : IJobParallelForBatch {

        [ReadOnly] internal NativeArray<ushort> ZoneTriangles;

        [ReadOnly] internal NativeArray<ushort> NeighborTriangles;

        [ReadOnly] internal NativeArray<ushort> FlatTriangles;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        internal NativeArray<JobMeshInfo> ZoneArray;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        internal NativeArray<JobMeshInfo> NeighborArray;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        internal NativeArray<JobMeshDataKeys> MeshDataKeys;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        internal NativeArray<float2> PointsArray;

        [DeallocateOnJobCompletion]
        [ReadOnly]
        internal NativeArray<float3> PositionsArray;

        internal NativeArray<MeshData> ResultArray;

        [DeallocateOnJobCompletion][ReadOnly] internal NativeArray<ushort> EmptyArray;

        [BurstCompile]
        public void Execute(int startIndex, int count) {
            for (int i = 0; i < count; ++i)
                Execute(startIndex + i);
        }

        [BurstCompile]
        public void Execute(int index) {
            JobMeshInfo zoneInfo = ZoneArray[index];
            JobMeshInfo neighborInfo = NeighborArray[index];
            MeshData meshData = ResultArray[index];
            JobMeshDataKeys key = MeshDataKeys[index];
            NativeSlice<float2> points = new NativeSlice<float2>(PointsArray, key.PointIndex, key.PointsLength);
            NativeSlice<float3> positions = new NativeSlice<float3>(PositionsArray, key.PositionsIndex, key.PositionsLength);

            if (points.Length == 0 || positions.Length == 0) return;

            // compute triangles if needed
            SetTriangles(zoneInfo, neighborInfo, points, out NativeArray<ushort> flatTriangles,
                                                         out NativeArray<ushort> zoneTriangles,
                                                         out NativeArray<ushort> neighborTriangles);

            // calc normals
            // only using the first sub-mesh for normal calc
            CalculateNormals(positions,
                             zoneTriangles,
                             out float3 normal, out half4 tangent);

            // build vertices
            NativeArray<CustomVertex> vertices = meshData.GetVertexData<CustomVertex>();
            BuildVertices(zoneInfo, neighborInfo, positions, ref vertices, new CustomVertex() {
                normal = normal,
                tangent = tangent
            });

            // load triangles into mesh
            meshData.SetIndexBufferParams(flatTriangles.Length, IndexFormat.UInt16);
            NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();

            triangleIndices.CopyFrom(flatTriangles);

            // setup sub-meshes
            SetupSubMeshes(ref meshData, zoneInfo.IsEdge && neighborInfo.IsValid, zoneTriangles, neighborTriangles);
        }

        [BurstCompile]
        private void SetTriangles(JobMeshInfo zoneInfo, JobMeshInfo neighborInfo,
                                  NativeSlice<float2> points,
                                  out NativeArray<ushort> flatTriangles, out NativeArray<ushort> zoneTriangles,
                                  out NativeArray<ushort> neighborTriangles) {
            if (!zoneInfo.IsEdge) {
                JobTriangulator triangulator = new JobTriangulator(points);

                flatTriangles = triangulator.Triangulate(Allocator.Temp).AsArray();
                zoneTriangles = flatTriangles;
                neighborTriangles = EmptyArray;

                return;
            }

            flatTriangles = FlatTriangles;
            zoneTriangles = ZoneTriangles;
            neighborTriangles = neighborInfo.IsValid ? NeighborTriangles : EmptyArray;
        }

        [BurstCompile]
        private void BuildVertices(JobMeshInfo zoneInfo, JobMeshInfo neighborInfo,
                                   NativeSlice<float3> positions,
                                   ref NativeArray<CustomVertex> vertices, CustomVertex vertex) {
            for (int i = 0; i < positions.Length; ++i) {
                vertex.position = positions[i];
                vertex.texCoord0 = CalculateUVs(zoneInfo.IsEdge, vertex.position, zoneInfo.UVModifiers);

                if (zoneInfo.IsEdge && neighborInfo.IsValid)
                    vertex.texCoord1 = CalculateUVs(true, vertex.position, neighborInfo.UVModifiers);

                vertices[i] = vertex;
            }
        }

        [BurstCompile]
        private void SetupSubMeshes(ref MeshData meshData, bool isValidEdge,
                                    NativeArray<ushort> zoneTriangles, NativeArray<ushort> neighborTriangles) {
            meshData.subMeshCount = isValidEdge ? 2 : 1;

            int triangleIndex = 0;
            for (int i = 0; i < meshData.subMeshCount; ++i) {
                // setup tris
                int subMeshTrianglesLength = (i == 0 ? zoneTriangles : neighborTriangles).Length;

                meshData.SetSubMesh(i, new SubMeshDescriptor(triangleIndex, subMeshTrianglesLength));
                triangleIndex += subMeshTrianglesLength;
            }
        }

        [BurstCompile]
        internal void CalculateNormals(NativeSlice<float3> positions, NativeArray<ushort> triangles,
                                              out float3 normal, out half4 tangent) {
            float3 nSum = float3.zero;
            float3 tSum = float3.zero;

            for (int i = 0; i < triangles.Length; i += 3) {
                float3 p0 = positions[triangles[i]];
                float3 p1 = positions[triangles[i + 1]];
                float3 p2 = positions[triangles[i + 2]];

                // area-weighted face normal
                float3 face = math.cross(p1 - p0, p2 - p0);
                nSum += face;

                // use the first edge as a tangent candidate
                tSum += p1 - p0;
            }

            normal = math.normalize(nSum);

            float3 tan3 = math.normalize(math.project(tSum, normal));
            tangent = new half4(new float4(tan3, 1f)); // −1 to flip
        }

        [BurstCompile]
        internal half2 CalculateUVs(bool isEdge, float3 vertex, UVModifiers mods) {
            vertex += mods.UVOffset;
            float uValue = vertex.x;

            if (isEdge) {
                float length = math.distance(new float2(mods.PointOne.x, mods.PointOne.z),
                                             new float2(mods.PointTwo.x, mods.PointTwo.z));

                uValue = math.distance(new float2(vertex.x, vertex.z),
                                       new float2(mods.PointOne.x, mods.PointOne.z));

                if (mods.FlipUV) uValue = length - uValue;
            }

            return new half2(
                new half((uValue / mods.TileSize.x) + mods.StartOffset),
                new half((isEdge ? vertex.y : vertex.z) / mods.TileSize.y)
            );
        }
    }
}