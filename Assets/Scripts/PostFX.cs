using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Horror look over the whole view (camera feed included): bloom on flashes and explosions,
// a cold desaturated grade, film grain and a vignette that reacts to danger.
public class PostFX : MonoBehaviour
{
    public static PostFX Instance { get; private set; }

    Bloom bloom;
    FilmGrain grain;
    Vignette vignette;
    ChromaticAberration aberration;
    ColorAdjustments colorAdjustments;
    float danger;
    float jolt;

    public static PostFX Create(Camera camera)
    {
        if (camera == null) return null;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        GameObject holder = new GameObject("PostFX");
        PostFX fx = holder.AddComponent<PostFX>();
        fx.Build(holder);
        fx.ApplyQuality();
        GameSettings.QualityChanged += fx.ApplyQuality;
        Instance = fx;
        return fx;
    }

    void OnDestroy()
    {
        GameSettings.QualityChanged -= ApplyQuality;
        if (Instance == this) Instance = null;
    }

    // bloom and film grain are the costly passes; Low quality drops them
    void ApplyQuality()
    {
        bool high = GameSettings.HighQuality;
        bloom.active = high;
        grain.active = high;
    }

    void Build(GameObject holder)
    {
        Volume volume = holder.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.6f);

        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.contrast.Override(14f);
        colorAdjustments.saturation.Override(-18f);
        colorAdjustments.colorFilter.Override(new Color(0.95f, 1f, 0.96f));

        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.22f);
        grain.response.Override(0.8f);

        aberration = profile.Add<ChromaticAberration>(true);
        aberration.intensity.Override(0f);
    }

    // 0 = safe, 1 = about to die
    public void SetDanger(float amount)
    {
        danger = Mathf.Clamp01(amount);
    }

    // short lens punch for explosions and heavy hits
    public void Jolt(float strength)
    {
        jolt = Mathf.Max(jolt, Mathf.Clamp01(strength));
    }

    void Update()
    {
        jolt = Mathf.MoveTowards(jolt, 0f, Time.unscaledDeltaTime * 2.5f);
        float pulse = danger * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));

        vignette.intensity.Override(Mathf.Lerp(0.3f, 0.5f, danger) + 0.08f * pulse);
        vignette.color.Override(Color.Lerp(Color.black, new Color(0.45f, 0f, 0f), danger));
        colorAdjustments.saturation.Override(Mathf.Lerp(-18f, -55f, danger));
        aberration.intensity.Override(Mathf.Max(jolt, danger * 0.25f));
        bloom.intensity.Override(0.7f + jolt * 1.2f);
    }
}
