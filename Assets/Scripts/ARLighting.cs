using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

// Lights the virtual scene like the real room: ARCore's light estimate drives the main light and
// ambient light, and a transparent "shadow catcher" floor lets zombies cast shadows onto the real floor.
public class ARLighting : MonoBehaviour
{
    const float ShadowDistance = 12f;
    const float FloorSize = 40f;

    ARCameraManager cameraManager;
    Light mainLight;
    Transform floor;
    float targetIntensity = 1f;
    Color targetColor = Color.white;
    Quaternion targetRotation;
    bool hasDirection;

    public static ARLighting Attach(Camera arCamera, float floorBelowCamera)
    {
        if (arCamera == null) return null;
        ARLighting lighting = arCamera.gameObject.AddComponent<ARLighting>();
        lighting.Setup(arCamera, floorBelowCamera);
        return lighting;
    }

    void Setup(Camera arCamera, float floorBelowCamera)
    {
        cameraManager = arCamera.GetComponent<ARCameraManager>();
        mainLight = FindMainLight();
        if (mainLight != null)
        {
            mainLight.shadows = LightShadows.Soft;
            mainLight.shadowStrength = 1f;
            targetRotation = mainLight.transform.rotation;
            targetIntensity = mainLight.intensity;
        }

        // zombies spawn 4-5 m away, beyond the asset's default 3 m shadow range
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.shadowDistance = ShadowDistance;
        }

        // ARCore's environmental HDR mode: main light direction, colour and brightness plus ambient
        if (cameraManager != null)
        {
            cameraManager.requestedLightEstimation = LightEstimation.MainLightDirection
                | LightEstimation.MainLightIntensity | LightEstimation.AmbientSphericalHarmonics;
            cameraManager.frameReceived += OnFrameReceived;
        }

        CreateShadowFloor(arCamera, floorBelowCamera);
    }

    void OnDestroy()
    {
        if (cameraManager != null) cameraManager.frameReceived -= OnFrameReceived;
    }

    void OnFrameReceived(ARCameraFrameEventArgs args)
    {
        ARLightEstimationData estimate = args.lightEstimation;

        if (estimate.mainLightDirection.HasValue)
        {
            hasDirection = true;
            targetRotation = Quaternion.LookRotation(estimate.mainLightDirection.Value);
        }
        if (estimate.mainLightColor.HasValue)
        {
            Color color = estimate.mainLightColor.Value;
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (max > 0.0001f) targetColor = color / max;
        }
        if (estimate.averageMainLightBrightness.HasValue)
        {
            // keep zombies readable in a dark room and stop them glowing in sunlight
            targetIntensity = Mathf.Clamp(estimate.averageMainLightBrightness.Value * 1.6f, 0.45f, 1.8f);
        }
        else if (estimate.averageBrightness.HasValue)
        {
            targetIntensity = Mathf.Clamp(estimate.averageBrightness.Value * 2f, 0.45f, 1.8f);
        }

        if (estimate.ambientSphericalHarmonics.HasValue)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = estimate.ambientSphericalHarmonics.Value;
        }
    }

    void Update()
    {
        // ease towards the estimate so the lighting never flickers
        if (mainLight != null)
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
            mainLight.intensity = Mathf.Lerp(mainLight.intensity, targetIntensity, k);
            mainLight.color = Color.Lerp(mainLight.color, targetColor, k);
            if (hasDirection) mainLight.transform.rotation = Quaternion.Slerp(mainLight.transform.rotation, targetRotation, k);
        }

        // the shadow floor follows the player so shadows exist wherever zombies walk
        if (floor != null)
        {
            Vector3 position = transform.position;
            // sit a hair above the real floor so the shadows don't flicker against it
            float y = ARWorld.Instance != null ? ARWorld.FloorY + 0.005f : floor.position.y;
            floor.position = new Vector3(position.x, y, position.z);
        }
    }

    void CreateShadowFloor(Camera arCamera, float floorBelowCamera)
    {
        Shader shader = Shader.Find("Outbreak/ShadowCatcher");
        if (shader == null) return;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ShadowFloor";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(FloorSize, FloorSize, 1f);
        quad.transform.position = arCamera.transform.position + Vector3.down * floorBelowCamera;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(shader);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        floor = quad.transform;
    }

    static Light FindMainLight()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional) return light;
        }
        return null;
    }
}
