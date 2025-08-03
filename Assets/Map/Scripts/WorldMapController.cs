using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class WorldMapController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Material  mapMat;      // morph shader material
    [SerializeField] Renderer  mapRenderer; // the actual map mesh renderer

    [Header("World Geometry")]
    [SerializeField] float radius = 100f;   // must match shader

    [Header("Zoom")]
    [SerializeField] float zoomSpeed = 6f;           // zoom sensitivity
    [SerializeField] float zoomInBuffer = 0.5f;      // additional distance from mesh surface

    [Header("Panning")]
    [SerializeField] float panKeySpeed = 60f;        // degrees per second for keys

    // ---------- private ----------
    Camera cam;
    InputSystem_Actions input;
    float mapWidth, mapHeight;
    float baseDistance;     // default distance to fit map width
    float minZoom, maxZoom; // zoom distance limits
    float currentZoom;      // current zoom distance
    
    // Panning state
    float focusLon = 0f;    // longitude center (-180 to 180)
    float focusLat = 0f;    // latitude center (limited by zoom)

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
        PositionCamera();
        
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
        if (mapRenderer == null)
        {
            // Fallback to radius-based calculation if no renderer assigned
            Debug.LogWarning("No map renderer assigned. Using radius-based zoom limit.");
            return radius * 1.1f;
        }

        // Raycast from camera position forward to find actual mesh distance
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.GetComponent<Renderer>() == mapRenderer)
            {
                return hit.distance + zoomInBuffer;
            }
        }
        
        // Fallback: use mesh bounds if raycast doesn't work
        Bounds bounds = mapRenderer.bounds;
        Vector3 closestPoint = bounds.ClosestPoint(transform.position);
        float distanceToMesh = Vector3.Distance(transform.position, closestPoint);
        
        return Mathf.Max(distanceToMesh + zoomInBuffer, 1f); // minimum 1 unit
    }

    void PositionCamera()
    {
        // Convert latitude to world Y position for vertical camera movement
        float worldY = (focusLat / 180f) * mapHeight;
        
        // Position camera: X stays centered, Y follows focus, Z for zoom
        cam.transform.localPosition = new Vector3(0f, worldY, -currentZoom);
        
        // Face the camera straight at the map
        transform.localRotation = Quaternion.identity;
    }

    void SetupFlatMap()
    {
        // Set morph to fully flat
        if (mapMat != null)
        {
            mapMat.SetFloat("_Morph", 0f);
            UpdateUVOffset();
        }
    }

    void Update()
    {
        // Get scroll input for zooming
        float scroll = input.Map.Zoom.ReadValue<float>();
        
        // Apply zoom
        currentZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, minZoom, maxZoom);
        
        // Update camera position
        PositionCamera();
        
        // Get panning input
        Vector2 moveKeys = input.Map.Move.ReadValue<Vector2>();
        Vector2 dragPan = input.Map.DragPan.ReadValue<Vector2>();
        
        // Calculate how much world space one pixel represents at current zoom
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
        
        // Apply panning with limits
        focusLon = Mathf.Repeat(focusLon + panLon, 360f);  // Wrap around horizontally
        
        // Calculate vertical limits based on current zoom level
        float maxLatOffset = CalculateMaxLatitudeOffset();
        focusLat = Mathf.Clamp(focusLat + panLat, -maxLatOffset, maxLatOffset);
        
        // Update horizontal UV offset for panning
        UpdateUVOffset();
    }

    void UpdateUVOffset()
    {
        if (mapMat == null) return;

        // Only use UV offset for horizontal panning (infinite scrolling)
        float uvOffsetX = focusLon / 360f;
        
        // Set UV offset: X for horizontal panning, Y always 0
        mapMat.SetVector("_UVOffset", new Vector2(uvOffsetX, 0f));
    }

    float CalculateMaxLatitudeOffset()
    {
        // Calculate how much of the map height is visible at current zoom
        float vFOV = cam.fieldOfView * Mathf.Deg2Rad;
        float visibleHeight = 2f * currentZoom * Mathf.Tan(vFOV * 0.5f);
        
        // If the visible height is greater than or equal to the map height,
        // we can see the entire map vertically, so no panning limits needed
        if (visibleHeight >= mapHeight)
            return 0f;
        
        // Calculate how much we can pan before the edge of the map becomes visible
        // The camera can move until the edge of the visible area reaches the map edge
        float maxWorldOffset = (mapHeight - visibleHeight) * 0.5f;
        
        // Convert from world units to degrees (latitude ranges from -90 to +90)
        float maxLatitudeOffset = (maxWorldOffset / mapHeight) * 180f;
        
        return maxLatitudeOffset;
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
            PositionCamera();
            UpdateUVOffset();
        }
    }
}
