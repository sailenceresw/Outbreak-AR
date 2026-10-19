using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shared look for menus and buttons: rounded dark panels with a coloured accent, built at runtime.
public static class UIStyle
{
    public static readonly Color Panel = new Color(0.06f, 0.07f, 0.06f, 0.94f);
    public static readonly Color PanelLight = new Color(0.12f, 0.14f, 0.12f, 0.96f);
    public static readonly Color Blood = new Color(0.78f, 0.12f, 0.1f, 1f);
    public static readonly Color Toxic = new Color(0.45f, 0.85f, 0.25f, 1f);
    public static readonly Color Gold = new Color(1f, 0.75f, 0.15f, 1f);
    public static readonly Color Steel = new Color(0.32f, 0.36f, 0.38f, 1f);
    public static readonly Color XP = new Color(0.35f, 0.75f, 1f, 1f);

    const int TextureSize = 64;
    const int Radius = 22;

    static Sprite rounded, roundedOutline;

    // 9-sliced rounded rectangle; stretch it to any size
    public static Sprite Rounded => rounded != null ? rounded : rounded = BuildSprite(false);

    // just the rim of the rounded rectangle, drawn over a button for an accent border
    public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : roundedOutline = BuildSprite(true);

    public static Image AddPanel(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    // restyle any existing uGUI button (including the scene's template buttons)
    public static void StyleButton(Button button, Color accent, string label = null, float fontSize = 0f)
    {
        if (button == null) return;
        Image background = button.targetGraphic as Image;
        if (background == null) background = button.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = Rounded;
            background.type = Image.Type.Sliced;
            background.color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.92f);
        }

        // accent rim on top of the fill
        Transform existing = button.transform.Find("Rim");
        Image rim = existing != null ? existing.GetComponent<Image>() : null;
        if (rim == null)
        {
            GameObject rimObject = new GameObject("Rim", typeof(RectTransform));
            RectTransform rect = rimObject.GetComponent<RectTransform>();
            rect.SetParent(button.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rim = rimObject.AddComponent<Image>();
            rim.raycastTarget = false;
        }
        rim.sprite = RoundedOutline;
        rim.type = Image.Type.Sliced;
        rim.color = accent;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label != null) text.text = label;
            if (fontSize > 0f) text.fontSize = fontSize;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4f;
        }
        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            if (label != null) text.text = label;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
        }
    }

    // a full-width button built from scratch, with a label cloned from a scene text for its font
    public static Button NewButton(RectTransform parent, TMP_Text template, string label, Color accent, float fontSize, Action onClick)
    {
        GameObject go = new GameObject(label + " Button", typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = AddPanel(go, accent);
        image.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = UnityEngine.Object.Instantiate(template, rect, false);
        text.name = "Label";
        foreach (Transform child in text.transform) UnityEngine.Object.Destroy(child.gameObject);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        StyleButton(button, accent, label, fontSize);
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    static Sprite BuildSprite(bool outlineOnly)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[TextureSize * TextureSize];
        float half = TextureSize / 2f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // signed distance to a rounded square, negative inside
                float px = Mathf.Abs(x + 0.5f - half) - (half - Radius);
                float py = Mathf.Abs(y + 0.5f - half) - (half - Radius);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - Radius;
                float fill = Mathf.Clamp01(0.5f - outside);
                float alpha = outlineOnly ? fill * Mathf.Clamp01(outside + 3.5f) : fill;
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        float border = Radius + 2;
        return Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
