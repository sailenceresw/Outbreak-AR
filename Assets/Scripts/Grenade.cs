using System;
using System.Collections;
using UnityEngine;

// A thrown grenade: arcs to its target on the floor, blinks while the fuse burns, then explodes.
public class Grenade : MonoBehaviour
{
    const float FuseTime = 0.7f;

    static readonly Color BodyColor = new Color(0.22f, 0.27f, 0.16f, 1f);
    static readonly Color BlinkColor = new Color(1f, 0.15f, 0.1f, 1f);

    Renderer body;
    MaterialPropertyBlock propertyBlock;
    Action<Vector3> onExplode;

    public static Grenade Throw(Vector3 from, Vector3 target, float flightTime, Action<Vector3> onExplode)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Grenade";
        // damage comes from the blast radius, so the shell must not block shots
        Destroy(go.GetComponent<Collider>());
        go.transform.position = from;
        go.transform.localScale = new Vector3(0.08f, 0.1f, 0.08f);

        Grenade grenade = go.AddComponent<Grenade>();
        grenade.onExplode = onExplode;
        grenade.body = go.GetComponent<Renderer>();
        grenade.propertyBlock = new MaterialPropertyBlock();
        grenade.SetColor(BodyColor);
        grenade.StartCoroutine(grenade.Fly(from, target, flightTime));
        return grenade;
    }

    IEnumerator Fly(Vector3 from, Vector3 target, float flightTime)
    {
        // a lob that peaks above the higher of the two ends
        float arcHeight = Mathf.Max(0.6f, Vector3.Distance(from, target) * 0.25f);
        Vector3 spinAxis = UnityEngine.Random.onUnitSphere;

        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = t / flightTime;
            Vector3 position = Vector3.Lerp(from, target, k);
            position.y += arcHeight * 4f * k * (1f - k);
            transform.position = position;
            transform.Rotate(spinAxis, 720f * Time.deltaTime, Space.World);
            yield return null;
        }
        transform.position = target + Vector3.up * 0.05f;
        GameFX.Dust(target, 0.4f);

        // blink faster as the fuse runs down
        for (float t = 0f; t < FuseTime; t += Time.deltaTime)
        {
            float rate = Mathf.Lerp(6f, 22f, t / FuseTime);
            SetColor(Mathf.Sin(t * rate * Mathf.PI * 2f) > 0f ? BlinkColor : BodyColor);
            yield return null;
        }

        onExplode?.Invoke(target);
        Destroy(gameObject);
    }

    void SetColor(Color color)
    {
        if (body == null) return;
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        body.SetPropertyBlock(propertyBlock);
    }
}
