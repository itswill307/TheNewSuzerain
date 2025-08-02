// PlaneSphereMorph.hlsl  – SAFE VERSION
#ifndef PLANE_SPHERE_MORPH_INCLUDED
#define PLANE_SPHERE_MORPH_INCLUDED

void PlaneSphereMorph_float(float2 uv, float Radius, float Morph, out float3 outPos, out float3 outNorm)
{
    const float _PI = 3.14159265359;          // <- renamed

    float lon = (uv.x - 0.5) * (2.0 * _PI);   // use _PI
    float lat = (uv.y - 0.5) *  _PI;

    float3 sphere = Radius * float3(cos(lat) * cos(lon), sin(lat), cos(lat) * sin(lon));

    float3 plane  = float3((uv.x - 0.5) * (2.0 * _PI * Radius), (uv.y - 0.5) * (_PI * Radius), 0.0);

    outPos = lerp(plane, sphere, Morph);

    float3 nPlane  = float3(0,0,1);
    float3 nSphere = normalize(sphere);
    outNorm = normalize(lerp(nPlane, nSphere, Morph));
}

#endif // PLANE_SPHERE_MORPH_INCLUDED
