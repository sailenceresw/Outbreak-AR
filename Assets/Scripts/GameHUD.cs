using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Combat HUD built at runtime on the scene's existing canvas.
public class GameHUD : MonoBehaviour
{
    const int MaxThreatArrows = 8;

    RectTransform root;
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
        hud.Build(rect, textTemplate);
        return hud;
    }

    void Build(RectTransform rootRect, TMP_Text template)
    {
        root = rootRect;

        vignette = NewRawImage("Vignette", GameFX.VignetteTexture);
        Stretch(vignette.rectTransform);
        vignette.color = new Color(0.6f, 0f, 0f, 0f);

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
    }

    void Update()
    {
        // damage flash on top of a slow pulse when health is low
        damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.deltaTime * 1.2f);
        float pulse = lowHealthAmount * (0.35f + 0.2f * Mathf.Sin(Time.time * 6f));
        vignette.color = new Color(0.6f, 0f, 0f, Mathf.Clamp01(Mathf.Max(damageAlpha, pulse)));

        crosshairPunch = Mathf.MoveTowards(crosshairPunch, 0f, Time.deltaTime * 4f);
        crosshair.localScale = Vector3.one * (1f + crosshairPunch * 0.6f);
        crosshairImage.color = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), crosshairFlashColor, crosshairPunch);

        shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.deltaTime * 30f);
        root.anchoredPosition = shakeAmount > 0f ? Random.insideUnitCircle * shakeAmount : Vector2.zero;
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
        text.text = string.Empty;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.richText = true;
        text.enableWordWrapping = true;
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
