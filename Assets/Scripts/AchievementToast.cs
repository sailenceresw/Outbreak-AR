using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The banner that slides down from the top when an achievement unlocks, over any screen.
// Several unlocking together queue up and show one after another.
public class AchievementToast : MonoBehaviour
{
    static AchievementToast instance;
    readonly Queue<Achievement> queue = new Queue<Achievement>();
    Canvas canvas;
    TMP_Text template;
    bool showing;

    // hooks the banner to achievements; call once per scene with the canvas and a text to copy the font from
    public static void Attach(Canvas canvas, TMP_Text template)
    {
        if (instance != null) Destroy(instance.gameObject);
        GameObject go = new GameObject("AchievementToast");
        instance = go.AddComponent<AchievementToast>();
        instance.canvas = canvas;
        instance.template = template;
        Achievements.Unlocked += instance.Enqueue;
    }

    void OnDestroy()
    {
        Achievements.Unlocked -= Enqueue;
        if (instance == this) instance = null;
    }

    void Enqueue(Achievement achievement)
    {
        queue.Enqueue(achievement);
        if (!showing) StartCoroutine(ShowQueue());
    }

    IEnumerator ShowQueue()
    {
        showing = true;
        while (queue.Count > 0)
        {
            yield return Show(queue.Dequeue());
        }
        showing = false;
    }

    IEnumerator Show(Achievement achievement)
    {
        if (canvas == null || template == null) yield break;

        GameObject go = new GameObject("Toast", typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.SetAsLastSibling();
        rect.anchorMin = new Vector2(0.06f, 1f);
        rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 150f);
        Image panel = UIStyle.AddPanel(go, new Color(0.1f, 0.08f, 0.02f, 0.96f));
        panel.raycastTarget = false;

        // gold rim
        GameObject rimObject = new GameObject("Rim", typeof(RectTransform));
        RectTransform rim = rimObject.GetComponent<RectTransform>();
        rim.SetParent(rect, false);
        rim.anchorMin = Vector2.zero;
        rim.anchorMax = Vector2.one;
        rim.offsetMin = rim.offsetMax = Vector2.zero;
        Image rimImage = rimObject.AddComponent<Image>();
        rimImage.sprite = UIStyle.RoundedOutline;
        rimImage.type = Image.Type.Sliced;
        rimImage.color = UIStyle.Gold;
        rimImage.raycastTarget = false;

        Sprite trophy = UIStyle.Icon("Achievements");
        if (trophy != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
            RectTransform icon = iconObject.GetComponent<RectTransform>();
            icon.SetParent(rect, false);
            icon.anchorMin = new Vector2(0.03f, 0.15f);
            icon.anchorMax = new Vector2(0.2f, 0.85f);
            icon.offsetMin = icon.offsetMax = Vector2.zero;
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = trophy;
            iconImage.preserveAspect = true;
            iconImage.color = UIStyle.Gold;
            iconImage.raycastTarget = false;
        }

        Label(rect, "ACHIEVEMENT UNLOCKED", 16f, new Vector2(0.23f, 0.66f), new Vector2(0.97f, 0.92f), UIStyle.Gold);
        Label(rect, achievement.name.ToUpper(), 30f, new Vector2(0.23f, 0.33f), new Vector2(0.97f, 0.7f), Color.white);
        Label(rect, achievement.description + "   <color=#FFC832>" + Achievements.RewardLabel(achievement) + "</color>", 16f,
            new Vector2(0.23f, 0.06f), new Vector2(0.97f, 0.36f), new Color(1f, 1f, 1f, 0.7f));

        GameAudio.Play(GameAudio.Fanfare, 0.8f, 1.2f);

        // slide down, hold, slide back up; on real time so a hit-stop or pause doesn't stall it
        float hidden = 170f, shown = -24f;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(hidden, shown, Mathf.SmoothStep(0f, 1f, t / 0.35f)));
            yield return null;
        }
        rect.anchoredPosition = new Vector2(0f, shown);
        yield return new WaitForSecondsRealtime(3f);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(shown, hidden, Mathf.SmoothStep(0f, 1f, t / 0.35f)));
            yield return null;
        }
        Destroy(go);
    }

    void Label(RectTransform parent, string value, float size, Vector2 min, Vector2 max, Color color)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = "Label";
        foreach (Transform child in text.transform) Destroy(child.gameObject);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
