using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen board with the survivor level, daily streak and today's missions. Built at runtime.
public class MissionBoard : MonoBehaviour
{
    static readonly Color CardColor = new Color(0.1f, 0.12f, 0.1f, 0.95f);
    static readonly Color DoneCardColor = new Color(0.12f, 0.22f, 0.12f, 0.95f);
    static readonly Color XPColor = new Color(0.35f, 0.75f, 1f, 1f);
    static readonly Color ClaimColor = new Color(1f, 0.75f, 0.15f, 1f);

    class MissionRow
    {
        public Image card;
        public TMP_Text description;
        public TMP_Text progress;
        public Image fill;
        public Button claim;
        public Image claimImage;
        public TMP_Text claimLabel;
    }

    TMP_Text template;
    TMP_Text levelText;
    Image xpFill;
    TMP_Text xpText;
    TMP_Text rewardText;
    TMP_Text streakText;
    readonly List<MissionRow> rows = new List<MissionRow>();
    Action<int> onLevelsGained;
    Action onClosed;

    public static MissionBoard Show(Canvas canvas, TMP_Text textTemplate, Action<int> onLevelsGained, Action onClosed)
    {
        GameObject panel = new GameObject("MissionBoard", typeof(RectTransform));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Stretch(rect, Vector2.zero, Vector2.one);
        rect.SetAsLastSibling();

        // the dim also swallows taps meant for the game underneath
        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.02f, 0.02f, 0.02f, 0.97f);

        MissionBoard board = panel.AddComponent<MissionBoard>();
        board.template = textTemplate;
        board.onLevelsGained = onLevelsGained;
        board.onClosed = onClosed;
        board.Build(rect);
        board.Refresh();
        return board;
    }

    void Build(RectTransform root)
    {
        TMP_Text title = NewText(root, "MISSIONS", 40f, FontStyles.Bold);
        Stretch(title.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 0.97f));
        title.characterSpacing = 8f;

        // survivor level and XP
        levelText = NewText(root, string.Empty, 30f, FontStyles.Bold);
        Stretch(levelText.rectTransform, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.9f));

        RectTransform bar = NewRect(root, "XPBar");
        Stretch(bar, new Vector2(0.1f, 0.825f), new Vector2(0.9f, 0.84f));
        AddImage(bar.gameObject, new Color(1f, 1f, 1f, 0.12f));
        xpFill = NewFill(bar, XPColor);

        xpText = NewText(root, string.Empty, 20f, FontStyles.Normal);
        Stretch(xpText.rectTransform, new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.82f));
        xpText.color = new Color(1f, 1f, 1f, 0.75f);

        rewardText = NewText(root, string.Empty, 20f, FontStyles.Normal);
        Stretch(rewardText.rectTransform, new Vector2(0.06f, 0.755f), new Vector2(0.94f, 0.79f));
        rewardText.color = ClaimColor;

        streakText = NewText(root, string.Empty, 22f, FontStyles.Normal);
        Stretch(streakText.rectTransform, new Vector2(0.06f, 0.705f), new Vector2(0.94f, 0.75f));

        TMP_Text daily = NewText(root, "TODAY'S MISSIONS", 20f, FontStyles.Bold);
        Stretch(daily.rectTransform, new Vector2(0.06f, 0.665f), new Vector2(0.94f, 0.7f));
        daily.color = new Color(1f, 1f, 1f, 0.6f);
        daily.characterSpacing = 6f;

        for (int i = 0; i < MissionSystem.MissionsPerDay; i++)
        {
            float top = 0.655f - i * 0.15f;
            rows.Add(BuildRow(root, i, new Vector2(0.05f, top - 0.135f), new Vector2(0.95f, top)));
        }

        Button close = NewButton(root, "CLOSE", new Color(0.25f, 0.25f, 0.25f, 1f), out _, out _);
        Stretch((RectTransform)close.transform, new Vector2(0.3f, 0.07f), new Vector2(0.7f, 0.13f));
        close.onClick.AddListener(Close);
    }

    MissionRow BuildRow(RectTransform root, int index, Vector2 min, Vector2 max)
    {
        MissionRow row = new MissionRow();
        RectTransform card = NewRect(root, "Mission " + (index + 1));
        Stretch(card, min, max);
        row.card = AddImage(card.gameObject, CardColor);

        row.description = NewText(card, string.Empty, 24f, FontStyles.Bold);
        Stretch(row.description.rectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.66f, 0.92f));
        row.description.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform bar = NewRect(card, "Progress");
        Stretch(bar, new Vector2(0.04f, 0.3f), new Vector2(0.66f, 0.42f));
        AddImage(bar.gameObject, new Color(1f, 1f, 1f, 0.12f));
        row.fill = NewFill(bar, new Color(0.35f, 0.85f, 0.35f, 1f));

        row.progress = NewText(card, string.Empty, 18f, FontStyles.Normal);
        Stretch(row.progress.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.66f, 0.28f));
        row.progress.alignment = TextAlignmentOptions.MidlineLeft;
        row.progress.color = new Color(1f, 1f, 1f, 0.7f);

        row.claim = NewButton(card, string.Empty, ClaimColor, out row.claimImage, out row.claimLabel);
        Stretch((RectTransform)row.claim.transform, new Vector2(0.7f, 0.2f), new Vector2(0.96f, 0.8f));
        int captured = index;
        row.claim.onClick.AddListener(() => Claim(captured));
        return row;
    }

    void Refresh()
    {
        int level = PlayerProfile.Level;
        levelText.text = "SURVIVOR LEVEL " + level;
        xpFill.fillAmount = PlayerProfile.LevelProgress;
        xpText.text = level >= PlayerProfile.MaxLevel
            ? "MAX LEVEL"
            : Mathf.RoundToInt(PlayerProfile.LevelProgress * PlayerProfile.XPToNext(level)) + " / " + PlayerProfile.XPToNext(level) + " XP";

        int next = PlayerProfile.NextRewardLevel(level);
        rewardText.text = next > 0 ? "Level " + next + ":  " + PlayerProfile.RewardFor(next) : string.Empty;

        int streak = PlayerProfile.Streak;
        int toBonus = 7 - streak % 7;
        streakText.text = "Daily streak  <b>" + streak + (streak == 1 ? " day" : " days") + "</b>" +
            "   <color=#999999>" + (streak > 0 && toBonus == 7 ? "bonus earned today!" : toBonus + " more for +" + PlayerProfile.StreakBonusXP + " XP") + "</color>";

        List<MissionSystem.Mission> missions = MissionSystem.Today;
        for (int i = 0; i < rows.Count && i < missions.Count; i++)
        {
            MissionSystem.Mission mission = missions[i];
            MissionRow row = rows[i];
            row.description.text = mission.description;
            row.fill.fillAmount = mission.progress / (float)mission.target;
            row.progress.text = mission.progress + " / " + mission.target + "    <color=#5AB4FF>+" + mission.xp + " XP</color>";
            row.card.color = mission.Complete ? DoneCardColor : CardColor;

            bool claimable = mission.Complete && !mission.claimed;
            row.claim.interactable = claimable;
            row.claimImage.color = claimable ? ClaimColor : new Color(0.3f, 0.3f, 0.3f, 1f);
            row.claimLabel.text = mission.claimed ? "DONE" : mission.Complete ? "CLAIM" : "IN PROGRESS";
            row.claimLabel.color = claimable ? Color.black : new Color(1f, 1f, 1f, 0.6f);
            row.claimLabel.fontSize = mission.Complete ? 24f : 16f;
        }
    }

    void Claim(int index)
    {
        int xp = MissionSystem.Claim(index);
        if (xp <= 0) return;

        GameAudio.Play(GameAudio.Pickup, 0.8f);
        int levels = PlayerProfile.AddXP(xp);
        if (levels > 0)
        {
            GameAudio.Play(GameAudio.Fanfare, 0.9f);
            onLevelsGained?.Invoke(levels);
        }
        Refresh();
    }

    void Close()
    {
        GameAudio.Play(GameAudio.DryFire, 0.6f, 1.2f);
        Action closed = onClosed;
        Destroy(gameObject);
        closed?.Invoke();
    }

    // ---------- Builders ----------

    TMP_Text NewText(RectTransform parent, string value, float size, FontStyles style)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
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

    Button NewButton(RectTransform parent, string label, Color color, out Image image, out TMP_Text text)
    {
        RectTransform rect = NewRect(parent, "Button");
        image = AddImage(rect.gameObject, color);
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        text = NewText(rect, label, 24f, FontStyles.Bold);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    static RectTransform NewRect(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static Image AddImage(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = GameFX.WhiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image NewFill(RectTransform parent, Color color)
    {
        RectTransform rect = NewRect(parent, "Fill");
        Stretch(rect, Vector2.zero, Vector2.one);
        Image fill = AddImage(rect.gameObject, color);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        return fill;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
