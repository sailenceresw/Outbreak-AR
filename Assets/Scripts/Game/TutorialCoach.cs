using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// First-play coaching: short tips during the first campaign fight that react to what the player does,
// with the button they should press pulsing. Runs once; Settings can replay it.
public class TutorialCoach : GameSystem
{
    const string DoneKey = "Tutorial_Done";

    public static bool Done
    {
        get => PlayerPrefs.GetInt(DoneKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(DoneKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    RectTransform bubble;
    TMP_Text bubbleText;
    Transform pulsing;
    Vector3 pulsingScale;
    bool running;

    protected override void OnBind()
    {
        Flow.LevelStarted += () =>
        {
            if (!Done && !running && Flow.Encounter == null && Flow.CurrentLevelIndex == 0) StartCoroutine(FirstFight());
        };
        Flow.StateChanged += state =>
        {
            if (state == GameControllerScript.GameState.LevelComplete && running)
            {
                StopAllCoroutines();
                StartCoroutine(Finish());
            }
            if (state == GameControllerScript.GameState.Lost)
            {
                // a retry starts the tips over
                StopAllCoroutines();
                running = false;
                Hide();
            }
        };
    }

    IEnumerator FirstFight()
    {
        running = true;
        yield return Tip("Move your phone to aim.\nThe crosshair is the middle of the screen.", null,
            () => Spawner.Alive.Count > 0, 8f);
        yield return Tip("Zombie! Tap <color=#FF5040>FIRE</color> to shoot.\nHeadshots do double damage.", Game.shootButton,
            () => Flow.RunKills >= 1, 40f);
        yield return Tip("They come from every side.\nThe <color=#FF5040>red arrows</color> point at zombies you can't see.", null,
            null, 4.5f);

        if (Inventory.Count(ItemId.Trap) > 0)
        {
            int traps = Inventory.Count(ItemId.Trap);
            yield return Tip("Wound a zombie, then tap <color=#66E6FF>TRAP</color>\nto catch it for your Zombie-dex.", UI.TrapButton,
                () => Inventory.Count(ItemId.Trap) < traps, 15f);
        }

        int grenades = Weapons.GrenadeCount;
        if (grenades > 0)
        {
            yield return Tip("Bunched up? Tap <color=#9BE36A>GRENADE</color>.\nIt lands where you look on the floor.", UI.GrenadeButton,
                () => Weapons.GrenadeCount < grenades, 12f);
        }
        Hide();
    }

    // after the first level: point at the map, then never coach again
    IEnumerator Finish()
    {
        running = false;
        Done = true;
        Hide();
        yield return new WaitForSecondsRealtime(4f);
        yield return Tip("Back on the home screen, open the <color=#9BE36A>WORLD MAP</color>\nto find outbreak sites and supply caches near you.",
            null, null, 6f, false);
        Hide();
    }

    // shows a tip until done() is true, the time runs out, or (for fight tips) the fight stops
    IEnumerator Tip(string text, GameObject target, System.Func<bool> done, float maxSeconds, bool fightOnly = true)
    {
        ShowBubble(text);
        Pulse(target);
        GameAudio.Play(GameAudio.Pickup, 0.4f, 1.4f);

        float until = Time.unscaledTime + maxSeconds;
        while (Time.unscaledTime < until)
        {
            if (done != null && done()) break;
            if (fightOnly && !Flow.IsPlaying) break;
            if (pulsing != null) pulsing.localScale = pulsingScale * (1f + 0.07f * Mathf.Sin(Time.unscaledTime * 8f));
            yield return null;
        }
        StopPulse();
        // a beat between tips so they don't blur together
        if (bubble != null) bubble.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    void ShowBubble(string text)
    {
        if (bubble == null) BuildBubble();
        if (bubble == null) return;
        bubbleText.text = text;
        bubble.gameObject.SetActive(true);
        bubble.SetAsLastSibling();
    }

    void BuildBubble()
    {
        TMP_Text template = Game.playerHealthText;
        Canvas canvas = template != null ? template.canvas : null;
        if (canvas == null) return;

        GameObject go = new GameObject("TutorialTip", typeof(RectTransform));
        bubble = go.GetComponent<RectTransform>();
        bubble.SetParent(canvas.rootCanvas.transform, false);
        bubble.anchorMin = new Vector2(0.07f, 1f);
        bubble.anchorMax = new Vector2(0.93f, 1f);
        bubble.pivot = new Vector2(0.5f, 1f);
        bubble.sizeDelta = new Vector2(0f, 150f);
        bubble.anchoredPosition = new Vector2(0f, -190f);
        Image panel = UIStyle.AddPanel(go, new Color(0.03f, 0.06f, 0.08f, 0.93f));
        panel.raycastTarget = false;

        GameObject rimObject = new GameObject("Rim", typeof(RectTransform));
        RectTransform rim = rimObject.GetComponent<RectTransform>();
        rim.SetParent(bubble, false);
        rim.anchorMin = Vector2.zero;
        rim.anchorMax = Vector2.one;
        rim.offsetMin = rim.offsetMax = Vector2.zero;
        Image rimImage = rimObject.AddComponent<Image>();
        rimImage.sprite = UIStyle.RoundedOutline;
        rimImage.type = Image.Type.Sliced;
        rimImage.color = UIStyle.XP;
        rimImage.raycastTarget = false;

        TMP_Text label = Instantiate(template, bubble, false);
        label.name = "TipLabel";
        foreach (Transform child in label.transform) Destroy(child.gameObject);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.04f, 0.68f);
        labelRect.anchorMax = new Vector2(0.96f, 0.95f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.text = "TIP";
        label.fontSize = 16f;
        label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 8f;
        label.color = UIStyle.XP;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;

        bubbleText = Instantiate(template, bubble, false);
        bubbleText.name = "TipText";
        foreach (Transform child in bubbleText.transform) Destroy(child.gameObject);
        RectTransform textRect = bubbleText.rectTransform;
        textRect.anchorMin = new Vector2(0.04f, 0.06f);
        textRect.anchorMax = new Vector2(0.96f, 0.72f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        bubbleText.fontSize = 24f;
        bubbleText.enableAutoSizing = true;
        bubbleText.fontSizeMin = 14f;
        bubbleText.fontSizeMax = 24f;
        bubbleText.fontStyle = FontStyles.Normal;
        bubbleText.color = Color.white;
        bubbleText.richText = true;
        bubbleText.alignment = TextAlignmentOptions.MidlineLeft;
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        bubbleText.raycastTarget = false;
        go.SetActive(false);
    }

    void Pulse(GameObject target)
    {
        StopPulse();
        if (target == null || !target.activeInHierarchy) return;
        pulsing = target.transform;
        pulsingScale = pulsing.localScale;
    }

    void StopPulse()
    {
        if (pulsing != null) pulsing.localScale = pulsingScale;
        pulsing = null;
    }

    void Hide()
    {
        StopPulse();
        if (bubble != null) bubble.gameObject.SetActive(false);
    }
}
