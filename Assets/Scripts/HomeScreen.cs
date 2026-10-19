using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Title screen shown over the camera: survivor card plus Play, Levels, Missions, Shop and Settings.
public class HomeScreen : MonoBehaviour
{
    TMP_Text template;
    TMP_Text levelText;
    TMP_Text detailText;
    TMP_Text playLabel;
    TMP_Text missionsLabel;
    TMP_Text dexLabel;
    TMP_Text mapLabel;
    Image avatarImage;
    Image xpFill;
    RectTransform root;
    GameObject settingsPanel;

    public static HomeScreen Show(Canvas canvas, TMP_Text template, Action onPlay, Action onMap,
        Action onLevels, Action onMissions, Action onShop, Action onDex)
    {
        GameObject panel = new GameObject("HomeScreen", typeof(RectTransform));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Stretch(rect, Vector2.zero, Vector2.one);
        rect.SetAsLastSibling();

        HomeScreen home = panel.AddComponent<HomeScreen>();
        home.template = template;
        home.root = rect;
        home.Build(onPlay, onMap, onLevels, onMissions, onShop, onDex);
        home.Refresh();
        return home;
    }

    void Build(Action onPlay, Action onMap, Action onLevels, Action onMissions, Action onShop, Action onDex)
    {
        // darken the camera behind the menu so it reads, but keep the room visible
        RawImage shade = NewShade(new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.55f));
        shade.raycastTarget = true;

        // key art: a horde rendered from the game's zombies, rising behind the menu
        Sprite hordeArt = Resources.Load<Sprite>("Art/HomeHorde");
        if (hordeArt != null)
        {
            GameObject art = new GameObject("KeyArt", typeof(RectTransform));
            RectTransform artRect = art.GetComponent<RectTransform>();
            artRect.SetParent(root, false);
            Stretch(artRect, new Vector2(-0.08f, 0f), new Vector2(1.08f, 0.7f));
            Image artImage = art.AddComponent<Image>();
            artImage.sprite = hordeArt;
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;
            artImage.color = new Color(1f, 1f, 1f, 0.85f);
        }

        // logo: a faint biohazard emblem behind the title, which glows blood red over a dark outline
        Sprite emblem = UIStyle.Icon("Outbreak");
        if (emblem != null)
        {
            GameObject mark = new GameObject("Emblem", typeof(RectTransform));
            RectTransform markRect = mark.GetComponent<RectTransform>();
            markRect.SetParent(root, false);
            Stretch(markRect, new Vector2(0.3f, 0.8f), new Vector2(0.7f, 0.97f));
            Image markImage = mark.AddComponent<Image>();
            markImage.sprite = emblem;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            markImage.color = new Color(0.75f, 0.1f, 0.08f, 0.22f);
        }

        TMP_Text title = NewText(root, "OUTBREAK", 76f, FontStyles.Bold, new Vector2(0f, 0.84f), new Vector2(1f, 0.93f));
        title.color = UIStyle.Blood;
        title.characterSpacing = 12f;
        StyleLogo(title);
        TMP_Text subtitle = NewText(root, "A R   Z O M B I E   S U R V I V A L", 20f, FontStyles.Bold, new Vector2(0f, 0.81f), new Vector2(1f, 0.85f));
        subtitle.color = new Color(1f, 1f, 1f, 0.75f);

        // survivor card
        GameObject card = new GameObject("SurvivorCard", typeof(RectTransform));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.SetParent(root, false);
        Stretch(cardRect, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.78f));
        Image cardImage = UIStyle.AddPanel(card, UIStyle.Panel);

        // tapping the card opens the full profile
        Button cardButton = card.AddComponent<Button>();
        cardButton.targetGraphic = cardImage;
        cardButton.onClick.AddListener(OpenProfile);

        RectTransform avatarRect = NewRectIn(cardRect, "Avatar", new Vector2(0.03f, 0.12f), new Vector2(0.19f, 0.88f));
        avatarImage = avatarRect.gameObject.AddComponent<Image>();
        avatarImage.preserveAspect = true;
        avatarImage.raycastTarget = false;

        levelText = NewText(cardRect, string.Empty, 28f, FontStyles.Bold, new Vector2(0.2f, 0.6f), new Vector2(0.97f, 0.92f));
        GameObject bar = new GameObject("XPBar", typeof(RectTransform));
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.SetParent(cardRect, false);
        Stretch(barRect, new Vector2(0.22f, 0.44f), new Vector2(0.95f, 0.54f));
        Image track = bar.AddComponent<Image>();
        track.sprite = GameFX.WhiteSprite;
        track.color = new Color(1f, 1f, 1f, 0.12f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.SetParent(barRect, false);
        Stretch(fillRect, Vector2.zero, Vector2.one);
        xpFill = fill.AddComponent<Image>();
        xpFill.sprite = GameFX.WhiteSprite;
        xpFill.type = Image.Type.Filled;
        xpFill.fillMethod = Image.FillMethod.Horizontal;
        xpFill.color = UIStyle.XP;
        detailText = NewText(cardRect, string.Empty, 19f, FontStyles.Normal, new Vector2(0.2f, 0.06f), new Vector2(0.97f, 0.4f));

        // the two ways to play first and largest, then a grid of the rest
        Button play = AddButton("PLAY", UIStyle.Blood, 40f, 0.52f, 0.63f, onPlay, icon: "Play");
        playLabel = play.GetComponentInChildren<TMP_Text>();
        Button map = AddButton("WORLD MAP", UIStyle.Toxic, 30f, 0.435f, 0.51f, onMap, icon: "Map");
        mapLabel = map.GetComponentInChildren<TMP_Text>();
        AddButton("LEVELS", UIStyle.Steel, 22f, 0.355f, 0.42f, onLevels, 0.12f, 0.49f, "Levels");
        missionsLabel = AddButton("MISSIONS", UIStyle.Gold, 22f, 0.355f, 0.42f, onMissions, 0.51f, 0.88f, "Missions").GetComponentInChildren<TMP_Text>();
        AddButton("SHOP", UIStyle.Gold, 22f, 0.275f, 0.34f, onShop, 0.12f, 0.49f, "Shop");
        dexLabel = AddButton("ZOMBIE-DEX", new Color(0.3f, 0.9f, 1f), 22f, 0.275f, 0.34f, onDex, 0.51f, 0.88f, "Dex").GetComponentInChildren<TMP_Text>();
        AddButton("SETTINGS", UIStyle.Steel, 20f, 0.2f, 0.255f, ShowSettings, icon: "Settings");

        TMP_Text hint = NewText(root, "Find a clear floor space. Zombies will rise from it.", 18f, FontStyles.Normal, new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.12f));
        hint.color = new Color(1f, 1f, 1f, 0.55f);
    }

    // a dark outline and a soft red glow underneath, on this text's own material copy
    static void StyleLogo(TMP_Text title)
    {
        Material material = title.fontMaterial;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(10, 0, 0, 255);
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 0.05f, 0.02f, 0.75f));
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.85f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
        }
    }

    public void Refresh()
    {
        int level = PlayerProfile.Level;
        levelText.text = PlayerProfile.Name.ToUpper() + "  <color=#5AB4FF>LV " + level + "</color>  <size=60%><color=#FFC832>" +
            PlayerProfile.Title(level).ToUpper() + "</color></size>";
        Sprite avatar = Resources.Load<Sprite>(PlayerProfile.Avatar);
        avatarImage.sprite = avatar != null ? avatar : Resources.Load<Sprite>("WeaponIcons/Pistol");
        xpFill.fillAmount = PlayerProfile.LevelProgress;
        int streak = PlayerProfile.Streak;
        detailText.text = "<color=#FFC832>" + PlayerProfile.SupplyPoints.ToString("N0") + " SP</color>     " +
            "Streak " + streak + (streak == 1 ? " day" : " days") + "     Best " + GameProgress.HighScore.ToString("N0");

        int next = GameProgress.UnlockedLevel;
        playLabel.text = "PLAY\n<size=45%><color=#DDDDDD>Level " + (next + 1) + ": " + LevelCatalog.Get(next).title + "</color></size>";

        int ready = MissionSystem.UnclaimedCount;
        missionsLabel.text = ready > 0 ? "MISSIONS  (" + ready + ")" : "MISSIONS";
        dexLabel.text = "ZOMBIE-DEX  <size=80%>" + ZombieDex.SpeciesCaught + "/" + ZombieDex.CatchableSpecies + "</size>";
        mapLabel.text = "WORLD MAP\n<size=50%><color=#DDDDDD>Walk to outbreak sites and caches</color></size>";
    }

    void OpenProfile()
    {
        GameAudio.Play(GameAudio.Pickup, 0.5f, 0.9f);
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) ProfileMenu.Show(canvas.rootCanvas, template, Refresh);
    }

    RectTransform NewRectIn(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect, min, max);
        return rect;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (visible)
        {
            transform.SetAsLastSibling();
            Refresh();
        }
    }

    // ---------- Settings ----------

    void ShowSettings()
    {
        if (settingsPanel != null) Destroy(settingsPanel);
        settingsPanel = new GameObject("Settings", typeof(RectTransform));
        RectTransform panel = settingsPanel.GetComponent<RectTransform>();
        panel.SetParent(root, false);
        Stretch(panel, Vector2.zero, Vector2.one);
        Image dim = settingsPanel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        GameObject card = new GameObject("Card", typeof(RectTransform));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.SetParent(panel, false);
        Stretch(cardRect, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
        UIStyle.AddPanel(card, UIStyle.Panel);

        TMP_Text title = NewText(cardRect, "SETTINGS", 36f, FontStyles.Bold, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f));
        title.characterSpacing = 8f;

        Button sound = null, vibration = null, graphics = null, reset = null;
        bool confirmReset = false;
        sound = CardButton(cardRect, SoundLabel(), UIStyle.XP, 0.75f, () =>
        {
            GameSettings.Sound = !GameSettings.Sound;
            sound.GetComponentInChildren<TMP_Text>().text = SoundLabel();
        });
        UIStyle.AddIcon(sound, "Sound");
        graphics = CardButton(cardRect, GraphicsLabel(), UIStyle.XP, 0.62f, () =>
        {
            GameSettings.Quality = GameSettings.HighQuality ? GraphicsQuality.Low : GraphicsQuality.High;
            graphics.GetComponentInChildren<TMP_Text>().text = GraphicsLabel();
        });
        UIStyle.AddIcon(graphics, "Graphics");
        vibration = CardButton(cardRect, VibrationLabel(), UIStyle.XP, 0.49f, () =>
        {
            GameSettings.Vibration = !GameSettings.Vibration;
            vibration.GetComponentInChildren<TMP_Text>().text = VibrationLabel();
#if UNITY_ANDROID || UNITY_IOS
            if (GameSettings.Vibration) Handheld.Vibrate();
#endif
        });
        UIStyle.AddIcon(vibration, "Vibration");
        Button tutorial = null;
        tutorial = CardButton(cardRect, TutorialCoach.Done ? "REPLAY TUTORIAL" : "TUTORIAL: ON", UIStyle.Gold, 0.34f, () =>
        {
            // the tips run again on the next fight on level 1
            TutorialCoach.Done = false;
            tutorial.GetComponentInChildren<TMP_Text>().text = "TUTORIAL: ON  <size=70%>(play level 1)</size>";
        });
        UIStyle.AddIcon(tutorial, "Missions");
        reset = CardButton(cardRect, "RESET PROGRESS", UIStyle.Blood, 0.19f, () =>
        {
            // two taps so it can't happen by accident
            if (!confirmReset)
            {
                confirmReset = true;
                reset.GetComponentInChildren<TMP_Text>().text = "TAP AGAIN TO ERASE ALL";
                return;
            }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            GameSettings.Apply();
            Refresh();
            Destroy(settingsPanel);
        });
        CardButton(cardRect, "CLOSE", UIStyle.Steel, 0.04f, () => Destroy(settingsPanel));
    }

    static string SoundLabel() => "SOUND:  " + (GameSettings.Sound ? "ON" : "OFF");
    static string GraphicsLabel() => GameSettings.HighQuality
        ? "GRAPHICS:  HIGH  <size=70%>(60 FPS)</size>"
        : "GRAPHICS:  LOW  <size=70%>(battery saver)</size>";
    static string VibrationLabel() => "VIBRATION:  " + (GameSettings.Vibration ? "ON" : "OFF");

    Button CardButton(RectTransform card, string label, Color accent, float bottom, Action onClick)
    {
        Button button = UIStyle.NewButton(card, template, label, accent, 22f, onClick);
        Stretch((RectTransform)button.transform, new Vector2(0.08f, bottom), new Vector2(0.92f, bottom + 0.105f));
        button.GetComponentInChildren<TMP_Text>().richText = true;
        return button;
    }

    // ---------- Builders ----------

    Button AddButton(string label, Color accent, float size, float bottom, float top, Action onClick,
        float left = 0.12f, float right = 0.88f, string icon = null)
    {
        Button button = UIStyle.NewButton(root, template, label, accent, size, () =>
        {
            GameAudio.Play(GameAudio.Pickup, 0.5f, 0.9f);
            onClick?.Invoke();
        });
        Stretch((RectTransform)button.transform, new Vector2(left, bottom), new Vector2(right, top));
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (icon != null) UIStyle.AddIcon(button, icon);
        return button;
    }

    RawImage NewShade(Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject("Shade", typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        Stretch(rect, min, max);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = GameFX.VignetteTexture;
        image.color = color;
        // a flat dim under the vignette
        Image flat = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>();
        flat.rectTransform.SetParent(rect, false);
        Stretch(flat.rectTransform, Vector2.zero, Vector2.one);
        flat.sprite = GameFX.WhiteSprite;
        flat.color = new Color(0f, 0f, 0f, 0.35f);
        flat.raycastTarget = false;
        return image;
    }

    TMP_Text NewText(RectTransform parent, string value, float size, FontStyles style, Vector2 min, Vector2 max)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        Stretch(text.rectTransform, min, max);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
