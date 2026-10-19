using System.Collections;
using UnityEngine;

// A thrown containment trap. It flies to the targeted zombie, pins it in an energy field and
// shakes up to three times; a success shrinks the zombie into the trap, a failure lets it break free.
public class CaptureTrap : MonoBehaviour
{
    const float FlightTime = 0.45f;
    const float ShakeTime = 0.6f;

    static readonly Color FieldColor = new Color(0.3f, 0.9f, 1f, 1f);
    static readonly Color SuccessColor = new Color(0.4f, 1f, 0.5f, 1f);
    static readonly Color FailColor = new Color(1f, 0.35f, 0.25f, 1f);

    ZombieController target;
    bool success;
    System.Action<ZombieController, bool> onResult;
    Renderer body;
    MaterialPropertyBlock block;

    public static void Throw(Vector3 from, ZombieController target, float catchChance,
        System.Action<ZombieController, bool> onResult)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "CaptureTrap";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = from;
        go.transform.localScale = new Vector3(0.12f, 0.05f, 0.12f);

        CaptureTrap trap = go.AddComponent<CaptureTrap>();
        trap.target = target;
        trap.success = Random.value < catchChance;
        trap.onResult = onResult;
        trap.body = go.GetComponent<Renderer>();
        trap.block = new MaterialPropertyBlock();
        trap.SetColor(new Color(0.15f, 0.17f, 0.2f), FieldColor * 1.5f);
        trap.StartCoroutine(trap.Run(from));
    }

    IEnumerator Run(Vector3 from)
    {
        GameAudio.Play(GameAudio.Throw, 0.8f, 1.2f);

        // lob onto the floor at the zombie's feet
        for (float t = 0f; t < FlightTime; t += Time.deltaTime)
        {
            if (target == null || target.IsDead)
            {
                Finish(false, true);
                yield break;
            }
            float k = t / FlightTime;
            Vector3 land = target.transform.position + Vector3.up * 0.05f;
            Vector3 position = Vector3.Lerp(from, land, k);
            position.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
            transform.position = position;
            transform.Rotate(Vector3.up, 900f * Time.deltaTime, Space.World);
            yield return null;
        }

        if (target == null || target.IsDead)
        {
            Finish(false, true);
            yield break;
        }

        transform.position = target.transform.position + Vector3.up * 0.03f;
        transform.rotation = Quaternion.identity;
        target.BeginCapture();
        GameFX.FlashLight(transform.position + Vector3.up * 0.8f, FieldColor, 4f, 3f, 0.4f);
        GameAudio.PlayAt(GameAudio.PowerUp, transform.position, 0.9f, 0.7f);
        ParticleSystem field = GameFX.Aura(transform, FieldColor);
        ParticleSystem.ShapeModule shape = field.shape;
        shape.radius = 0.4f * target.transform.localScale.x;
        ParticleSystem.MainModule main = field.main;
        main.startSpeed = 1.2f;
        main.gravityModifier = -0.4f;

        // a success always survives all three shakes; a failure breaks out on one of them
        int shakes = success ? 3 : Random.Range(1, 4);
        for (int i = 0; i < shakes; i++)
        {
            yield return Shake(i);
            if (target == null) break;
        }

        if (field != null) Destroy(field.gameObject);
        // shot dead while trapped: the trap has nothing left to hold
        bool stillThere = target != null && !target.IsDead;
        Finish(success && stillThere, !stillThere);
    }

    IEnumerator Shake(int index)
    {
        GameAudio.PlayAt(GameAudio.DryFire, transform.position, 0.9f, 0.7f + index * 0.15f);
        for (float t = 0f; t < ShakeTime; t += Time.deltaTime)
        {
            float wobble = Mathf.Sin(t / ShakeTime * Mathf.PI * 4f) * 25f * (1f - t / ShakeTime);
            transform.rotation = Quaternion.Euler(0f, 0f, wobble);
            yield return null;
        }
        transform.rotation = Quaternion.identity;
    }

    void Finish(bool caught, bool missed)
    {
        if (!missed)
        {
            Color color = caught ? SuccessColor : FailColor;
            GameFX.Burst(transform.position + Vector3.up * 0.3f, Vector3.up, color, 50, 2.5f, 0.06f, 0.8f, -0.3f, 180f);
            GameFX.FlashLight(transform.position + Vector3.up * 0.5f, color, 5f, 3f, 0.4f);
        }
        onResult?.Invoke(target, caught && !missed);
        Destroy(gameObject, caught ? 0.6f : 0f);
    }

    void SetColor(Color baseColor, Color emission)
    {
        if (body == null) return;
        block.SetColor("_BaseColor", baseColor);
        block.SetColor("_EmissionColor", emission);
        body.SetPropertyBlock(block);
    }
}
