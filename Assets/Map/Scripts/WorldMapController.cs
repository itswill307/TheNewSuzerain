using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class WorldMapController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Material  mapMat;      // morph shader material
    [SerializeField] Renderer  mapRenderer; // mesh renderer for bounds calculation

    [Header("World Geometry")]
    [SerializeField] float radius = 100f;   // must match shader

    [Header("Zoom")]
    [SerializeField] float zoomSpeed = 6f;           // zoom sensitivity
    [SerializeField] float zoomInBuffer = 0.5f;      // additional distance from mesh surface

    [Header("Panning")]
    [SerializeField] float panKeySpeed = 60f;        // degrees per second for keys
    
    [Header("Morph")]
    [SerializeField] float currentMorph = 0f;        // current morph value (0=flat, 1=sphere) - controlled by zoom
    [SerializeField] bool enableZoomMorph = true;    // enable automatic morph based on zoom level

    // ---------- private ----------
    Camera cam;
    InputSystem_Actions input;
    float mapWidth, mapHeight;
    float baseDistance;     // default distance to fit map width
    float minZoom, maxZoom; // zoom distance limits
    float currentZoom;      // current zoom distance
    float sphereRadius;
    
    // Panning state - hybrid approach for optimal visual quality
    float focusLon = 0f;    // longitude center (-180 to 180) - handled by UV offset only
    float cameraLat = 0f;   // camera latitude in degrees - handled by camera position + Z distance correction

    void Awake()
    {
        cam = GetComponent<Camera>();
        input = new InputSystem_Actions();
        
        // Calculate map dimensions
        mapWidth = 2f * Mathf.PI * radius;  // circumference
        mapHeight = Mathf.PI * radius;      // height from pole to pole
        
        // Calculate base camera distance and zoom limits
        CalculateZoomLimits();
        
        // Start at default view
        currentZoom = baseDistance;
        cameraLat = 0f;  // Ensure camera starts at equator
        
        // Initialize camera with identity rotation to avoid -180 Y rotation issue
        transform.localRotation = Quaternion.identity;

        sphereRadius = Mathf.Lerp(radius * 3.0f, radius, currentMorph);

        PositionCamera(sphereRadius);
        
        // Set up map for flat viewing
        SetupFlatMap();
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

    void CalculateZoomLimits()
    {
        // Calculate horizontal FOV from vertical FOV and aspect ratio
        float vFOV = cam.fieldOfView * Mathf.Deg2Rad;
        float hFOV = 2f * Mathf.Atan(Mathf.Tan(vFOV * 0.5f) * cam.aspect);

        // Base distance to fit map width exactly - this becomes our zoom out limit
        baseDistance = (mapWidth * 0.5f) / Mathf.Tan(hFOV * 0.5f);
        maxZoom = baseDistance;  // zoom out limit = map fills screen horizontally
        
        // Calculate zoom in limit based on actual mesh distance
        minZoom = CalculateMinZoomFromMesh();
    }

    float CalculateMinZoomFromMesh()
    {
        return radius * 0.01f; // Very close zoom - 1% of radius
    }

    void PositionCamera(float sphereRadius)
    {
        // Clamp camera latitude to valid range for shader calculations
        // Stay within the shader's valid range but allow close to poles
        float clampedLat = Mathf.Clamp(cameraLat, -89f, 89f); // dont need when bounds implemented

        // Calculate surface point and normal at current camera latitude
        Vector3 surfaceNormal = CalculateSurfaceNormalAtLatitude(clampedLat, sphereRadius);

        // Debug: print the current surface normal each frame (comment out if too noisy)
        Debug.Log($"Surface normal @ lat {clampedLat:F1}° : {surfaceNormal}");
        
        // Position camera radially outward from surface at zoom distance
        Vector3 cameraPosition = new Vector3(0, 0, sphereRadius) + surfaceNormal * (sphereRadius + currentZoom);
        cam.transform.localPosition = cameraPosition;
        
        // Camera looks toward the surface
        Vector3 cameraForward = -surfaceNormal;
        
        transform.localRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
    }
    
    Vector3 CalculateSurfaceNormalAtLatitude(float latitudeDegrees, float sphereRadius)
    {
        // Calculate the actual surface point at given latitude (same as shader math)
        
        // Convert camera latitude to UV coordinate, then to shader latitude
        // This ensures we match the shader's UV mapping exactly
        
        // Convert latitude degrees to UV.y coordinate (matching shader's range)
        // UV.y = 0.5 corresponds to latitude 0°
        // UV.y = 0.0 corresponds to latitude -90°  
        // UV.y = 1.0 corresponds to latitude +90°
        float uvY = (latitudeDegrees / 180f) + 0.5f;
        
        // Use shader's exact calculation: lat = (UV.y - 0.5) * PI
        float latitude = (uvY - 0.5f) * Mathf.PI;
        float longitude = 0f; // Camera always looks at center longitude
        
        // Sphere position at this latitude (same as shader)
        Vector3 sphereCenter = new Vector3(0, 0, sphereRadius);
        Vector3 spherePos = sphereCenter + sphereRadius * new Vector3(
            Mathf.Cos(latitude) * Mathf.Sin(longitude),     // = 0 (longitude = 0)
            Mathf.Sin(latitude),
            -Mathf.Cos(latitude) * Mathf.Cos(longitude)     // = -Cos(latitude)
        );
        
        Vector3 nPlane  = new Vector3(0, 0, -1);              // (0,0,1)
        Vector3 nSphere = (spherePos - sphereCenter).normalized;
        Vector3 surfaceNormal = Vector3.Lerp(nPlane, nSphere, currentMorph).normalized;

        Debug.Log($"Surface normal {surfaceNormal}");
;
        Debug.Log($"No lerp normal {nSphere}");

        // --- Geometric normal via analytic derivatives ---
        // Tangents of the flat plane (object-space)
        Vector3 dPlane_du = new Vector3( 2.0f * Mathf.PI * radius, 0.0f, 0.0f );
        Vector3 dPlane_dv = new Vector3( 0.0f, Mathf.PI * radius, 0.0f );

        // Tangents of the morphed sphere patch
        float  dLon_dU = (2.0f * Mathf.PI * radius) / sphereRadius; // ∂longitude / ∂u
        float  dLat_dV = Mathf.PI;                                // ∂latitude  / ∂v

        // ∂Psphere/∂u  (varying longitude only)
        Vector3 dSphere_du = sphereRadius * new Vector3(
            Mathf.Cos(latitude) * Mathf.Cos(longitude) * dLon_dU,   // X
            0.0f,                         // Y
            Mathf.Cos(latitude) * Mathf.Sin(longitude) * dLon_dU    // Z
        );

        // ∂Psphere/∂v  (varying latitude only)
        Vector3 dSphere_dv = sphereRadius * Mathf.PI * new Vector3(
        -Mathf.Sin(latitude) * Mathf.Sin(longitude),             // X
            Mathf.Cos(latitude),                      // Y
            Mathf.Sin(latitude) * Mathf.Cos(longitude)              // Z
        );

        // Blend plane and sphere tangents by Morph (same as position blend)
        Vector3 tangentU = Vector3.Lerp(dPlane_du, dSphere_du, currentMorph);
        Vector3 tangentV = Vector3.Lerp(dPlane_dv, dSphere_dv, currentMorph);

        Vector3 geoNormal = Vector3.Cross(tangentU, tangentV).normalized;

        // Ensure normal points outward (same hemisphere as sphere normal)
        if (Vector3.Dot(geoNormal, nSphere) < 0.0) geoNormal = -geoNormal;

        Debug.Log($"Geometric normal {geoNormal}");

        //return surfaceNormal;
        return geoNormal;
    }

    void SetupFlatMap()
    {
        // Morph is now controlled by zoom level, just update UV offset
        if (mapMat != null)
        {
            UpdateUVOffset();
        }
    }

    void Update()
    {
        // Get scroll input for zooming
        float scroll = input.Map.Zoom.ReadValue<float>();
        
        // Apply zoom
        currentZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, minZoom, maxZoom);
        
        // Calculate morph based on zoom level (if enabled)
        if (enableZoomMorph)
        {
            // When zoomed out (currentZoom = maxZoom), morph = 0 (flat)
            // When zoomed in (currentZoom = minZoom), morph = 1 (sphere)
            float zoomRange = maxZoom - minZoom;
            if (zoomRange > 0) // Avoid division by zero
            {
                float normalizedZoom = (maxZoom - currentZoom) / zoomRange; // 0 at max zoom, 1 at min zoom
                currentMorph = Mathf.Clamp01(normalizedZoom);
            }
        }
        
        // Apply morph to material
        if (mapMat != null)
        {
            mapMat.SetFloat("_Morph", currentMorph);
        }

        sphereRadius = Mathf.Lerp(radius * 3.0f, radius, currentMorph);

        // Update camera position
        PositionCamera(sphereRadius);
        
        // Get panning input
        Vector2 moveKeys = input.Map.Move.ReadValue<Vector2>();
        Vector2 dragPan = input.Map.DragPan.ReadValue<Vector2>();
        
        // Calculate how much world space one pixel represents at current zoom
        // Use absolute currentZoom value, not camera position which moves with panning
        float vFOV = cam.fieldOfView * Mathf.Deg2Rad;
        float hFOV = 2f * Mathf.Atan(Mathf.Tan(vFOV * 0.5f) * cam.aspect);
        float worldUnitsPerPixelX = (2f * currentZoom * Mathf.Tan(hFOV * 0.5f)) / Screen.width;
        float worldUnitsPerPixelY = (2f * currentZoom * Mathf.Tan(vFOV * 0.5f)) / Screen.height;
        
        // Convert world units to degrees for consistent panning
        float degreesPerPixelX = (worldUnitsPerPixelX / mapWidth) * 360f;
        float degreesPerPixelY = (worldUnitsPerPixelY / mapHeight) * 180f;

        // Calculate panning from keys (WASD/arrows) and mouse drag
        float panLon = (moveKeys.x * panKeySpeed * Time.deltaTime) + (-dragPan.x * degreesPerPixelX);
        float panLat = (moveKeys.y * panKeySpeed * Time.deltaTime) + (-dragPan.y * degreesPerPixelY);
        
        // Apply horizontal panning with UV offset (infinite scrolling)
        focusLon = Mathf.Repeat(focusLon + panLon, 360f);  // Wrap around horizontally
        
        // Apply vertical panning with camera movement (no limits)
        cameraLat += panLat;  // No wrapping - infinite vertical movement
        
        // Update UV offset for panning
        UpdateUVOffset();
    }

    void UpdateUVOffset()
    {
        if (mapMat == null) return;

        // Set UV offset for horizontal panning only (vertical panning uses camera movement)
        float uvOffsetX = focusLon / 360f;
        float uvOffsetY = 0f;  // No vertical UV offset - camera handles vertical movement
        
        // Set UV offset: only X for horizontal infinite panning
        mapMat.SetVector("_UVOffset", new Vector2(uvOffsetX, uvOffsetY));
        
        // No mesh movement - texture panning handled purely by UV offset
    }

    // Recalculate if camera settings change
    void OnValidate()
    {
        if (Application.isPlaying && cam != null)
        {
            mapWidth = 2f * Mathf.PI * radius;
            mapHeight = Mathf.PI * radius;
            CalculateZoomLimits();
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            
            // Recalculate morph based on current zoom level (if enabled)
            if (enableZoomMorph && maxZoom > minZoom) // Avoid division by zero
            {
                float zoomRange = maxZoom - minZoom;
                float normalizedZoom = (maxZoom - currentZoom) / zoomRange;
                currentMorph = Mathf.Clamp01(normalizedZoom);
            }
            
            // Always apply current morph value to material
            if (mapMat != null)
            {
                mapMat.SetFloat("_Morph", currentMorph);
            }
            
            PositionCamera(sphereRadius);
            UpdateUVOffset();
        }
    }
}
