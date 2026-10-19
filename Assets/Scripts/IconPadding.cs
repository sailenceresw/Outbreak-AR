using UnityEngine;
using TMPro;

// Keeps a button's icon sized to the button and its label clear of the icon, whatever size
// the button ends up at (most menu buttons are stretched by anchors after they're built).
public class IconPadding : MonoBehaviour
{
    public RectTransform icon;
    public TMP_Text label;

    void Start()
    {
        Apply();
    }

    void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    public void Apply()
    {
        if (icon == null) return;
        Rect box = ((RectTransform)transform).rect;
        float height = box.height;
        if (height <= 1f) return;

        bool iconOnly = label == null || string.IsNullOrWhiteSpace(label.text);
        float size = Mathf.Min(height * (iconOnly ? 0.62f : 0.56f), box.width * 0.4f);
        icon.sizeDelta = new Vector2(size, size);

        if (iconOnly)
        {
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            return;
        }

        float inset = Mathf.Min(height * 0.28f, box.width * 0.06f);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(inset, 0f);
        label.margin = new Vector4(inset + size * 0.85f, 0f, inset * 0.5f, 0f);
    }
}
