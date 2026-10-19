using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Zombie-dex screen: one card per kind of zombie with its rarity, how many have been caught
// and whether its mutant has been found. Unseen kinds stay hidden. Built at runtime.
public class DexMenu : MonoBehaviour
{
    TMP_Text template;
    Action onClosed;

    public static DexMenu Show(Canvas canvas, TMP_Text template, Action onClosed)
    {
        GameObject panel = new GameObject("DexMenu", typeof(RectTransform));
        RectTransform root = panel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root, Vector2.zero, Vector2.one);
        root.SetAsLastSibling();

        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.02f, 0.02f, 0.03f, 0.97f);

        DexMenu menu = panel.AddComponent<DexMenu>();
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
        TMP_Text title = NewText(root, "ZOMBIE-DEX", 40f, FontStyles.Bold, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f));
        title.characterSpacing = 8f;
        title.color = new Color(0.4f, 0.95f, 1f);

        NewText(root, ZombieDex.SpeciesCaught + " / " + ZombieDex.CatchableSpecies + " kinds caught     " +
            "<color=#66E6FF>" + Inventory.Count(ItemId.Trap) + " traps</color>", 22f, FontStyles.Normal,
            new Vector2(0f, 0.865f), new Vector2(1f, 0.905f));

        DexEntry[] entries = ZombieDex.Entries;
        const int columns = 2;
        int rows = Mathf.CeilToInt(entries.Length / (float)columns);
        for (int i = 0; i < entries.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float top = 0.85f - row * (0.7f / rows);
            float bottom = top - 0.7f / rows + 0.01f;
            AddCard(root, entries[i], new Vector2(0.04f + column * 0.47f, bottom), new Vector2(0.49f + column * 0.47f, top - 0.01f));
        }

        Button close = UIStyle.NewButton(root, template, "CLOSE", UIStyle.Blood, 24f, Close);
        Stretch((RectTransform)close.transform, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.11f));
    }

    void AddCard(RectTransform root, DexEntry entry, Vector2 min, Vector2 max)
    {
        GameObject card = new GameObject(entry.name, typeof(RectTransform));
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        Stretch(rect, min, max);

        int seen = ZombieDex.Seen(entry.type);
        int caught = ZombieDex.Caught(entry.type);
        int mutants = ZombieDex.MutantsCaught(entry.type);
        Color rarityColor = ZombieDex.RarityColor(entry.rarity);
        bool known = seen > 0 || caught > 0;

        UIStyle.AddPanel(card, known ? new Color(0.09f, 0.11f, 0.12f, 0.96f) : new Color(0.05f, 0.05f, 0.06f, 0.96f));

        // coloured stripe along the top shows the rarity
        GameObject stripe = new GameObject("Rarity", typeof(RectTransform));
        RectTransform stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.SetParent(rect, false);
        Stretch(stripeRect, new Vector2(0.06f, 0.9f), new Vector2(0.94f, 0.93f));
        Image stripeImage = stripe.AddComponent<Image>();
        stripeImage.sprite = GameFX.WhiteSprite;
        stripeImage.color = known ? rarityColor : new Color(0.25f, 0.25f, 0.25f);
        stripeImage.raycastTarget = false;

        TMP_Text name = NewText(rect, known ? entry.name.ToUpper() : "???", 24f, FontStyles.Bold, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.88f));
        TMP_Text rarity = NewText(rect, entry.rarity.ToString().ToUpper(), 15f, FontStyles.Bold, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.7f));
        rarity.color = known ? rarityColor : new Color(0.4f, 0.4f, 0.4f);
        rarity.characterSpacing = 6f;

        string stats = !entry.catchable ? (known ? "<color=#FFB040>Can't be trapped</color>" : string.Empty)
            : "Caught  <b>" + caught + "</b>" + (mutants > 0 ? "\n<color=#C080FF>Mutant caught  x" + mutants + "</color>" : "\n<color=#555555>Mutant not found</color>");
        NewText(rect, stats, 17f, FontStyles.Normal, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.6f));

        TMP_Text description = NewText(rect, known ? entry.description : "Not encountered yet.", 14f, FontStyles.Normal,
            new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.34f));
        description.color = new Color(1f, 1f, 1f, 0.55f);
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

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
