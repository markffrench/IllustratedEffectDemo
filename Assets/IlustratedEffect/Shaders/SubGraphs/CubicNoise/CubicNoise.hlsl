float random(float3 x) {
    return frac(sin(x.x + x.y * 57.0 + x.z * 113.0) * 43758.5453);
}

float interpolate(float a, float b, float c, float d, float x) {
    float p = (d - c) - (a - b);
    
    return x * (x * (x * p + ((a - b) - p)) + (c - a)) + b;
}

float sampleX(float3 at) {
    float floored = floor(at.x);
    
    return interpolate(
        random(float3(floored - 1.0, at.yz)),
        random(float3(floored, at.yz)),
        random(float3(floored + 1.0, at.yz)),
        random(float3(floored + 2.0, at.yz)),
    	frac(at.x)) * 0.5 + 0.25;
}

float sampleY(float3 at) {
    float floored = floor(at.y);
    
    return interpolate(
        sampleX(float3(at.x, floored - 1.0, at.z)),
        sampleX(float3(at.x, floored, at.z)),
        sampleX(float3(at.x, floored + 1.0, at.z)),
        sampleX(float3(at.x, floored + 2.0, at.z)),
        frac(at.y));
}

void cubicNoise_float(float3 at, out float output) {
    float floored = floor(at.z);
    
    output = interpolate(
        sampleY(float3(at.xy, floored - 1.0)),
        sampleY(float3(at.xy, floored)),
        sampleY(float3(at.xy, floored + 1.0)),
        sampleY(float3(at.xy, floored + 2.0)),
        frac(at.z));
}
