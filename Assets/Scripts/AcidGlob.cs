using UnityEngine;

// A spitter's acid: flies at where the player stood when it was thrown. Step aside to dodge it,
// or shoot it out of the air.
public class AcidGlob : MonoBehaviour
{
    const float Speed = 3.2f;
    const float Damage = 14f;
    // how close to the camera counts as a hit when it arrives
    const float HitRadius = 0.5f;

    static readonly Color AcidColor = new Color(0.55f, 1f, 0.2f, 1f);

    Vector3 start;
    Vector3 target;
    float travelTime;
    float age;

    public static AcidGlob Launch(Vector3 from, Vector3 target)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "AcidGlob";
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.13f;

        // a trigger, so the player's shots can hit it without it pushing anything
        SphereCollider collider = go.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1.2f;

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", AcidColor);
        block.SetColor("_EmissionColor", AcidColor * 2f);
        renderer.SetPropertyBlock(block);
        GameFX.Aura(go.transform, AcidColor);

        AcidGlob glob = go.AddComponent<AcidGlob>();
        glob.start = from;
        glob.target = target;
        glob.travelTime = Vector3.Distance(from, target) / Speed;
        return glob;
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / Mathf.Max(travelTime, 0.01f));

        // a shallow lob
        Vector3 position = Vector3.Lerp(start, target, k);
        position.y += Mathf.Sin(k * Mathf.PI) * 0.35f;
        transform.position = position;

        if (k >= 1f) Arrive();
    }

    void Arrive()
    {
        Camera camera = Camera.main;
        bool hit = camera != null && Vector3.Distance(camera.transform.position, target) < HitRadius;
        GameControllerScript game = GameControllerScript.Instance;
        if (hit && game != null) game.AcidHit(Damage);
        else if (game != null) game.AcidDodged();

        GameFX.Burst(transform.position, Vector3.up, AcidColor, 30, 1.6f, 0.06f, 0.7f, 1f, 160f);
        GameAudio.PlayAt(GameAudio.AcidSplash, transform.position, 0.8f);
        Destroy(gameObject);
    }

    // shot out of the air
    public void Pop()
    {
        GameFX.Burst(transform.position, Vector3.up, AcidColor, 40, 2.2f, 0.06f, 0.6f, 1.2f, 180f);
        GameAudio.PlayAt(GameAudio.AcidSplash, transform.position, 0.7f, 1.3f);
        Destroy(gameObject);
    }
}
