using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

internal class WorldMapMeshHelper {
	private class MeshEdge {
		public Corner CornerOne;
		public Corner CornerTwo;

		public override bool Equals(object obj) {
			if (obj is MeshEdge edge) 
				return CornerOne == edge.CornerOne && CornerTwo == edge.CornerTwo;
			else return false;
    	}

		public override int GetHashCode() => HashCode.Combine(CornerOne.GetHashCode(), CornerTwo.GetHashCode());
	}

	[StructLayout(LayoutKind.Sequential)]
    internal struct CustomVertex {
        public float3 position, normal;
        public half4 tangent;
        public half2 texCoord0;
        public half2 texCoord1;
    }

	internal static readonly VertexAttributeDescriptor[] Layout = {
		new VertexAttributeDescriptor(dimension: 3),
		new VertexAttributeDescriptor(VertexAttribute.Normal,  dimension: 3),
		new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
		new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
		new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 2)
	};

	private readonly World _world;
	private Dictionary<Zone, ZoneRoomWrapper> _zoneRoomWrappers;
	private readonly HashSet<MeshEdge> _edgesProcessed;

	internal WorldMapMeshHelper(World world) {
		_world = world;
		_zoneRoomWrappers = new Dictionary<Zone, ZoneRoomWrapper>();
		_edgesProcessed = new HashSet<MeshEdge>();
	}

	internal void Setup() {
		SetupRoomWrappers();
	}

	// this is needed to ensure consistency
	// when we generate an edge, we generate it on both sides at once
	// this means we need both zones at once, even before the other zone is processed here
	// so we need to pre-emptively set which index maps the zones will be using
	// otherwise we would be re-generating some at times and causing weirdness
	private void SetupRoomWrappers() {
        _zoneRoomWrappers = new Dictionary<Zone, ZoneRoomWrapper>();

        foreach (Zone zone in _world.Zones)
            if (zone.Room != null)
                _zoneRoomWrappers.Add(zone, new ZoneRoomWrapper() {
                    Room = zone.Room,
                    WallIndexMaps = new List<IndexMapWrapper>(),
                    FloorIndexMap = zone.Room.GetFloorIndexMap(),
                    MergeWallsTileSize = 0,
                    MergeWallsStartOffsets = new List<float>()
                });

        foreach (var pair in _zoneRoomWrappers) {
            int cornerCount = pair.Key.Corners.Count;
            IndexMapWrapper mergeWrapper = null;
            float perimLength = 0;
            List<float> edgeLengths = new List<float>();

            for (int i = 0; i < cornerCount; ++i) {
                if (pair.Value.Room.PickDifferentIndexMaps || i == 0) {
                    IndexMapWrapper picked = pair.Value.Room.GetWallIndexMap(i);

                    if (picked != null) {
                        pair.Value.WallIndexMaps.Add(picked);

                        if (pair.Value.Room.MergeWalls) mergeWrapper = picked;
                    }
                }

                if (pair.Value.Room.MergeWalls && mergeWrapper != null) {
                    Corner cornerOne = pair.Key.Corners[i];
                    Corner cornerTwo = pair.Key.Corners[(i + 1) % cornerCount];
                    float edgeLength = Vector2.Distance(cornerOne.Coord, cornerTwo.Coord);

                    edgeLengths.Add(edgeLength);
                    perimLength += edgeLength;
                }
            }

            // we flip here because everything displays right-to-left otherwise
            pair.Value.WallIndexMaps.Reverse();

            if (perimLength > 0)
                FillMergedWallsStartOffsets(pair.Value, mergeWrapper, perimLength, edgeLengths);
        }
	}

    private void FillMergedWallsStartOffsets(ZoneRoomWrapper wrapper, IndexMapWrapper mergeWrapper, 
                                             float perimLength, List<float> edgeLengths) {
        wrapper.MergeWallsTileSize = mergeWrapper.TextureWrapMode == IndexMapWrapper.WrapMode.Fit 
                                     ? perimLength / mergeWrapper.IndexMapTexture.width
                                     : mergeWrapper.DefaultTileSize;

        float currentOffset = 0;
        foreach (float edgeLength in edgeLengths) {
            currentOffset += edgeLength / wrapper.MergeWallsTileSize;
            // we flip here because everything displays right-to-left otherwise
            wrapper.MergeWallsStartOffsets.Add(perimLength / wrapper.MergeWallsTileSize - currentOffset);
        }
    }

	internal ZoneRoomWrapper GetZoneRoomWrapper(Zone zone) => 
		zone != null && _zoneRoomWrappers.ContainsKey(zone) ? _zoneRoomWrappers[zone] : null;

	internal bool CanProcessZoneEdge(Corner cornerOne, Corner cornerTwo) =>
        _edgesProcessed.Add(CreateMeshEdge(cornerOne, cornerTwo));

	internal int FindLeadingEdgeIndex(Zone zone, Corner cornerOne, Corner cornerTwo) {
		if (zone == null) return 0;

        int indexA = zone.Corners.IndexOf(cornerOne);
        int indexB = zone.Corners.IndexOf(cornerTwo);
        int cornerCount = zone.Corners.Count;

        // pick the index whose next vertex (mod cornerCount) is the other corner
        return (indexA + 1) % cornerCount == indexB ? indexA :
               (indexB + 1) % cornerCount == indexA ? indexB :
               Mathf.Min(indexA, indexB);            // fallback, should not happen
    }

	internal static float3 RotateVertexToMatchParentRotation(float3 vertex, Transform transform) => transform.rotation * vertex;

	internal static float3 TranslateVectorToLocal(float3 vec, float3 globalRef, Transform transform) => 
		RotateVertexToMatchParentRotation(vec, transform) - globalRef;

	internal static Bounds CalculateBounds(NativeArray<float3> positions) {
		Bounds bounds = new Bounds(positions[0], Vector3.zero);

		// grow bounds position by position
		for (int i = 1; i < positions.Length; ++i) 
			bounds.Encapsulate(positions[i]);

		return bounds;
    }

    [BurstCompile]
	internal static void CalculateNormals(NativeSlice<float3> positions, NativeArray<ushort> triangles, 
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
	internal static half2 CalculateUVs(bool isEdge, float3 vertex, UVModifiers mods) {
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

	internal static bool ShouldFlipUV(float3 a, float3 b, float3 interior) {
        // cross < 0 = interior on right side
        return ((b.x - a.x) * (interior.y - a.y) -
                (b.y - a.y) * (interior.x - a.x)) < 0;
    }

	private MeshEdge CreateMeshEdge(Corner a, Corner b) {
		bool isAFirst = a.GetHashCode() <= b.GetHashCode();
		return new MeshEdge { CornerOne = isAFirst ? a : b, CornerTwo = isAFirst ? b : a };
    }
}