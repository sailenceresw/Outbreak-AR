using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Every achievement in a scrolling list: unlocked ones in gold, the rest with a progress bar. Built at runtime.
public class AchievementsMenu : MonoBehaviour
{
    const float RowHeight = 150f;

    TMP_Text template;
    Action onClosed;

    public static AchievementsMenu Show(Canvas canvas, TMP_Text template, Action onClosed)
    {
        GameObject panel = new GameObject("AchievementsMenu", typeof(RectTransform));
        RectTransform root = panel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root, Vector2.zero, Vector2.one);
        root.SetAsLastSibling();

        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.02f, 0.02f, 0.03f, 0.98f);

        AchievementsMenu menu = panel.AddComponent<AchievementsMenu>();
        menu.template = template;
        menu.onClosed = onClosed;
        menu.Build(root);
        return menu;
    }

    void Update()
    {
        if (PauseMenu.BackPressed()) Close();
    }

    void Build(RectTransform root)
    {
        TMP_Text title = NewText(root, "ACHIEVEMENTS", 40f, FontStyles.Bold, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f));
        title.color = UIStyle.Gold;
        title.characterSpacing = 8f;
        NewText(root, Achievements.UnlockedCount + " / " + Achievements.All.Length + " unlocked", 22f, FontStyles.Normal,
            new Vector2(0f, 0.87f), new Vector2(1f, 0.91f));

        // scrolling list: viewport (masked) > content (grows with rows)
        RectTransform viewport = NewRect(root, "Viewport");
        Stretch(viewport, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.86f));
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        Achievement[] all = Achievements.All;
        content.sizeDelta = new Vector2(0f, all.Length * RowHeight);
        content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        // unlocked first, then closest to done
        Achievement[] sorted = (Achievement[])all.Clone();
        Array.Sort(sorted, (a, b) =>
        {
            if (a.Unlocked != b.Unlocked) return a.Unlocked ? -1 : 1;
            return (b.Progress / (float)b.target).CompareTo(a.Progress / (float)a.target);
        });
        for (int i = 0; i < sorted.Length; i++) AddRow(content, sorted[i], i);

        Button close = UIStyle.NewButton(root, template, "CLOSE", UIStyle.Blood, 24f, Close);
        Stretch((RectTransform)close.transform, new Vector2(0.3f, 0.035f), new Vector2(0.7f, 0.095f));
        UIStyle.AddIcon(close, "Back");
    }

    void AddRow(RectTransform content, Achievement achievement, int index)
    {
        bool unlocked = achievement.Unlocked;
        RectTransform row = NewRect(content, achievement.id);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, RowHeight - 10f);
        row.anchoredPosition = new Vector2(0f, -index * RowHeight);
        UIStyle.AddPanel(row.gameObject, unlocked ? new Color(0.16f, 0.12f, 0.03f, 0.97f) : UIStyle.PanelLight);

        Sprite trophy = UIStyle.Icon("Achievements");
        if (trophy != null)
        {
            RectTransform iconRect = NewRect(row, "Icon");
            Stretch(iconRect, new Vector2(0.025f, 0.18f), new Vector2(0.16f, 0.82f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = trophy;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = unlocked ? UIStyle.Gold : new Color(1f, 1f, 1f, 0.18f);
        }

        TMP_Text name = NewText(row, achievement.name.ToUpper() + "  <size=60%><color=#888888>" + achievement.group.ToUpper() + "</color></size>",
            26f, FontStyles.Bold, new Vector2(0.19f, 0.6f), new Vector2(0.97f, 0.92f));
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.color = unlocked ? UIStyle.Gold : Color.white;

        TMP_Text description = NewText(row, achievement.description + "   <color=#FFC832>" + Achievements.RewardLabel(achievement) + "</color>",
            17f, FontStyles.Normal, new Vector2(0.19f, 0.36f), new Vector2(0.97f, 0.62f));
        description.alignment = TextAlignmentOptions.MidlineLeft;
        description.color = new Color(1f, 1f, 1f, 0.7f);
        description.enableAutoSizing = true;
        description.fontSizeMin = 11f;
        description.fontSizeMax = 17f;

        // progress bar and count, or a tick once done
        if (unlocked)
        {
            TMP_Text done = NewText(row, "UNLOCKED", 16f, FontStyles.Bold, new Vector2(0.19f, 0.08f), new Vector2(0.97f, 0.34f));
            done.alignment = TextAlignmentOptions.MidlineLeft;
            done.color = UIStyle.Toxic;
            done.characterSpacing = 4f;
            return;
        }
        RectTransform bar = NewRect(row, "Bar");
        Stretch(bar, new Vector2(0.19f, 0.14f), new Vector2(0.74f, 0.26f));
        Image track = bar.gameObject.AddComponent<Image>();
        track.sprite = GameFX.WhiteSprite;
        track.color = new Color(1f, 1f, 1f, 0.1f);
        RectTransform fillRect = NewRect(bar, "Fill");
        Stretch(fillRect, Vector2.zero, Vector2.one);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = GameFX.WhiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = achievement.Progress / (float)achievement.target;
        fill.color = UIStyle.XP;
        TMP_Text count = NewText(row, achievement.Progress.ToString("N0") + " / " + achievement.target.ToString("N0"), 15f, FontStyles.Bold,
            new Vector2(0.76f, 0.06f), new Vector2(0.97f, 0.34f));
        count.alignment = TextAlignmentOptions.MidlineRight;
    }

    void Close()
    {
        GameAudio.Play(GameAudio.DryFire, 0.6f, 1.2f);
        Action closed = onClosed;
        Destroy(gameObject);
        closed?.Invoke();
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
