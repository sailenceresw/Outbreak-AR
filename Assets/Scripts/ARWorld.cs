using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Ties the game to the real room: depth occlusion hides zombies behind real furniture and walls,
// and the floor height comes from the detected floor plane instead of a fixed guess.
public class ARWorld : MonoBehaviour
{
    // used until a floor plane is found: roughly a phone held at chest height
    public const float DefaultCameraHeight = 1.25f;

    public static ARWorld Instance { get; private set; }

    // world-space height of the real floor
    public static float FloorY
    {
        get
        {
            if (Instance != null && Instance.hasFloor) return Instance.floorY;
            Camera camera = Camera.main;
            return camera != null ? camera.transform.position.y - DefaultCameraHeight : 0f;
        }
    }

    public static bool HasRealFloor => Instance != null && Instance.hasFloor;
    public static bool OcclusionActive => Instance != null && Instance.occlusion != null && Instance.occlusion.enabled
        && Instance.occlusion.currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled;

    ARPlaneManager planes;
    AROcclusionManager occlusion;
    Camera arCamera;
    bool hasFloor;
    float floorY;

    public static ARWorld Attach(Camera camera)
    {
        if (camera == null) return null;
        ARWorld world = camera.gameObject.AddComponent<ARWorld>();
        world.arCamera = camera;
        Instance = world;
        world.Setup();
        return world;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Setup()
    {
        planes = FindAnyObjectByType<ARPlaneManager>();

        // environment depth: ARCore estimates the distance to every real surface, and the camera
        // background writes it into the depth buffer so virtual objects behind real ones are hidden
        occlusion = arCamera.GetComponent<AROcclusionManager>();
        if (occlusion == null) occlusion = arCamera.gameObject.AddComponent<AROcclusionManager>();
        occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
        occlusion.environmentDepthTemporalSmoothingRequested = true;
        occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
    }

    void Update()
    {
        UpdateFloor();
        ReportOnce();
    }

    bool depthReported;
    bool floorReported;

    // one line each in the device log, so occlusion and floor detection can be confirmed on a phone
    void ReportOnce()
    {
        if (!depthReported && occlusion != null && occlusion.descriptor != null && Time.time > 5f)
        {
            depthReported = true;
            Debug.Log("OutbreakAR: environment depth supported=" + occlusion.descriptor.environmentDepthImageSupported +
                " requested=" + occlusion.requestedEnvironmentDepthMode + " current=" + occlusion.currentEnvironmentDepthMode);
        }
        if (!floorReported && hasFloor)
        {
            floorReported = true;
            Debug.Log("OutbreakAR: real floor found " + (arCamera.transform.position.y - floorY).ToString("0.00") + " m below the phone");
        }
    }

    // true when a point (any height) stands over real floor ARCore has mapped; also true before any
    // floor is known, so spawning still works in an unscanned room
    public static bool IsOverFloor(Vector3 point)
    {
        if (Instance == null || Instance.planes == null || !Instance.hasFloor) return true;
        foreach (ARPlane plane in Instance.planes.trackables)
        {
            if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
            if (plane.trackingState != TrackingState.Tracking) continue;
            // only floor-level planes, not tables or beds
            if (Mathf.Abs(plane.center.y - Instance.floorY) > 0.2f) continue;
            Vector3 local = plane.transform.InverseTransformPoint(point);
            if (InsidePolygon(new Vector2(local.x, local.z), plane.boundary)) return true;
        }
        return false;
    }

    // even-odd test against a plane's boundary, which ARCore gives in the plane's own XZ space
    static bool InsidePolygon(Vector2 point, Unity.Collections.NativeArray<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // the lowest large horizontal plane below the camera is the floor
    void UpdateFloor()
    {
        if (planes == null || arCamera == null) return;

        float cameraY = arCamera.transform.position.y;
        float best = float.MaxValue;
        bool found = false;
        foreach (ARPlane plane in planes.trackables)
        {
            if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
            if (plane.trackingState != TrackingState.Tracking) continue;
            float y = plane.center.y;
            // tables and beds are higher up; the floor is clearly below the phone (held low, or seated)
            if (cameraY - y < 0.4f) continue;
            // ARCore starts planes small and grows them, so accept a modest patch
            if (plane.size.x * plane.size.y < 0.08f) continue;
            if (y < best)
            {
                best = y;
                found = true;
            }
        }

        if (!found) return;
        // ease in, so zombies never pop up or down when the plane estimate shifts
        floorY = hasFloor ? Mathf.Lerp(floorY, best, Time.deltaTime * 2f) : best;
        hasFloor = true;
    }
}
