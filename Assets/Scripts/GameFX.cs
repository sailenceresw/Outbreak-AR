using UnityEngine;

// Particle effects and textures built at runtime from one shared particle material.
public static class GameFX
{
    static Material particleMaterial;
    static Texture2D whiteTexture, ringTexture, arrowTexture, vignetteTexture, topShadeTexture;
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

    public static void MuzzleFlash(Vector3 position, Vector3 direction, bool big = false)
    {
        Burst(position, direction, new Color(1f, 0.8f, 0.3f, 1f), big ? 24 : 10, big ? 4.5f : 3f,
            big ? 0.09f : 0.05f, big ? 0.12f : 0.08f, 0f, big ? 40f : 25f);
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

    // grenade blast: fireball, sparks, lingering smoke and a ring along the floor
    public static void Explosion(Vector3 position)
    {
        Burst(position + Vector3.up * 0.15f, Vector3.up, new Color(1f, 0.55f, 0.15f, 1f), 45, 3.2f, 0.4f, 0.45f, -0.2f, 180f);
        Burst(position + Vector3.up * 0.1f, Vector3.up, new Color(1f, 0.9f, 0.5f, 1f), 70, 7.5f, 0.045f, 0.8f, 1.2f, 160f);
        Burst(position + Vector3.up * 0.2f, Vector3.up, new Color(0.22f, 0.2f, 0.18f, 0.7f), 30, 1.1f, 0.7f, 1.8f, -0.15f, 120f);
        Shockwave(position + Vector3.up * 0.05f, new Color(1f, 0.6f, 0.2f, 0.8f));
        FlashLight(position + Vector3.up * 0.5f, new Color(1f, 0.6f, 0.25f), 8f, 6f, 0.35f);
    }

    // brief point light so muzzle flashes and blasts light up the zombies around them
    public static void FlashLight(Vector3 position, Color color, float intensity, float range, float duration)
    {
        GameObject holder = new GameObject("FX Flash");
        holder.transform.position = position;
        Light light = holder.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        holder.AddComponent<FadeLight>().duration = duration;
    }

    // a dark blood mark that sticks to a zombie's body where it was shot
    public static void Wound(Transform bone, Vector3 point, Vector3 shotDirection)
    {
        ParticleSystem system = CreateSystem("FX Wound", point - shotDirection.normalized * 0.02f, -shotDirection,
            new Color(0.32f, 0f, 0f, 0.95f), 0f, 0.08f, 1000f, 0f, 0f);
        system.transform.SetParent(bone, true);

        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.duration = 0.1f;
        main.maxParticles = 3;
        main.startLifetime = 1000f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        // follow the body as it animates
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        // no fade: the wound stays until the corpse is gone
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = false;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)3) });
        system.Play();
    }

    // brass casing flicked out of the right side of the gun
    public static void ShellCasing(Vector3 position, Vector3 right, Vector3 up, bool shotgun)
    {
        GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        casing.name = "FX Casing";
        Object.Destroy(casing.GetComponent<Collider>());
        casing.transform.position = position;
        casing.transform.localScale = shotgun ? new Vector3(0.02f, 0.03f, 0.02f) : new Vector3(0.009f, 0.012f, 0.009f);
        casing.transform.rotation = Random.rotation;

        Renderer renderer = casing.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color brass = shotgun ? new Color(0.7f, 0.12f, 0.1f) : new Color(0.85f, 0.65f, 0.25f);
        block.SetColor("_BaseColor", brass);
        block.SetFloat("_Metallic", 0.9f);
        block.SetFloat("_Smoothness", 0.7f);
        renderer.SetPropertyBlock(block);

        Rigidbody body = casing.AddComponent<Rigidbody>();
        body.mass = 0.01f;
        body.linearVelocity = right * Random.Range(1.2f, 1.8f) + up * Random.Range(0.8f, 1.3f);
        body.angularVelocity = Random.insideUnitSphere * 25f;
        Object.Destroy(casing, 1.2f);
    }

    // dark stain that spreads on the floor under a fallen zombie, then fades
    public static void BloodPool(Vector3 floorPosition, float scale)
    {
        const float lifetime = 7f;
        ParticleSystem system = CreateSystem("FX BloodPool", floorPosition + Vector3.up * 0.01f, Vector3.up,
            new Color(0.28f, 0f, 0f, 0.85f), 0f, 0.9f * scale, lifetime, 0f, 0f);

        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.duration = 0.1f;
        main.maxParticles = 1;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = Random.Range(0.75f, 1f) * scale;
        // lie flat on the floor with a random spin
        main.startRotation3D = true;
        main.startRotationX = 90f * Mathf.Deg2Rad;
        main.startRotationY = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        main.startRotationZ = 0f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

        ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.12f, 1f), new Keyframe(1f, 1.05f)));

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.World;

        system.Play();
        Object.Destroy(system.gameObject, lifetime + 0.5f);
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

    // opaque at the top, clear at the bottom; sits behind the HUD so text stays readable over the camera
    public static Texture2D TopShadeTexture
    {
        get
        {
            if (topShadeTexture == null)
            {
                topShadeTexture = NewTexture(64, (x, y) => Mathf.SmoothStep(0f, 1f, y / 63f));
            }
            return topShadeTexture;
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
