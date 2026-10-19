using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen level picker built at runtime. Chapter 1 on the left, chapter 2 on the right.
public class LevelSelectMenu : MonoBehaviour
{
    const int LevelsPerColumn = 4;

    Action<int> onSelect;
    // backed out without picking a level
    Action onClosed;

    public static LevelSelectMenu Show(Canvas canvas, TMP_Text textTemplate, Action<int> onSelect, Action onClosed = null)
    {
        GameObject panel = new GameObject("LevelSelectMenu", typeof(RectTransform));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        Stretch(rect, Vector2.zero, Vector2.one);
        // on top of everything so nothing else swallows the taps
        rect.SetAsLastSibling();

        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0f, 0f, 0f, 0.82f);

        LevelSelectMenu menu = panel.AddComponent<LevelSelectMenu>();
        menu.onSelect = onSelect;
        menu.onClosed = onClosed;
        menu.Build(rect, textTemplate);
        return menu;
    }

    void Update()
    {
        // the phone's back button closes the picker like BACK does
        if (PauseMenu.BackPressed()) Close();
    }

    void Build(RectTransform root, TMP_Text template)
    {
        TMP_Text title = NewText(template, root, "SELECT LEVEL", 40f, FontStyles.Bold);
        Stretch(title.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.97f));

        TMP_Text best = NewText(template, root, "High score  " + GameProgress.HighScore.ToString("N0"), 22f, FontStyles.Normal);
        best.color = new Color(1f, 0.78f, 0.2f);
        Stretch(best.rectTransform, new Vector2(0f, 0.83f), new Vector2(1f, 0.88f));

        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.SetParent(root, false);
        Stretch(gridRect, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.82f));

        Button back = UIStyle.NewButton(root, template, "BACK", UIStyle.Steel, 24f, Close);
        Stretch((RectTransform)back.transform, new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.1f));

        int unlocked = GameProgress.UnlockedLevel;
        for (int i = 0; i < LevelCatalog.Count; i++)
        {
            int column = i / LevelsPerColumn;
            int row = i % LevelsPerColumn;
            Vector2 min = new Vector2(column * 0.5f + 0.015f, 1f - (row + 1f) / LevelsPerColumn + 0.015f);
            Vector2 max = new Vector2(column * 0.5f + 0.485f, 1f - (float)row / LevelsPerColumn - 0.015f);
            CreateLevelButton(gridRect, template, i, i <= unlocked, min, max);
        }
    }

    void CreateLevelButton(RectTransform parent, TMP_Text template, int index, bool unlocked, Vector2 min, Vector2 max)
    {
        LevelDefinition level = LevelCatalog.Get(index);

        GameObject go = new GameObject("Level " + (index + 1), typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect, min, max);

        Image background = go.AddComponent<Image>();
        background.sprite = GameFX.WhiteSprite;
        bool boss = level.objective == LevelObjective.Boss;
        background.color = !unlocked ? new Color(0.15f, 0.15f, 0.15f, 0.9f)
            : boss ? new Color(0.45f, 0.08f, 0.08f, 0.95f)
            : new Color(0.12f, 0.22f, 0.12f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = background;
        button.interactable = unlocked;
        int captured = index;
        button.onClick.AddListener(() => Select(captured));

        string objective = level.objective == LevelObjective.Survive ? "Survive"
            : boss ? "Boss fight"
            : "Eliminate";
        int levelBest = GameProgress.LevelBest(index);
        string label = unlocked
            ? "<b>" + (index + 1) + ". " + level.title + "</b>\n<size=70%><color=#BBBBBB>" + objective +
              (levelBest > 0 ? "  -  Best "+ levelBest.ToString("N0") : "") + "</color></size>"
            : "<b>" + (index + 1) + ". LOCKED</b>\n<size=70%><color=#888888>Clear level " + index + " to unlock</color></size>";

        TMP_Text text = NewText(template, rect, label, 24f, FontStyles.Normal);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(10f, 4f);
        text.rectTransform.offsetMax = new Vector2(-10f, -4f);
        if (!unlocked) text.color = new Color(0.6f, 0.6f, 0.6f);
    }

    void Select(int index)
    {
        GameAudio.Play(GameAudio.Pickup, 0.6f);
        Action<int> callback = onSelect;
        Destroy(gameObject);
        callback?.Invoke(index);
    }

    void Close()
    {
        GameAudio.Play(GameAudio.DryFire, 0.6f, 1.1f);
        Action callback = onClosed;
        Destroy(gameObject);
        callback?.Invoke();
    }

    static TMP_Text NewText(TMP_Text template, RectTransform parent, string value, float size, FontStyles style)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
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
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
