using UnityEngine;

public enum PowerUpType { RapidFire, HollowPoints, CryoPulse, Shockwave }

// Glowing orb dropped by zombies. Shoot it or walk into it to activate.
public class PowerUpPickup : MonoBehaviour
{
    public PowerUpType type;
    public float collectDistance = 0.8f;
    public float lifetime = 15f;

    Camera mainCamera;
    Vector3 basePosition;
    float spawnTime;
    bool collected;

    public static string DisplayName(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.RapidFire: return "RAPID FIRE";
            case PowerUpType.HollowPoints: return "HOLLOW POINTS";
            case PowerUpType.CryoPulse: return "CRYO PULSE";
            default: return "SHOCKWAVE";
        }
    }

    public static Color TypeColor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.RapidFire: return new Color(1f, 0.85f, 0.1f);
            case PowerUpType.HollowPoints: return new Color(1f, 0.35f, 0.1f);
            case PowerUpType.CryoPulse: return new Color(0.3f, 0.85f, 1f);
            default: return new Color(0.75f, 0.3f, 1f);
        }
    }

    public static PowerUpPickup Spawn(PowerUpType type, Vector3 position)
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "PowerUp " + type;
        orb.transform.position = position;
        orb.transform.localScale = Vector3.one * 0.22f;

        // trigger so zombies and tracers pass through, the shot raycast still hits it
        orb.GetComponent<Collider>().isTrigger = true;

        Color color = TypeColor(type);
        Renderer renderer = orb.GetComponent<Renderer>();
        renderer.material.color = color;
        renderer.material.EnableKeyword("_EMISSION");
        renderer.material.SetColor("_EmissionColor", color * 2f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameFX.Aura(orb.transform, color);

        PowerUpPickup pickup = orb.AddComponent<PowerUpPickup>();
        pickup.type = type;
        return pickup;
    }

    void Start()
    {
        mainCamera = Camera.main;
        basePosition = transform.position;
        spawnTime = Time.time;
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        transform.position = basePosition + Vector3.up * (Mathf.Sin(age * 3f) * 0.06f);
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

        // blink during the last few seconds so the player knows it's about to vanish
        if (age > lifetime - 3f)
        {
            GetComponent<Renderer>().enabled = Mathf.Repeat(age * 6f, 1f) > 0.3f;
        }
        if (age > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (mainCamera == null) return;
        Vector3 offset = mainCamera.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= collectDistance) Collect();
    }

    public void Collect()
    {
        GameControllerScript game = GameControllerScript.Instance;
        if (collected || game == null || !game.IsPlaying) return;

        collected = true;
        GameFX.Sparkle(transform.position, TypeColor(type));
        game.ActivatePowerUp(type);
        Destroy(gameObject);
    }
}
