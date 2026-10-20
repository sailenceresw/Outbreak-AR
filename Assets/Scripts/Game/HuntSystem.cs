using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Hunt: zombies idle at hidden spots around the room until found. A radar points at the nearest one
// and pings faster as the player closes in; each one breathes in 3D. A zombie wakes when the player
// walks close, looks straight at it, or shoots it.
public class HuntSystem : GameSystem
{
    // walking this close (metres, along the floor) wakes a hidden zombie
    const float WakeDistance = 2.2f;
    // looking within this many degrees of a hidden zombie, inside SpotDistance, counts as finding it
    const float SpotAngle = 10f;
    const float SpotDistance = 7f;

    RectTransform radar;
    RectTransform arrow;
    TMP_Text radarText;
    float nextPing;
    readonly Dictionary<ZombieController, float> nextBreath = new Dictionary<ZombieController, float>();

    public bool Active => Flow.Level != null && Flow.Level.objective == LevelObjective.Hunt;

    protected override void OnBind()
    {
        Flow.StateChanged += state =>
        {
            if (state != GameControllerScript.GameState.Playing) SetRadar(false);
        };
    }

    public int HiddenCount
    {
        get
        {
            int count = 0;
            foreach (ZombieController zombie in Spawner.Hidden) if (IsHidden(zombie)) count++;
            return count;
        }
    }

    static bool IsHidden(ZombieController zombie) => zombie != null && !zombie.IsDead && zombie.IsDormant;

    public void Tick()
    {
        if (!Active || !Flow.IsPlaying || MainCamera == null)
        {
            SetRadar(false);
            return;
        }

        Transform view = MainCamera.transform;
        ZombieController nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (ZombieController zombie in Spawner.Hidden.ToArray())
        {
            if (!IsHidden(zombie)) continue;

            Vector3 chest = zombie.transform.position + Vector3.up * 1.1f * zombie.transform.localScale.y;
            Vector3 toZombie = chest - view.position;
            Vector3 flat = new Vector3(toZombie.x, 0f, toZombie.z);
            float distance = flat.magnitude;

            if (distance < WakeDistance || (distance < SpotDistance && Vector3.Angle(view.forward, toZombie) < SpotAngle))
            {
                Found(zombie);
                continue;
            }

            Breathe(zombie, distance);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = zombie;
            }
        }

        if (nearest == null)
        {
            SetRadar(false);
            return;
        }
        UpdateRadar(view, nearest, nearestDistance);
    }

    void Found(ZombieController zombie)
    {
        zombie.Wake();
        nextBreath.Remove(zombie);
        if (Hud != null) Hud.Shake(8f);
        GameAudio.Play(GameAudio.Impact, 0.8f, 1.2f);
        int left = HiddenCount;
        ShowMessage(left > 0 ? "FOUND ONE!\n<size=70%>" + left + " still hiding</size>" : "THAT'S THE LAST ONE!", 1.6f, new Color(1f, 0.35f, 0.25f));
    }

    // slow, ragged breathing from each hiding spot; louder and more often when near
    void Breathe(ZombieController zombie, float distance)
    {
        if (!nextBreath.TryGetValue(zombie, out float at))
        {
            nextBreath[zombie] = Time.time + Random.Range(1f, 4f);
            return;
        }
        if (Time.time < at) return;
        nextBreath[zombie] = Time.time + Mathf.Lerp(3f, 8f, Mathf.InverseLerp(2f, 8f, distance)) + Random.Range(0f, 2f);
        AudioClip groan = Spawner.GroanClip();
        if (groan != null) GameAudio.PlayAt(groan, zombie.transform.position + Vector3.up, 0.45f, Random.Range(0.55f, 0.7f));
    }

    // ---------- Radar ----------

    void UpdateRadar(Transform view, ZombieController target, float distance)
    {
        if (radar == null) BuildRadar();
        if (radar == null) return;
        SetRadar(true);

        // arrow points the way to turn: straight up means ahead
        Vector3 forward = view.forward;
        forward.y = 0f;
        Vector3 toTarget = target.transform.position - view.position;
        toTarget.y = 0f;
        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);

        int left = HiddenCount;
        radarText.text = distance.ToString("0.0") + " m\n<size=70%><color=#FF6040>" + left + " HIDING</color></size>";

        // pings speed up as the player closes in, like a metal detector
        if (Time.time >= nextPing)
        {
            float closeness = 1f - Mathf.InverseLerp(WakeDistance, 9f, distance);
            nextPing = Time.time + Mathf.Lerp(1.6f, 0.35f, closeness);
            GameAudio.Play(GameAudio.HitMarker, 0.25f + 0.25f * closeness, Mathf.Lerp(0.7f, 1.3f, closeness));
        }
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 10f);
        radar.localScale = Vector3.one * pulse;
    }

    void SetRadar(bool visible)
    {
        if (radar != null && radar.gameObject.activeSelf != visible) radar.gameObject.SetActive(visible);
    }

    void BuildRadar()
    {
        TMP_Text template = Game.playerHealthText;
        Canvas canvas = template != null ? template.canvas : null;
        if (canvas == null) return;

        GameObject go = new GameObject("HuntRadar", typeof(RectTransform));
        radar = go.GetComponent<RectTransform>();
        radar.SetParent(canvas.rootCanvas.transform, false);
        // bottom right, above SWAP, clear of FIRE
        radar.anchorMin = radar.anchorMax = radar.pivot = new Vector2(1f, 0f);
        radar.sizeDelta = new Vector2(140f, 140f);
        radar.anchoredPosition = new Vector2(-24f, 190f);
        Image disc = UIStyle.AddPanel(go, new Color(0.03f, 0.08f, 0.03f, 0.75f));
        disc.raycastTarget = false;

        Sprite sweep = UIStyle.Icon("Radar");
        if (sweep != null)
        {
            Image sweepImage = NewImage(radar, "Sweep", sweep, new Color(0.4f, 1f, 0.3f, 0.35f), Vector2.zero, Vector2.one);
            sweepImage.rectTransform.offsetMin = new Vector2(10f, 10f);
            sweepImage.rectTransform.offsetMax = new Vector2(-10f, -10f);
        }

        Sprite arrowSprite = UIStyle.Icon("Arrow");
        Image arrowImage = NewImage(radar, "Arrow", arrowSprite, new Color(1f, 0.3f, 0.2f, 1f), new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
        arrow = arrowImage.rectTransform;
        // the icon's arrow points down; a child turned half round makes 0 degrees point up (ahead)
        GameObject turn = new GameObject("ArrowUp", typeof(RectTransform));
        RectTransform turnRect = turn.GetComponent<RectTransform>();
        turnRect.SetParent(arrow, false);
        turnRect.anchorMin = Vector2.zero;
        turnRect.anchorMax = Vector2.one;
        turnRect.offsetMin = turnRect.offsetMax = Vector2.zero;
        turnRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
        Image arrowUp = turn.AddComponent<Image>();
        arrowUp.sprite = arrowSprite;
        arrowUp.preserveAspect = true;
        arrowUp.raycastTarget = false;
        arrowUp.color = arrowImage.color;
        arrowImage.enabled = false;

        radarText = Instantiate(template, radar, false);
        radarText.name = "RadarText";
        foreach (Transform child in radarText.transform) Destroy(child.gameObject);
        RectTransform textRect = radarText.rectTransform;
        // distance and count sit above the radar
        textRect.anchorMin = new Vector2(-0.6f, 1.02f);
        textRect.anchorMax = new Vector2(1f, 1.55f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        radarText.fontSize = 24f;
        radarText.enableAutoSizing = false;
        radarText.fontStyle = FontStyles.Bold;
        radarText.alignment = TextAlignmentOptions.BottomRight;
        radarText.color = Color.white;
        radarText.richText = true;
        radarText.raycastTarget = false;
        radarText.textWrappingMode = TextWrappingModes.NoWrap;
        go.SetActive(false);
    }

    static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = color;
        return image;
    }
}
