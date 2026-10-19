using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Pause overlay: freezes the game and offers resume, restart, missions and home. Built at runtime.
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    // frame the menu closed on, so the same back press can't reopen it
    public static int ClosedFrame { get; private set; } = -1;

    Action onResume;

    public static PauseMenu Show(Canvas canvas, TMP_Text template, string levelName,
        Action onResume, Action onRestart, Action onMissions, Action onHome)
    {
        GameObject panel = new GameObject("PauseMenu", typeof(RectTransform));
        RectTransform root = panel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        // the dim also swallows taps meant for the fight underneath
        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        PauseMenu menu = panel.AddComponent<PauseMenu>();
        menu.onResume = onResume;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsOpen = true;

        GameObject card = new GameObject("Card", typeof(RectTransform));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.SetParent(root, false);
        cardRect.anchorMin = new Vector2(0.12f, 0.27f);
        cardRect.anchorMax = new Vector2(0.88f, 0.73f);
        cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
        UIStyle.AddPanel(card, UIStyle.Panel);

        TMP_Text title = NewLabel(template, cardRect, "PAUSED", 44f, new Vector2(0f, 0.82f), new Vector2(1f, 0.96f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 10f;
        TMP_Text subtitle = NewLabel(template, cardRect, levelName, 20f, new Vector2(0f, 0.74f), new Vector2(1f, 0.83f));
        subtitle.color = new Color(1f, 1f, 1f, 0.6f);

        AddButton(cardRect, template, "RESUME", UIStyle.Toxic, 0.56f, menu.Resume);
        AddButton(cardRect, template, "RESTART LEVEL", UIStyle.Gold, 0.40f, () => menu.Close(onRestart));
        AddButton(cardRect, template, "MISSIONS", UIStyle.XP, 0.24f, () => onMissions?.Invoke());
        AddButton(cardRect, template, "HOME", UIStyle.Blood, 0.08f, () => menu.Close(onHome));
        return menu;
    }

    void Update()
    {
        // Android back button resumes, like closing any other overlay
        if (BackPressed()) Resume();
    }

    // the Input System reports Android's back button as the keyboard's escape key
    public static bool BackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    void Resume()
    {
        Close(onResume);
    }

    void Close(Action then)
    {
        // always back to full speed: a hit-stop or slow-mo caught by the pause must not stick
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        Destroy(gameObject);
        then?.Invoke();
    }

    void OnDestroy()
    {
        // never leave the game frozen if the scene unloads under the menu
        if (IsOpen)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsOpen = false;
        }
    }

    static void AddButton(RectTransform card, TMP_Text template, string label, Color accent, float bottom, Action onClick)
    {
        Button button = UIStyle.NewButton(card, template, label, accent, 26f, onClick);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0.1f, bottom);
        rect.anchorMax = new Vector2(0.9f, bottom + 0.13f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        string icon = label.StartsWith("RESUME") ? "Play" : label.StartsWith("RESTART") ? "Swap"
            : label.StartsWith("MISSIONS") ? "Missions" : label.StartsWith("HOME") ? "Home" : null;
        if (icon != null) UIStyle.AddIcon(button, icon);
    }

    static TMP_Text NewLabel(TMP_Text template, RectTransform parent, string value, float size, Vector2 min, Vector2 max)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }
}
