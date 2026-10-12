using UnityEngine;

// Particle effects and textures built at runtime from one shared particle material.
public static class GameFX
{
    static Material particleMaterial;
    static Texture2D whiteTexture, ringTexture, arrowTexture, vignetteTexture;
    static Sprite whiteSprite, ringSprite, arrowSprite;

    static Material ParticleMaterial
    {
        get
        {
            if (particleMaterial == null) particleMaterial = Resources.Load<Material>("FX/FX_Particle");
            return particleMaterial;
        }
    }

    // ---------- Effects ----------

    public static void Blood(Vector3 position, Vector3 direction, bool headshot)
    {
        Burst(position, direction, new Color(0.45f, 0f, 0f, 1f), headshot ? 40 : 18,
            headshot ? 3.5f : 2.2f, headshot ? 0.09f : 0.06f, 0.7f, 1.2f, 70f);
    }

    public static void MuzzleFlash(Vector3 position, Vector3 direction)
    {
        Burst(position, direction, new Color(1f, 0.8f, 0.3f, 1f), 10, 3f, 0.05f, 0.08f, 0f, 25f);
    }

    // dirt kicked up when a zombie claws out of the ground
    public static void Dust(Vector3 position, float scale)
    {
        Burst(position, Vector3.up, new Color(0.35f, 0.3f, 0.25f, 0.8f), Mathf.RoundToInt(30 * scale),
            1.2f * scale, 0.25f * scale, 1.3f, 0.3f, 160f);
    }

    public static void Sparkle(Vector3 position, Color color)
    {
        Burst(position, Vector3.up, color, 25, 1.8f, 0.05f, 0.6f, -0.2f, 180f);
    }

    public static void Shockwave(Vector3 position, Color color)
    {
        Burst(position, Vector3.up, color, 120, 6f, 0.12f, 0.8f, 0f, 180f);
    }

    public static ParticleSystem Burst(Vector3 position, Vector3 direction, Color color, int count,
        float speed, float size, float lifetime, float gravity, float spreadAngle)
    {
        ParticleSystem system = CreateSystem("FX Burst", position, direction, color, speed, size, lifetime, gravity, spreadAngle);
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.duration = 0.1f;
        main.maxParticles = Mathf.Max(count, 1);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        system.Play();
        Object.Destroy(system.gameObject, lifetime + 0.5f);
        return system;
    }

    // looping glow that follows a power-up
    public static ParticleSystem Aura(Transform parent, Color color)
    {
        ParticleSystem system = CreateSystem("FX Aura", parent.position, Vector3.up, color, 0.3f, 0.08f, 0.8f, -0.1f, 180f);
        system.transform.SetParent(parent, true);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.duration = 1f;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        system.Play();
        return system;
    }

    static ParticleSystem CreateSystem(string name, Vector3 position, Vector3 direction, Color color,
        float speed, float size, float lifetime, float gravity, float spreadAngle)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetPositionAndRotation(position,
            direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity);

        ParticleSystem system = holder.AddComponent<ParticleSystem>();
        // a new particle system starts playing immediately, and duration can only change while stopped
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Clamp(spreadAngle * 0.5f, 0f, 90f);
        shape.radius = 0.02f;

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
        if (ParticleMaterial != null) renderer.sharedMaterial = ParticleMaterial;

        return system;
    }

    // ---------- Procedural UI art ----------

    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                whiteTexture = NewTexture(4, (x, y) => 1f);
                whiteSprite = ToSprite(whiteTexture);
            }
            return whiteSprite;
        }
    }

    public static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                // thin ring with a gap at each compass point, like a sight
                ringTexture = NewTexture(64, (x, y) =>
                {
                    float dx = x - 31.5f, dy = y - 31.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - 24f) - 1.5f);
                    bool gap = Mathf.Abs(dx) < 3f || Mathf.Abs(dy) < 3f;
                    float dot = 1f - Mathf.Clamp01(distance - 2.5f);
                    return Mathf.Max(gap ? 0f : ring, dot);
                });
                ringSprite = ToSprite(ringTexture);
            }
            return ringSprite;
        }
    }

    public static Sprite ArrowSprite
    {
        get
        {
            if (arrowSprite == null)
            {
                // upward pointing chevron
                arrowTexture = NewTexture(64, (x, y) =>
                {
                    float u = (x - 31.5f) / 32f;
                    float v = y / 63f;
                    float halfWidth = (1f - v) * 0.9f;
                    float inside = halfWidth - Mathf.Abs(u);
                    float notch = v - (0.35f - Mathf.Abs(u) * 0.35f);
                    return Mathf.Clamp01(inside * 40f) * Mathf.Clamp01(notch * 40f);
                });
                arrowSprite = ToSprite(arrowTexture);
            }
            return arrowSprite;
        }
    }

    public static Texture2D VignetteTexture
    {
        get
        {
            if (vignetteTexture == null)
            {
                vignetteTexture = NewTexture(128, (x, y) =>
                {
                    float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                    return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy)));
                });
            }
            return vignetteTexture;
        }
    }

    static Texture2D NewTexture(int size, System.Func<int, int, float> alpha)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static Sprite ToSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
