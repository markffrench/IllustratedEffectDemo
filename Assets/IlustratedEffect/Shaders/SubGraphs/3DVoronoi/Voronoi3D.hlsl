//3D Voronoi referenced from https://www.shadertoy.com/view/flSGDK
float hash(float x) { return frac(x + 1.3215 * 1.8152); }

float hash3(float3 a) { return frac((hash(a.z * 42.8883) + hash(a.y * 36.9125) + hash(a.x * 65.4321)) * 291.1257); }

float3 rehash3(float x) { return float3(hash(((x + 0.5283) * 59.3829) * 274.3487), hash(((x + 0.8192) * 83.6621) * 345.3871), hash(((x + 0.2157f) * 36.6521f) * 458.3971f)); }

float sqr(float x) {return x*x;}
float fastdist(float3 a, float3 b) { return sqr(b.x - a.x) + sqr(b.y - a.y) + sqr(b.z - a.z); }

void voronoi3D_float(float3 pos, float density, out float Out, out float Cells, out float Gradient) {
	float4 p[27];
	pos *= density;
	float x = pos.x;
	float y = pos.y;
	float z = pos.z;
	for (int _x = -1; _x < 2; _x++) for (int _y = -1; _y < 2; _y++) for(int _z = -1; _z < 2; _z++) {
		float3 _p = float3(floor(x), floor(y), floor(z)) + float3(_x, _y, _z);
		float h = hash3(_p);
		p[(_x + 1) + ((_y + 1) * 3) + ((_z + 1) * 3 * 3)] = float4((rehash3(h) + _p).xyz, h);
	}
	float m1 = 9999.9999, m2 = 9999.9999, w = 0.0;
	for (int i = 0; i < 27; i++) {
		float d = fastdist(float3(x, y, z), p[i].xyz);
		if(d < m1) { 
			m2 = m1;
			m1 = d; 
			w = p[i].w;
		} else if(d < m2) {
			m2 = d;
		}
	}
	Out = m1;
	Cells = w;
	Gradient = m2 - m1;
}

float manhattanDistance(float3 p, float3 q) {
	return abs(p.x - q.x) + abs(p.y - q.y) + abs(p.z - q.z);
}

static float3 offsets[7] = {
	float3(0, 0, 0),  // Current cell
	float3(1, 0, 0),  // +X adjacent cell
	float3(-1, 0, 0), // -X adjacent cell
	float3(0, 1, 0),  // +Y adjacent cell
	float3(0, -1, 0), // -Y adjacent cell
	float3(0, 0, 1),  // +Z adjacent cell
	float3(0, 0, -1)  // -Z adjacent cell
};

void voronoi3D_fastGradient_float(float3 pos, float density, float mask, out float Out, out float Gradient) {
	if(mask <= 0.0001) {
		Out = 0.0;
		Gradient = 0.0;
		return;
	}

	pos *= density;
    
	float3 currentCell = floor(pos);

	float m1 = 9999.9999, m2 = 9999.9999;
	for (int i = 0; i < 7; i++) {
		float3 cellPosition = currentCell + offsets[i];
		float3 seedPosition = rehash3(hash3(cellPosition)) + cellPosition;
		float d = manhattanDistance(pos, seedPosition);
		if(d < m1) {
			m2 = m1;
			m1 = d;
		} else if(d < m2) {
			m2 = d;
		}
	}
    
	Out = m1*mask;
	Gradient = (m2 - m1)*mask;
}