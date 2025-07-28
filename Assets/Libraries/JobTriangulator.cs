using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true)]
public struct JobTriangulator {

	[ReadOnly]
	private NativeSlice<float2> m_points;

	public JobTriangulator(NativeSlice<float2> points) {
		m_points = points;
	}

	public NativeList<ushort> Triangulate(Allocator allocator) {
		var indices = new NativeList<ushort>(allocator);

		int n = m_points.Length;
		if(n < 3)
			return indices;

		NativeArray<int> V = new NativeArray<int>(n, allocator);

		bool isAreaPositive = Area() > 0.0f;
		for(int v = 0; v < n; ++v)
			V[v] = isAreaPositive ? v : (n - 1 - v);

		int nv = n;
		int count = 2 * nv;
		int vIndex = nv - 1;

		while(nv > 2) {
			if((count--) <= 0) {
				V.Dispose();
				return indices;
			}

			int u = vIndex;
			if(nv <= u) u = 0;

			vIndex = u + 1;
			if(nv <= vIndex) vIndex = 0;

			int w = vIndex + 1;
			if(nv <= w) w = 0;

			if(Snip(u, vIndex, w, nv, V)) {
				int a = V[u],
					b = V[vIndex],
					c = V[w];

				indices.Add((ushort) a);
				indices.Add((ushort) b);
				indices.Add((ushort) c);

				for(int s = vIndex, t = vIndex + 1; t < nv; ++s, ++t)
					V[s] = V[t];

				--nv;
				count = 2 * nv;
			}
		}

		// reverse to preserve original winding
		for(int i = 0, len = indices.Length; i < len / 2; ++i)
			(indices[len - 1 - i], indices[i]) = (indices[i], indices[len - 1 - i]);

		V.Dispose();
		return indices;
	}

	private float Area() {
		int n = m_points.Length;
		float A = 0.0f;

		for(int p = n - 1, q = 0; q < n; p = q++) {
			float2 pVal = m_points[p];
			float2 qVal = m_points[q];
			A += pVal.x * qVal.y - qVal.x * pVal.y;
		}
		return A * 0.5f;
	}

	private bool Snip(int u, int v, int w, int n, NativeArray<int> V) {
		float2 A = m_points[V[u]],
		       B = m_points[V[v]],
		       C = m_points[V[w]];

		if(math.abs(((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))) < math.EPSILON)
			return false;

		for(int p = 0; p < n; ++p) {
			if(p == u || p == v || p == w) continue;

			float2 P = m_points[V[p]];
			if(InsideTriangle(A, B, C, P))
				return false;
		}
		return true;
	}

	private bool InsideTriangle(float2 A, float2 B, float2 C, float2 P) {
		float ax = C.x - B.x, ay = C.y - B.y;
		float bx = A.x - C.x, by = A.y - C.y;
		float cx = B.x - A.x, cy = B.y - A.y;
		float apx = P.x - A.x, apy = P.y - A.y;
		float bpx = P.x - B.x, bpy = P.y - B.y;
		float cpx = P.x - C.x, cpy = P.y - C.y;

		float aCROSSbp = ax * bpy - ay * bpx;
		float cCROSSap = cx * apy - cy * apx;
		float bCROSScp = bx * cpy - by * cpx;

		return (aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f);
	}
} 