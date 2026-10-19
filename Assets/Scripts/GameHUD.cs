using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Combat HUD built at runtime on the scene's existing canvas.
public class GameHUD : MonoBehaviour
{
    const int MaxThreatArrows = 8;
    const int MaxDamageNumbers = 16;
    const float DamageNumberLife = 0.9f;

    static readonly Color HealthHigh = new Color(0.3f, 0.9f, 0.35f, 1f);
    static readonly Color HealthLow = new Color(0.95f, 0.15f, 0.1f, 1f);
    static readonly Color AimOnTarget = new Color(1f, 0.25f, 0.2f, 1f);
    static readonly Color XPBarColor = new Color(0.35f, 0.75f, 1f, 1f);

    const int MaxScreenSplats = 6;
    const float ScreenSplatLife = 3.2f;

    class ScreenSplat
    {
        public RawImage image;
        public float startTime;
        public Vector2 start;
        public Color color;
    }

    static readonly Color BloodSplat = new Color(0.42f, 0f, 0f, 0.6f);

    readonly List<ScreenSplat> screenSplats = new List<ScreenSplat>();
    int nextScreenSplat;

    class DamageNumber
    {
        public TMP_Text text;
        public Vector3 worldPosition;
        public float startTime;
        public float scale;
    }

    Canvas canvas;
    RectTransform root;
    Image healthFill;
    Image healthGhost;
    TMP_Text healthValue;
    float healthTarget = 1f;
    float healthShown = 1f;
    float healthGhostShown = 1f;
    float healthGhostHoldUntil;
    int healthShownValue = -1;
    TMP_Text xpLevelText;
    Image xpFill;
    int xpLevel = -1;
    float xpTarget;
    TMP_Text weaponText;
    TMP_Text roundsText;
    TMP_Text grenadeText;
    TMP_Text calloutText;
    TMP_Text catchHint;
    float calloutStart = -10f;
    float calloutUntil;
    readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
    int nextDamageNumber;
    bool aimOnTarget;
    CanvasGroup resultsGroup;
    TMP_Text resultsTitle;
    TMP_Text resultsBody;
    Coroutine resultsRoutine;
    RawImage vignette;
    RectTransform crosshair;
    Image crosshairImage;
    readonly List<Image> threatArrows = new List<Image>();
    GameObject bossBar;
    Image bossFill;
    TMP_Text bossLabel;
    TMP_Text scoreText;
    TMP_Text objectiveText;
    CanvasGroup bannerGroup;
    TMP_Text bannerTitle;
    TMP_Text bannerBody;

    float damageAlpha;
    float lowHealthAmount;
    float crosshairPunch;
    Color crosshairFlashColor = Color.white;
    float shakeAmount;
    Coroutine bannerRoutine;

    public static GameHUD Create(Canvas canvas, TMP_Text textTemplate)
    {
        GameObject holder = new GameObject("GameHUD", typeof(RectTransform));
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Stretch(rect);
        // first child, so prompts and the fade image draw on top of the HUD
        rect.SetAsFirstSibling();

        GameHUD hud = holder.AddComponent<GameHUD>();
        hud.canvas = canvas;
        hud.Build(rect, textTemplate);
        return hud;
    }

    void Build(RectTransform rootRect, TMP_Text template)
    {
        root = rootRect;

        vignette = NewRawImage("Vignette", GameFX.VignetteTexture);
        Stretch(vignette.rectTransform);
        vignette.color = new Color(0.6f, 0f, 0f, 0f);

        for (int i = 0; i < MaxScreenSplats; i++)
        {
            RawImage splat = NewRawImage("ScreenBlood", GameFX.SplatTexture(i));
            splat.rectTransform.anchorMin = splat.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            splat.gameObject.SetActive(false);
            screenSplats.Add(new ScreenSplat { image = splat });
        }

        // dark fade behind the top of the HUD so it reads over any camera image
        RawImage shade = NewRawImage("TopShade", GameFX.TopShadeTexture);
        shade.rectTransform.anchorMin = new Vector2(0f, 1f);
        shade.rectTransform.anchorMax = new Vector2(1f, 1f);
        shade.rectTransform.pivot = new Vector2(0.5f, 1f);
        shade.rectTransform.sizeDelta = new Vector2(0f, 190f);
        shade.color = new Color(0f, 0f, 0f, 0.6f);

        BuildHealthPanel(template);
        BuildAmmoPanel(template);

        crosshairImage = NewImage("Crosshair", GameFX.RingSprite);
        crosshair = crosshairImage.rectTransform;
        crosshair.sizeDelta = new Vector2(56f, 56f);
        crosshairImage.color = new Color(1f, 1f, 1f, 0.85f);

        for (int i = 0; i < MaxThreatArrows; i++)
        {
            Image arrow = NewImage("ThreatArrow", GameFX.ArrowSprite);
            arrow.rectTransform.sizeDelta = new Vector2(44f, 44f);
            arrow.gameObject.SetActive(false);
            threatArrows.Add(arrow);
        }

        // boss health bar across the top
        bossBar = new GameObject("BossBar", typeof(RectTransform));
        RectTransform barRect = bossBar.GetComponent<RectTransform>();
        barRect.SetParent(root, false);
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -70f);
        barRect.sizeDelta = new Vector2(420f, 16f);
        Image background = bossBar.AddComponent<Image>();
        background.sprite = GameFX.WhiteSprite;
        background.color = new Color(0f, 0f, 0f, 0.6f);
        background.raycastTarget = false;

        bossFill = NewImage("Fill", GameFX.WhiteSprite, barRect);
        Stretch(bossFill.rectTransform);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossFill.color = new Color(0.8f, 0.05f, 0.05f, 1f);

        bossLabel = NewText(template, "BossLabel", 22f, TextAlignmentOptions.Bottom, barRect);
        bossLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        bossLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        bossLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        bossLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        bossLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
        bossBar.SetActive(false);

        scoreText = NewText(template, "Score", 26f, TextAlignmentOptions.Top, root);
        AnchorTop(scoreText.rectTransform, -8f, 34f);

        objectiveText = NewText(template, "Objective", 22f, TextAlignmentOptions.Top, root);
        AnchorTop(objectiveText.rectTransform, -38f, 30f);

        // level banner in the middle of the screen
        GameObject banner = new GameObject("Banner", typeof(RectTransform));
        RectTransform bannerRect = banner.GetComponent<RectTransform>();
        bannerRect.SetParent(root, false);
        bannerRect.anchorMin = new Vector2(0f, 0.5f);
        bannerRect.anchorMax = new Vector2(1f, 0.5f);
        bannerRect.sizeDelta = new Vector2(0f, 260f);
        bannerRect.anchoredPosition = new Vector2(0f, 60f);
        bannerGroup = banner.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        bannerTitle = NewText(template, "Title", 44f, TextAlignmentOptions.Bottom, bannerRect);
        bannerTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        bannerTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        bannerTitle.rectTransform.offsetMin = new Vector2(16f, 0f);
        bannerTitle.rectTransform.offsetMax = new Vector2(-16f, 0f);
        bannerTitle.fontStyle = FontStyles.Bold;

        bannerBody = NewText(template, "Body", 24f, TextAlignmentOptions.Top, bannerRect);
        bannerBody.rectTransform.anchorMin = new Vector2(0f, 0f);
        bannerBody.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        bannerBody.rectTransform.offsetMin = new Vector2(16f, 0f);
        bannerBody.rectTransform.offsetMax = new Vector2(-16f, -8f);

        catchHint = NewText(template, "CatchHint", 20f, TextAlignmentOptions.Center, root);
        catchHint.rectTransform.anchorMin = catchHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        catchHint.rectTransform.sizeDelta = new Vector2(400f, 30f);
        catchHint.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        catchHint.color = new Color(0.4f, 0.95f, 1f);
        catchHint.fontStyle = FontStyles.Bold;

        // short call-outs like HEADSHOT! pop up above the crosshair
        calloutText = NewText(template, "Callout", 34f, TextAlignmentOptions.Center, root);
        calloutText.rectTransform.anchorMin = calloutText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        calloutText.rectTransform.sizeDelta = new Vector2(900f, 120f);
        // above the level banner, so a call-out and a banner never sit on top of each other
        calloutText.rectTransform.anchoredPosition = new Vector2(0f, 330f);
        calloutText.fontStyle = FontStyles.Bold;

        for (int i = 0; i < MaxDamageNumbers; i++)
        {
            TMP_Text number = NewText(template, "DamageNumber", 26f, TextAlignmentOptions.Center, root);
            number.rectTransform.anchorMin = number.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            number.rectTransform.sizeDelta = new Vector2(200f, 40f);
            number.fontStyle = FontStyles.Bold;
            number.gameObject.SetActive(false);
            damageNumbers.Add(new DamageNumber { text = number });
        }

        BuildResultsCard(template);
    }

    void BuildHealthPanel(TMP_Text template)
    {
        RectTransform panel = NewRect("HealthPanel", root);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(18f, -14f);
        panel.sizeDelta = new Vector2(250f, 46f);

        // "HEALTH 100" sits on the left so a long score line in the centre can't run into it
        healthValue = NewText(template, "Label", 18f, TextAlignmentOptions.BottomLeft, panel);
        healthValue.rectTransform.anchorMin = new Vector2(0f, 1f);
        healthValue.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthValue.rectTransform.pivot = new Vector2(0f, 1f);
        healthValue.rectTransform.sizeDelta = new Vector2(0f, 22f);
        healthValue.color = new Color(1f, 1f, 1f, 0.7f);
        healthValue.characterSpacing = 6f;

        // bar: dark track, a pale "ghost" that trails damage, then the live fill
        RectTransform bar = NewRect("Bar", panel);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(0f, 16f);
        Image track = bar.gameObject.AddComponent<Image>();
        track.sprite = GameFX.WhiteSprite;
        track.color = new Color(0f, 0f, 0f, 0.55f);
        track.raycastTarget = false;

        healthGhost = NewFill("Ghost", bar, new Color(1f, 0.95f, 0.85f, 0.75f));
        healthFill = NewFill("Fill", bar, HealthHigh);

        // survivor level and a thin XP bar just below the health bar
        RectTransform xpPanel = NewRect("XPPanel", root);
        xpPanel.anchorMin = xpPanel.anchorMax = new Vector2(0f, 1f);
        xpPanel.pivot = new Vector2(0f, 1f);
        xpPanel.anchoredPosition = new Vector2(18f, -66f);
        xpPanel.sizeDelta = new Vector2(250f, 20f);

        xpLevelText = NewText(template, "Level", 16f, TextAlignmentOptions.MidlineLeft, xpPanel);
        xpLevelText.rectTransform.anchorMin = new Vector2(0f, 0f);
        xpLevelText.rectTransform.anchorMax = new Vector2(0f, 1f);
        xpLevelText.rectTransform.pivot = new Vector2(0f, 0.5f);
        xpLevelText.rectTransform.sizeDelta = new Vector2(60f, 0f);
        xpLevelText.color = XPBarColor;
        xpLevelText.fontStyle = FontStyles.Bold;

        RectTransform xpBar = NewRect("Bar", xpPanel);
        xpBar.anchorMin = new Vector2(0f, 0.5f);
        xpBar.anchorMax = new Vector2(1f, 0.5f);
        xpBar.offsetMin = new Vector2(62f, -3f);
        xpBar.offsetMax = new Vector2(0f, 3f);
        Image xpTrack = xpBar.gameObject.AddComponent<Image>();
        xpTrack.sprite = GameFX.WhiteSprite;
        xpTrack.color = new Color(0f, 0f, 0f, 0.55f);
        xpTrack.raycastTarget = false;
        xpFill = NewFill("Fill", xpBar, XPBarColor);
        xpFill.fillAmount = 0f;
    }

    void BuildAmmoPanel(TMP_Text template)
    {
        RectTransform panel = NewRect("AmmoPanel", root);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-18f, -10f);
        panel.sizeDelta = new Vector2(240f, 90f);

        weaponText = NewText(template, "Weapon", 18f, TextAlignmentOptions.TopRight, panel);
        weaponText.rectTransform.anchorMin = new Vector2(0f, 1f);
        weaponText.rectTransform.anchorMax = new Vector2(1f, 1f);
        weaponText.rectTransform.pivot = new Vector2(1f, 1f);
        weaponText.rectTransform.sizeDelta = new Vector2(0f, 22f);
        weaponText.color = new Color(1f, 1f, 1f, 0.7f);
        weaponText.characterSpacing = 4f;
        // long names ("MAGNUM REVOLVER AMMO") stay on one line and shrink rather than wrapping over the count
        weaponText.textWrappingMode = TextWrappingModes.NoWrap;
        weaponText.enableAutoSizing = true;
        weaponText.fontSizeMin = 11f;
        weaponText.fontSizeMax = 18f;

        roundsText = NewText(template, "Rounds", 40f, TextAlignmentOptions.TopRight, panel);
        roundsText.rectTransform.anchorMin = new Vector2(0f, 1f);
        roundsText.rectTransform.anchorMax = new Vector2(1f, 1f);
        roundsText.rectTransform.pivot = new Vector2(1f, 1f);
        roundsText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        roundsText.rectTransform.sizeDelta = new Vector2(0f, 46f);
        roundsText.fontStyle = FontStyles.Bold;

        grenadeText = NewText(template, "Grenades", 18f, TextAlignmentOptions.TopRight, panel);
        grenadeText.rectTransform.anchorMin = new Vector2(0f, 1f);
        grenadeText.rectTransform.anchorMax = new Vector2(1f, 1f);
        grenadeText.rectTransform.pivot = new Vector2(1f, 1f);
        grenadeText.rectTransform.anchoredPosition = new Vector2(0f, -66f);
        grenadeText.rectTransform.sizeDelta = new Vector2(0f, 22f);
        grenadeText.color = new Color(0.75f, 0.9f, 0.55f, 1f);
    }

    // full-screen card shown behind the scene's restart prompt when the player dies
    void BuildResultsCard(TMP_Text template)
    {
        RectTransform card = NewRect("Results", root);
        Stretch(card);
        Image dim = card.gameObject.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.05f, 0f, 0f, 0.72f);
        dim.raycastTarget = false;
        resultsGroup = card.gameObject.AddComponent<CanvasGroup>();
        resultsGroup.alpha = 0f;
        resultsGroup.blocksRaycasts = false;
        resultsGroup.interactable = false;

        resultsTitle = NewText(template, "Title", 64f, TextAlignmentOptions.Bottom, card);
        resultsTitle.rectTransform.anchorMin = new Vector2(0f, 0.72f);
        resultsTitle.rectTransform.anchorMax = new Vector2(1f, 0.82f);
        resultsTitle.rectTransform.offsetMin = resultsTitle.rectTransform.offsetMax = Vector2.zero;
        resultsTitle.fontStyle = FontStyles.Bold;
        resultsTitle.characterSpacing = 8f;

        resultsBody = NewText(template, "Body", 26f, TextAlignmentOptions.Top, card);
        resultsBody.rectTransform.anchorMin = new Vector2(0.1f, 0.54f);
        resultsBody.rectTransform.anchorMax = new Vector2(0.9f, 0.71f);
        resultsBody.rectTransform.offsetMin = resultsBody.rectTransform.offsetMax = Vector2.zero;
        resultsBody.lineSpacing = 12f;
    }

    void Update()
    {
        // damage flash on top of a slow pulse when health is low
        damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.deltaTime * 1.2f);
        float pulse = lowHealthAmount * (0.35f + 0.2f * Mathf.Sin(Time.time * 6f));
        vignette.color = new Color(0.6f, 0f, 0f, Mathf.Clamp01(Mathf.Max(damageAlpha, pulse)));

        crosshairPunch = Mathf.MoveTowards(crosshairPunch, 0f, Time.deltaTime * 4f);
        crosshair.localScale = Vector3.one * (1f + crosshairPunch * 0.6f) * (aimOnTarget ? 0.85f : 1f);
        Color restColor = aimOnTarget ? AimOnTarget : new Color(1f, 1f, 1f, 0.85f);
        crosshairImage.color = Color.Lerp(restColor, crosshairFlashColor, crosshairPunch);

        shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.deltaTime * 30f);
        root.anchoredPosition = shakeAmount > 0f ? Random.insideUnitCircle * shakeAmount : Vector2.zero;

        UpdateHealthBar();
        UpdateCallout();
        UpdateDamageNumbers();
        UpdateScreenBlood();
        xpFill.fillAmount = Mathf.MoveTowards(xpFill.fillAmount, xpTarget, Time.deltaTime * 1.5f);
    }

    // blood splats near the screen edges that slowly drip down and fade
    public void ScreenBlood(int count)
    {
        ScreenSplatter(count, BloodSplat);
    }

    // blood, or green for a spitter's acid
    public void ScreenSplatter(int count, Color color)
    {
        Vector2 half = root.rect.size * 0.5f;
        for (int i = 0; i < count; i++)
        {
            ScreenSplat splat = screenSplats[nextScreenSplat];
            nextScreenSplat = (nextScreenSplat + 1) % screenSplats.Count;

            // keep away from the centre so the crosshair stays clear
            Vector2 direction = Random.insideUnitCircle.normalized;
            splat.start = new Vector2(direction.x * half.x * Random.Range(0.55f, 0.95f), direction.y * half.y * Random.Range(0.45f, 0.9f));
            float size = Random.Range(260f, 520f);
            splat.image.rectTransform.sizeDelta = new Vector2(size, size);
            splat.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            splat.image.rectTransform.anchoredPosition = splat.start;
            splat.startTime = Time.time;
            splat.color = color;
            splat.image.gameObject.SetActive(true);
        }
    }

    void UpdateScreenBlood()
    {
        foreach (ScreenSplat splat in screenSplats)
        {
            if (!splat.image.gameObject.activeSelf) continue;
            float age = (Time.time - splat.startTime) / ScreenSplatLife;
            if (age >= 1f)
            {
                splat.image.gameObject.SetActive(false);
                continue;
            }
            splat.image.rectTransform.anchoredPosition = splat.start + Vector2.down * (age * 60f);
            Color color = splat.color;
            color.a *= age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
            splat.image.color = color;
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    public void SetXP(int level, float progress)
    {
        if (level != xpLevel)
        {
            // a new level starts the bar from empty, except on the first update
            if (xpLevel >= 0) xpFill.fillAmount = 0f;
            else xpFill.fillAmount = progress;
            xpLevel = level;
            xpLevelText.text = "LV " + level;
        }
        xpTarget = progress;
    }

    void UpdateHealthBar()
    {
        // the fill eases quickly, the ghost lingers then drains so each hit is visible
        healthShown = Mathf.MoveTowards(healthShown, healthTarget, Time.deltaTime * 2.5f);
        if (healthGhostShown < healthShown) healthGhostShown = healthShown;
        else if (Time.time > healthGhostHoldUntil) healthGhostShown = Mathf.MoveTowards(healthGhostShown, healthShown, Time.deltaTime * 0.6f);

        healthFill.fillAmount = healthShown;
        healthGhost.fillAmount = healthGhostShown;
        Color color = Color.Lerp(HealthLow, HealthHigh, Mathf.InverseLerp(0.2f, 0.7f, healthShown));
        // the bar throbs once health is critical
        if (healthShown < 0.25f) color = Color.Lerp(color, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 10f)));
        healthFill.color = color;
    }

    void UpdateCallout()
    {
        float age = Time.time - calloutStart;
        float pop = Mathf.Clamp01(age / 0.15f);
        calloutText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, pop);
        float alpha = Time.time < calloutUntil ? 1f : Mathf.Clamp01(1f - (Time.time - calloutUntil) / 0.3f);
        calloutText.alpha = alpha;
    }

    void UpdateDamageNumbers()
    {
        Camera view = Camera.main;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        foreach (DamageNumber number in damageNumbers)
        {
            if (!number.text.gameObject.activeSelf) continue;

            float age = (Time.time - number.startTime) / DamageNumberLife;
            Vector3 screen = view != null ? view.WorldToScreenPoint(number.worldPosition) : Vector3.back;
            if (age >= 1f || screen.z <= 0f)
            {
                number.text.gameObject.SetActive(false);
                continue;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
            // drift up, pop in, fade out
            number.text.rectTransform.anchoredPosition = local + new Vector2(0f, 30f + age * 70f);
            number.text.rectTransform.localScale = Vector3.one * number.scale * (age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f);
            number.text.alpha = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
        }
    }

    public void SetHealth(float current, float max)
    {
        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (fraction < healthTarget) healthGhostHoldUntil = Time.time + 0.4f;
        healthTarget = fraction;
        int value = Mathf.CeilToInt(Mathf.Max(current, 0f));
        if (value != healthShownValue)
        {
            healthShownValue = value;
            healthValue.text = "HEALTH  <color=#FFFFFF><b>" + value + "</b></color>";
        }
    }

    public void SetAmmo(string weapon, string rounds, Color color, int grenades)
    {
        weaponText.text = weapon;
        roundsText.text = rounds;
        roundsText.color = color;
        grenadeText.text = grenades > 0 ? "GRENADES  " + grenades : "<color=#777777>NO GRENADES</color>";
    }

    public void Callout(string message, Color color, float duration = 1.2f)
    {
        calloutText.text = message;
        calloutText.color = color;
        calloutStart = Time.time;
        calloutUntil = Time.time + duration;
    }

    public void ShowDamageNumber(Vector3 worldPosition, float damage, bool headshot, bool kill)
    {
        DamageNumber number = damageNumbers[nextDamageNumber];
        nextDamageNumber = (nextDamageNumber + 1) % damageNumbers.Count;

        // a little sideways jitter so a burst of hits doesn't stack into one number
        number.worldPosition = worldPosition + Random.insideUnitSphere * 0.08f;
        number.startTime = Time.time;
        number.scale = kill ? 1.35f : headshot ? 1.15f : 1f;
        number.text.text = Mathf.RoundToInt(damage).ToString();
        number.text.color = headshot ? new Color(1f, 0.85f, 0.15f) : kill ? new Color(1f, 0.35f, 0.25f) : Color.white;
        number.text.gameObject.SetActive(true);
    }

    public void SetAimOnTarget(bool onTarget)
    {
        aimOnTarget = onTarget;
    }

    // "TRAP 62%" under the crosshair while aiming at a catchable zombie; empty hides it
    public void SetCatchHint(string hint)
    {
        if (catchHint.text != hint) catchHint.text = hint;
    }

    public void ShowResults(string title, Color titleColor, string body)
    {
        resultsTitle.text = title;
        resultsTitle.color = titleColor;
        resultsBody.text = body;
        if (resultsRoutine != null) StopCoroutine(resultsRoutine);
        resultsRoutine = StartCoroutine(FadeResults(1f));
    }

    public void HideResults()
    {
        if (resultsRoutine != null) StopCoroutine(resultsRoutine);
        resultsGroup.alpha = 0f;
    }

    IEnumerator FadeResults(float target)
    {
        float start = resultsGroup.alpha;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            resultsGroup.alpha = Mathf.Lerp(start, target, t / 0.6f);
            resultsTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, t / 0.6f);
            yield return null;
        }
        resultsGroup.alpha = target;
        resultsTitle.rectTransform.localScale = Vector3.one;
    }

    public void FlashDamage(float strength = 0.5f)
    {
        damageAlpha = Mathf.Max(damageAlpha, strength);
        Shake(6f);
    }

    public void SetLowHealth(float amount)
    {
        lowHealthAmount = Mathf.Clamp01(amount);
    }

    public void Shake(float pixels)
    {
        shakeAmount = Mathf.Max(shakeAmount, pixels);
    }

    public void HitMarker(bool kill, bool headshot)
    {
        crosshairPunch = kill ? 1f : 0.6f;
        crosshairFlashColor = headshot ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1f, 0.15f, 0.15f, 1f);
    }

    public void SetCrosshairVisible(bool visible)
    {
        crosshair.gameObject.SetActive(visible);
    }

    // a wider ring shows the shotgun's pellet spread
    public void SetCrosshairWide(bool wide)
    {
        crosshair.sizeDelta = wide ? new Vector2(110f, 110f) : new Vector2(56f, 56f);
    }

    public void SetScore(int score, int combo)
    {
        scoreText.text = combo > 1
            ? "SCORE " + score.ToString("N0") + "   <color=#FFC832>x" + combo + " COMBO</color>"
            : "SCORE " + score.ToString("N0");
    }

    public void SetObjective(string text)
    {
        objectiveText.text = text;
    }

    public void SetBoss(string bossName, float fraction)
    {
        bossBar.SetActive(true);
        bossLabel.text = bossName;
        bossFill.fillAmount = Mathf.Clamp01(fraction);
    }

    public void HideBoss()
    {
        bossBar.SetActive(false);
    }

    public void ShowBanner(string title, string body, float duration)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(title, body, duration));
    }

    IEnumerator BannerRoutine(string title, string body, float duration)
    {
        bannerTitle.text = title;
        bannerBody.text = body;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            bannerGroup.alpha = t / 0.3f;
            bannerTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, t / 0.3f);
            yield return null;
        }
        bannerGroup.alpha = 1f;
        bannerTitle.rectTransform.localScale = Vector3.one;

        if (duration <= 0f) yield break;
        yield return new WaitForSeconds(duration);

        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            bannerGroup.alpha = 1f - t / 0.5f;
            yield return null;
        }
        bannerGroup.alpha = 0f;
    }

    public void HideBanner()
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerGroup.alpha = 0f;
    }

    // arrows around the screen edge pointing at zombies the player can't see
    public void UpdateThreats(List<ZombieController> zombies, Camera viewCamera)
    {
        int used = 0;
        if (viewCamera != null)
        {
            Vector2 radius = new Vector2(root.rect.width * 0.42f, root.rect.height * 0.40f);
            foreach (ZombieController zombie in zombies)
            {
                if (used >= threatArrows.Count) break;
                if (zombie == null || zombie.IsDead) continue;

                Vector3 viewport = viewCamera.WorldToViewportPoint(zombie.transform.position + Vector3.up);
                bool onScreen = viewport.z > 0f && viewport.x > 0.05f && viewport.x < 0.95f && viewport.y > 0.05f && viewport.y < 0.95f;
                if (onScreen) continue;

                // top of the screen is "ahead", bottom is "behind"
                Vector3 local = viewCamera.transform.InverseTransformPoint(zombie.transform.position);
                Vector2 direction = new Vector2(local.x, local.z);
                if (direction.sqrMagnitude < 0.0001f) continue;
                direction.Normalize();

                Image arrow = threatArrows[used++];
                arrow.gameObject.SetActive(true);
                arrow.rectTransform.anchoredPosition = new Vector2(direction.x * radius.x, direction.y * radius.y);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);

                // close zombies pulse and grow
                float distance = new Vector2(local.x, local.z).magnitude;
                float danger = Mathf.InverseLerp(5f, 1f, distance);
                float flicker = danger > 0.6f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 1f;
                arrow.color = new Color(1f, 0.15f, 0.1f, Mathf.Lerp(0.45f, 1f, danger) * flicker);
                arrow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.4f, danger) * (zombie.IsBoss ? 1.5f : 1f);
            }
        }

        for (int i = used; i < threatArrows.Count; i++)
        {
            if (threatArrows[i].gameObject.activeSelf) threatArrows[i].gameObject.SetActive(false);
        }
    }

    // ---------- Builders ----------

    Image NewImage(string name, Sprite sprite, RectTransform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.GetComponent<RectTransform>().SetParent(parent != null ? parent : root, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    RectTransform NewRect(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    Image NewFill(string name, RectTransform parent, Color color)
    {
        Image fill = NewImage(name, GameFX.WhiteSprite, parent);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = color;
        return fill;
    }

    RawImage NewRawImage(string name, Texture texture)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.GetComponent<RectTransform>().SetParent(root, false);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text NewText(TMP_Text template, string name, float size, TextAlignmentOptions alignment, RectTransform parent)
    {
        // clone an existing label so the HUD keeps the scene's font and style
        TMP_Text text = Instantiate(template, parent, false);
        text.name = name;
        // the template's pivot is on its left edge; centre it so positions and scale pops are symmetric
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.text = string.Empty;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        return text;
    }

    static void AnchorTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-32f, height);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }
}
