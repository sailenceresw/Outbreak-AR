using UnityEngine;

// Fades a light out over its duration, then removes it. Used for muzzle flashes and blasts.
public class FadeLight : MonoBehaviour
{
    public float duration = 0.06f;

    Light fadingLight;
    float startIntensity;
    float age;

    void Start()
    {
        fadingLight = GetComponent<Light>();
        startIntensity = fadingLight.intensity;
    }

    void Update()
    {
        age += Time.deltaTime;
        fadingLight.intensity = startIntensity * Mathf.Clamp01(1f - age / duration);
        if (age >= duration) Destroy(gameObject);
    }
}
