using Unity.Mathematics;
using UnityEngine;

public static class VectorExtensions {
	public static float2 ConvertToUnmanaged(this Vector2 vec) => new float2(vec.x, vec.y);
	public static float3 ConvertToUnmanaged(this Vector3 vec) => new float3(vec.x, vec.y, vec.z);
}