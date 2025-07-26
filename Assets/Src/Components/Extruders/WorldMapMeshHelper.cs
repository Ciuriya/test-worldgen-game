using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldMapMeshHelper {
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
    public struct CustomVertex {
        public float3 position, normal;
        public half4 tangent;
        public half2 texCoord0;
        public half2 texCoord1;
    }

	private static readonly VertexAttributeDescriptor[] _layout = {
		new VertexAttributeDescriptor(dimension: 3),
		new VertexAttributeDescriptor(VertexAttribute.Normal,  dimension: 3),
		new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
		new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
		new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 2)
	};

	private readonly World _world;
	private Dictionary<Zone, ZoneRoomWrapper> _zoneRoomWrappers;
	private readonly HashSet<MeshEdge> _edgesProcessed;

	public WorldMapMeshHelper(World world) {
		_world = world;
		_zoneRoomWrappers = new Dictionary<Zone, ZoneRoomWrapper>();
		_edgesProcessed = new HashSet<MeshEdge>();
	}

	public void Setup() {
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
                    FloorIndexMap = zone.Room.GetFloorIndexMap()
                });

        foreach (Zone zone in _zoneRoomWrappers.Keys) {
            ZoneRoomWrapper wrapper = _zoneRoomWrappers[zone];
            int cornerCount = zone.Corners.Count;

            for (int i = 0; i < cornerCount; ++i)
                if ((i > 0 && wrapper.Room.PickDifferentIndexMaps) || i == 0) {
                    IndexMapWrapper picked = wrapper.Room.GetWallIndexMap(i);

                    if (picked != null) wrapper.WallIndexMaps.Add(picked);
                }
        }
	}

	public ZoneRoomWrapper GetZoneRoomWrapper(Zone zone) => 
		zone != null && _zoneRoomWrappers.ContainsKey(zone) ? _zoneRoomWrappers[zone] : null;

	public bool CanProcessZoneEdge(Corner cornerOne, Corner cornerTwo) {
		MeshEdge edge = CreateMeshEdge(cornerOne, cornerTwo);

		return _edgesProcessed.Add(edge);
	}

	public int FindLeadingEdgeIndex(Zone zone, Corner cornerOne, Corner cornerTwo) {
		if (zone == null) return 0;

        int indexA = zone.Corners.IndexOf(cornerOne);
        int indexB = zone.Corners.IndexOf(cornerTwo);
        int cornerCount = zone.Corners.Count;

        // pick the index whose next vertex (mod cornerCount) is the other corner
        return (indexA + 1) % cornerCount == indexB ? indexA :
               (indexB + 1) % cornerCount == indexA ? indexB :
               Mathf.Min(indexA, indexB);            // fallback, should not happen
    }

	public Vector3 RotateVertexToMatchParentRotation(Vector3 vertex, Transform transform) => transform.rotation * vertex;

	public Vector3 TranslateVectorToLocal(Vector3 vec, Vector3 globalRef, Transform transform) => 
		RotateVertexToMatchParentRotation(vec, transform) - globalRef;

	public NativeArray<VertexAttributeDescriptor> GetVertexLayout() =>
		new NativeArray<VertexAttributeDescriptor>(_layout, Allocator.Temp);

	public Bounds CalculateBounds(Vector3[] positions) {
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

	public void CalculateNormals(Vector3[] positions, ushort[] triangles, out Vector3 normal, out half4 tangent) {
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

	public half2 CalculateUVs(bool isEdge, float3 vertex, UVModifiers mods) {
		vertex += mods.UVOffset;
        float uValue = vertex.x;

        if (isEdge) {
            float length = Vector2.Distance(new Vector2(mods.PointOne.x, mods.PointOne.z),
                                            new Vector2(mods.PointTwo.x, mods.PointTwo.z));

            uValue = Vector2.Distance(new Vector2(vertex.x, vertex.z),
                                      new Vector2(mods.PointOne.x, mods.PointOne.z));

            if (mods.FlipUV) uValue = length - uValue;
        }

        return new half2(
            new half(uValue / mods.TileSize.x),
            new half((isEdge ? vertex.y : vertex.z) / mods.TileSize.y)
        );
    }

	public bool ShouldFlipUV(Vector2 a, Vector2 b, Vector2 interior) {
        // cross < 0 = interior on right side
        return ((b.x - a.x) * (interior.y - a.y) -
                (b.y - a.y) * (interior.x - a.x)) < 0;
    }

	public void Empty() {
		_zoneRoomWrappers.Clear();
		_edgesProcessed.Clear();
	}

	private MeshEdge CreateMeshEdge(Corner a, Corner b) {
		bool isAFirst = a.GetHashCode() <= b.GetHashCode();
		return new MeshEdge { CornerOne = isAFirst ? a : b, CornerTwo = isAFirst ? b : a };
    }
}