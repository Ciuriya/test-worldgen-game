using Unity.Burst;
using Unity.Mathematics;

public static class FloatExtensions {
	public static float3 ConvertTo3D(this float2 vec) => new float3(vec.x, vec.y, 0);
}
