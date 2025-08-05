// PlaneSphereMorph.hlsl
#ifndef PLANE_SPHERE_MORPH_HLSL
#define PLANE_SPHERE_MORPH_HLSL

//–– Constants ––
// full π and half-π for lon/lat conversion
static const float PI_      = 3.14159265359;
static const float HALF_PI_ = 1.57079632679;

//––
// In Shader Graph Custom Function node (File mode):
//   • Name:        PlaneSphereMorph     ← no “_float” suffix here
//   • Function:    PlaneSphereMorph_float
//   • Inputs:      UV (Vector2), Radius (Float), Morph (Float)
//   • Outputs:     OutPosition (Vector3), OutNormal (Vector3)
// Note: the “_float” suffix matches Shader Graph’s precision-suffix rules. :contentReference[oaicite:0]{index=0}
void PlaneSphereMorph_float(
    float2 UV,
    float  Radius,
    float  Morph,
    out float3 OutPosition,
    out float3 OutNormal)
{
    // Convert UV to longitude and latitude with proper orientation
    // Keep center of mesh facing forward, but make convex instead of concave
    float lon = (UV.x - 0.5) * (2.0 * PI_) - HALF_PI_;  // Offset to keep center forward
    float lat = (UV.y - 0.5) * PI_;

    // Trig helpers
    float cosLat = cos(lat);
    float sinLat = sin(lat);
    float cosLon = cos(lon);
    float sinLon = sin(lon);

    // Single-phase morph: flat rectangle → sphere (with simultaneous longitude and latitude curving)
    // Center of texture (UV 0.5, 0.5) stays at world origin (0, 0, 0)
    
    // Sphere radius decreases during morph - mesh maintains original size
    // This creates partial coverage of smaller spheres (no lateral distortion)
    float sphereRadius = lerp(Radius * 3.0, Radius, Morph);
    
    // Flat plane position - center texture at origin
    // Maintain 2:1 aspect ratio: X = 2π*Radius, Y = π*Radius  
    float3 planePos = float3(
        (UV.x - 0.5) * (2.0 * PI_ * Radius),    // X: 2π*Radius wide
        (UV.y - 0.5) * (PI_ * Radius),          // Y: π*Radius tall (2:1 ratio)
        0.0
    );
    
    // Single-phase morph: flat → sphere (longitude and latitude curve simultaneously)
    // Mesh maintains original size, maps to partial coverage of decreasing sphere
    
    // Calculate angles based on maintaining arc length = flat distance (no lateral distortion)
    // Arc length = angle × sphereRadius, Flat distance = (UV.x - 0.5) × 2π × Radius
    // For no distortion: angle × sphereRadius = (UV.x - 0.5) × 2π × Radius
    float longitude = (UV.x - 0.5) * (2.0 * PI_ * Radius) / sphereRadius;
    float latitude = (UV.y - 0.5) * PI_; // -π/2 to +π/2
    
    // Sphere center positioned so front face stays at Z=0
    float3 sphereCenter = float3(0, 0, sphereRadius);
    float3 spherePos = sphereCenter + sphereRadius * float3(
        cos(latitude) * sin(longitude),        // X: longitude wrapping
        sin(latitude),                         // Y: latitude curving  
        -cos(latitude) * cos(longitude)        // Z: depth (curves away from camera = concave)
    );
    
    // Single-phase transition: flat → sphere (both longitude and latitude curve together)
    OutPosition = lerp(planePos, spherePos, Morph);

    // --- Geometric normal via analytic derivatives ---
    // Tangents of the flat plane (object-space)
    float3 dPlane_du = float3( 2.0 * PI_ * Radius, 0.0, 0.0 );
    float3 dPlane_dv = float3( 0.0, PI_ * Radius, 0.0 );

    // Tangents of the morphed sphere patch
    float  dLon_dU = (2.0 * PI_ * Radius) / sphereRadius; // ∂longitude / ∂u
    float  dLat_dV = PI_;                                // ∂latitude  / ∂v

    // ∂Psphere/∂u  (varying longitude only)
    float3 dSphere_du = sphereRadius * float3(
        cosLat * cosLon * dLon_dU,   // X
        0.0,                         // Y
        cosLat * sinLon * dLon_dU    // Z
    );

    // ∂Psphere/∂v  (varying latitude only)
    float3 dSphere_dv = sphereRadius * PI_ * float3(
       -sinLat * sinLon,             // X
        cosLat,                      // Y
        sinLat * cosLon              // Z
    );

    // Blend plane and sphere tangents by Morph (same as position blend)
    float3 tangentU = lerp(dPlane_du, dSphere_du, Morph);
    float3 tangentV = lerp(dPlane_dv, dSphere_dv, Morph);

    float3 geoNormal = normalize(cross(tangentU, tangentV));

    // Ensure normal points outward (same hemisphere as sphere normal)
    float3 nSphere   = normalize(spherePos - sphereCenter);
    if (dot(geoNormal, nSphere) < 0.0) geoNormal = -geoNormal;

    OutNormal = geoNormal;
}

#endif // PLANE_SPHERE_MORPH_HLSL
