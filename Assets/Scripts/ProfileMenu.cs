using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The survivor's profile: name, avatar, rank and level, and lifetime stats. Built at runtime.
public class ProfileMenu : MonoBehaviour
{
    TMP_Text template;
    Action onClosed;
    RectTransform root;
    RectTransform content;
    TouchScreenKeyboard keyboard;

    public static ProfileMenu Show(Canvas canvas, TMP_Text template, Action onClosed)
    {
        GameObject panel = new GameObject("ProfileMenu", typeof(RectTransform));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Stretch(rect, Vector2.zero, Vector2.one);
        rect.SetAsLastSibling();

        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.02f, 0.02f, 0.03f, 0.97f);

        ProfileMenu menu = panel.AddComponent<ProfileMenu>();
        menu.template = template;
        menu.onClosed = onClosed;
        menu.root = rect;
        menu.Rebuild();
        return menu;
    }

    void Update()
    {
        if (keyboard != null)
        {
            if (keyboard.status == TouchScreenKeyboard.Status.Done)
            {
                PlayerProfile.Name = keyboard.text;
                keyboard = null;
                Rebuild();
            }
            else if (keyboard.status != TouchScreenKeyboard.Status.Visible)
            {
                keyboard = null;
            }
            return;
        }
        if (PauseMenu.BackPressed()) Close();
    }

    void Rebuild()
    {
        if (content != null) Destroy(content.gameObject);
        content = NewRect(root, "Content");
        Stretch(content, Vector2.zero, Vector2.one);

        BuildHeader();
        BuildAvatarPicker();
        BuildStats();

        Button achievements = UIStyle.NewButton(content, template,
            "ACHIEVEMENTS  " + Achievements.UnlockedCount + " / " + Achievements.All.Length, UIStyle.Gold, 22f, () =>
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) AchievementsMenu.Show(canvas.rootCanvas, template, Rebuild);
            });
        Stretch((RectTransform)achievements.transform, new Vector2(0.12f, 0.11f), new Vector2(0.88f, 0.175f));
        UIStyle.AddIcon(achievements, "Achievements");

        Button close = UIStyle.NewButton(content, template, "CLOSE", UIStyle.Blood, 24f, Close);
        Stretch((RectTransform)close.transform, new Vector2(0.3f, 0.035f), new Vector2(0.7f, 0.095f));
        UIStyle.AddIcon(close, "Back");
    }

    // ---------- Header ----------

    void BuildHeader()
    {
        RectTransform card = NewRect(content, "Header");
        Stretch(card, new Vector2(0.04f, 0.8f), new Vector2(0.96f, 0.96f));
        UIStyle.AddPanel(card.gameObject, UIStyle.Panel);

        RectTransform frame = NewRect(card, "AvatarFrame");
        Stretch(frame, new Vector2(0.03f, 0.1f), new Vector2(0.27f, 0.9f));
        UIStyle.AddPanel(frame.gameObject, new Color(0.15f, 0.04f, 0.04f, 1f));
        AddPicture(frame, LoadAvatar(PlayerProfile.Avatar), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        int level = PlayerProfile.Level;
        TMP_Text name = NewText(card, PlayerProfile.Name.ToUpper(), 34f, FontStyles.Bold, new Vector2(0.31f, 0.55f), new Vector2(0.78f, 0.92f));
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.enableAutoSizing = true;
        name.fontSizeMin = 18f;
        name.fontSizeMax = 34f;

        Button edit = UIStyle.NewButton(card, template, "EDIT", UIStyle.Steel, 18f, EditName);
        Stretch((RectTransform)edit.transform, new Vector2(0.8f, 0.6f), new Vector2(0.97f, 0.88f));

        TMP_Text rank = NewText(card, "<color=#FFC832>" + PlayerProfile.Title(level).ToUpper() + "</color>   <color=#5AB4FF>LV " + level + "</color>",
            20f, FontStyles.Bold, new Vector2(0.31f, 0.3f), new Vector2(0.97f, 0.55f));
        rank.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform bar = NewRect(card, "XPBar");
        Stretch(bar, new Vector2(0.31f, 0.14f), new Vector2(0.95f, 0.22f));
        Image track = bar.gameObject.AddComponent<Image>();
        track.sprite = GameFX.WhiteSprite;
        track.color = new Color(1f, 1f, 1f, 0.12f);
        RectTransform fillRect = NewRect(bar, "Fill");
        Stretch(fillRect, Vector2.zero, Vector2.one);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = GameFX.WhiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = PlayerProfile.LevelProgress;
        fill.color = UIStyle.XP;
    }

    void EditName()
    {
        if (!TouchScreenKeyboard.isSupported) return;
        keyboard = TouchScreenKeyboard.Open(PlayerProfile.Name, TouchScreenKeyboardType.Default, false, false, false, false,
            "Survivor name", PlayerProfile.MaxNameLength);
    }

    // ---------- Avatar ----------

    // guns the player owns, zombies they've seen, and mutants they've caught
    static List<string> UnlockedAvatars()
    {
        List<string> avatars = new List<string>();
        foreach (WeaponDefinition weapon in Arsenal.All)
        {
            if (Arsenal.Owns(weapon.id)) avatars.Add("WeaponIcons/" + weapon.id);
        }
        foreach (DexEntry entry in ZombieDex.Entries)
        {
            if (ZombieDex.Seen(entry.type) > 0 || ZombieDex.Caught(entry.type) > 0) avatars.Add("DexPortraits/" + entry.type);
            if (ZombieDex.MutantsCaught(entry.type) > 0) avatars.Add("DexPortraits/" + entry.type + "_Mutant");
        }
        return avatars;
    }

    void BuildAvatarPicker()
    {
        TMP_Text heading = NewText(content, "AVATAR  <size=70%><color=#888888>unlock more by owning guns and finding zombies</color></size>",
            20f, FontStyles.Bold, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.795f));
        heading.alignment = TextAlignmentOptions.MidlineLeft;

        List<string> avatars = UnlockedAvatars();
        const int perRow = 6;
        float rowHeight = 0.065f;
        string current = PlayerProfile.Avatar;
        for (int i = 0; i < avatars.Count && i < perRow * 2; i++)
        {
            string avatar = avatars[i];
            int column = i % perRow, row = i / perRow;
            float left = 0.04f + column * (0.92f / perRow);
            float top = 0.745f - row * (rowHeight + 0.008f);

            RectTransform slot = NewRect(content, "Avatar " + avatar);
            Stretch(slot, new Vector2(left + 0.005f, top - rowHeight), new Vector2(left + 0.92f / perRow - 0.005f, top));
            Image background = UIStyle.AddPanel(slot.gameObject, avatar == current ? new Color(0.45f, 0.1f, 0.08f, 1f) : UIStyle.PanelLight);
            Button button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                GameAudio.Play(GameAudio.Pickup, 0.6f, 1.2f);
                PlayerProfile.Avatar = avatar;
                Rebuild();
            });
            AddPicture(slot, LoadAvatar(avatar), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        }
    }

    static Sprite LoadAvatar(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        return sprite != null ? sprite : Resources.Load<Sprite>("WeaponIcons/Pistol");
    }

    // ---------- Stats ----------

    void BuildStats()
    {
        TMP_Text heading = NewText(content, "LIFETIME STATS", 20f, FontStyles.Bold, new Vector2(0.05f, 0.565f), new Vector2(0.95f, 0.605f));
        heading.alignment = TextAlignmentOptions.MidlineLeft;

        float km = LifetimeStats.Get(Stat.DistanceWalked) / 1000f;
        (string label, string value)[] stats =
        {
            ("Zombies killed", LifetimeStats.Get(Stat.Kills).ToString("N0")),
            ("Headshot kills", LifetimeStats.Get(Stat.HeadshotKills).ToString("N0")),
            ("Zombies caught", LifetimeStats.Get(Stat.Catches).ToString("N0")),
            ("Mutants caught", LifetimeStats.Get(Stat.MutantsCaught).ToString("N0")),
            ("Accuracy", LifetimeStats.AccuracyPercent + "%"),
            ("Best score", GameProgress.HighScore.ToString("N0")),
            ("Levels cleared", LifetimeStats.Get(Stat.LevelsCleared).ToString("N0")),
            ("Bosses killed", LifetimeStats.Get(Stat.BossesKilled).ToString("N0")),
            ("Sites cleared", LifetimeStats.Get(Stat.SitesCleared).ToString("N0")),
            ("Nests destroyed", LifetimeStats.Get(Stat.NestsDestroyed).ToString("N0")),
            ("Distance walked", km.ToString("0.0") + " km"),
            ("Time in fights", LifetimeStats.TimePlayedLabel),
        };

        const int columns = 2;
        int rows = Mathf.CeilToInt(stats.Length / (float)columns);
        float top = 0.56f, bottom = 0.19f;
        float height = (top - bottom) / rows;
        for (int i = 0; i < stats.Length; i++)
        {
            int column = i % columns, row = i / columns;
            RectTransform tile = NewRect(content, stats[i].label);
            float tileTop = top - row * height;
            Stretch(tile, new Vector2(0.04f + column * 0.465f, tileTop - height + 0.006f), new Vector2(0.495f + column * 0.465f, tileTop - 0.006f));
            UIStyle.AddPanel(tile.gameObject, UIStyle.PanelLight);

            TMP_Text label = NewText(tile, stats[i].label.ToUpper(), 14f, FontStyles.Bold, new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.95f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(1f, 1f, 1f, 0.55f);
            label.characterSpacing = 3f;
            TMP_Text value = NewText(tile, stats[i].value, 30f, FontStyles.Bold, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.62f));
            value.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    // ---------- Shared ----------

    void Close()
    {
        GameAudio.Play(GameAudio.DryFire, 0.6f, 1.2f);
        Action closed = onClosed;
        Destroy(gameObject);
        closed?.Invoke();
    }

    static void AddPicture(RectTransform parent, Sprite sprite, Vector2 min, Vector2 max)
    {
        if (sprite == null) return;
        RectTransform rect = NewRect(parent, "Picture");
        Stretch(rect, min, max);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
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

    static RectTransform NewRect(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
