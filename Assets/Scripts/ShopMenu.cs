using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Arsenal shop: buy and equip guns, upgrade them and stock up on items, paid for with Supply Points.
public class ShopMenu : MonoBehaviour
{
    enum Tab { Weapons, Upgrades, Items }

    TMP_Text template;
    RectTransform list;
    TMP_Text pointsText;
    readonly List<Button> tabButtons = new List<Button>();
    Tab tab = Tab.Weapons;
    WeaponId upgradeTarget = WeaponId.Pistol;
    Action onClosed;

    public static ShopMenu Show(Canvas canvas, TMP_Text template, Action onClosed)
    {
        GameObject panel = new GameObject("ShopMenu", typeof(RectTransform));
        RectTransform root = panel.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root, Vector2.zero, Vector2.one);
        root.SetAsLastSibling();

        Image dim = panel.AddComponent<Image>();
        dim.sprite = GameFX.WhiteSprite;
        dim.color = new Color(0.02f, 0.02f, 0.02f, 0.97f);

        ShopMenu shop = panel.AddComponent<ShopMenu>();
        shop.template = template;
        shop.onClosed = onClosed;
        shop.Build(root);
        shop.Refresh();
        return shop;
    }

    void Update()
    {
        if (PauseMenu.BackPressed()) Close();
    }

    void Build(RectTransform root)
    {
        TMP_Text title = NewText(root, "ARSENAL", 40f, FontStyles.Bold, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f));
        title.characterSpacing = 10f;

        pointsText = NewText(root, string.Empty, 24f, FontStyles.Bold, new Vector2(0f, 0.865f), new Vector2(1f, 0.91f));
        pointsText.color = UIStyle.Gold;

        string[] names = { "WEAPONS", "UPGRADES", "ITEMS" };
        for (int i = 0; i < names.Length; i++)
        {
            Tab captured = (Tab)i;
            Button button = UIStyle.NewButton(root, template, names[i], UIStyle.Steel, 20f, () => SelectTab(captured));
            Stretch((RectTransform)button.transform, new Vector2(0.04f + i * 0.31f, 0.8f), new Vector2(0.33f + i * 0.31f, 0.85f));
            tabButtons.Add(button);
        }

        list = NewRect(root, "List");
        Stretch(list, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.785f));

        Button close = UIStyle.NewButton(root, template, "CLOSE", UIStyle.Blood, 24f, Close);
        UIStyle.AddIcon(close, "Back");
        Stretch((RectTransform)close.transform, new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.12f));
    }

    void SelectTab(Tab selected)
    {
        tab = selected;
        GameAudio.Play(GameAudio.DryFire, 0.5f, 1.3f);
        Refresh();
    }

    void Refresh()
    {
        pointsText.text = PlayerProfile.SupplyPoints.ToString("N0") + " SUPPLY POINTS   <color=#5AB4FF>LV " + PlayerProfile.Level + "</color>";
        for (int i = 0; i < tabButtons.Count; i++)
        {
            UIStyle.StyleButton(tabButtons[i], i == (int)tab ? UIStyle.Gold : UIStyle.Steel);
        }

        foreach (Transform child in list) Destroy(child.gameObject);
        switch (tab)
        {
            case Tab.Weapons: BuildWeapons(); break;
            case Tab.Upgrades: BuildUpgrades(); break;
            default: BuildItems(); break;
        }
    }

    // ---------- Weapons ----------

    void BuildWeapons()
    {
        WeaponDefinition[] weapons = Arsenal.All;
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponDefinition weapon = weapons[i];
            bool owned = Arsenal.Owns(weapon.id);
            bool equipped = Arsenal.Equipped(weapon.slot).id == weapon.id;
            bool locked = !owned && PlayerProfile.Level < weapon.requiredLevel;

            string stats = (weapon.slot == WeaponSlot.Sidearm ? "SIDEARM" : "HEAVY") +
                "   DMG " + Mathf.RoundToInt(weapon.damage * weapon.pellets) +
                "   RPM " + Mathf.RoundToInt(60f / weapon.fireInterval) +
                (weapon.automatic ? "   AUTO" : string.Empty);

            string action;
            Color accent;
            Action onClick = null;
            if (equipped)
            {
                action = "EQUIPPED";
                accent = UIStyle.Toxic;
            }
            else if (owned)
            {
                action = "EQUIP";
                accent = UIStyle.XP;
                onClick = () => { Arsenal.Equip(weapon.id); GameAudio.Play(GameAudio.DryFire, 0.8f, 0.8f); Refresh(); };
            }
            else if (locked)
            {
                action = "LV " + weapon.requiredLevel;
                accent = UIStyle.Steel;
            }
            else
            {
                action = weapon.cost.ToString("N0") + " SP";
                accent = UIStyle.Gold;
                onClick = () => Purchase(Arsenal.Buy(weapon.id));
            }

            AddRow(i, weapons.Length, weapon.name, stats + "\n<color=#999999>" + weapon.description + "</color>", action, accent, onClick,
                1f, WeaponIcon(weapon.id), locked);
        }
    }

    // ---------- Upgrades ----------

    void BuildUpgrades()
    {
        // one row of owned guns to choose from, then the three stats for the chosen gun
        List<WeaponDefinition> owned = new List<WeaponDefinition>();
        foreach (WeaponDefinition weapon in Arsenal.All) if (Arsenal.Owns(weapon.id)) owned.Add(weapon);
        if (!Arsenal.Owns(upgradeTarget)) upgradeTarget = WeaponId.Pistol;

        for (int i = 0; i < owned.Count; i++)
        {
            WeaponDefinition weapon = owned[i];
            float width = 1f / owned.Count;
            Button button = UIStyle.NewButton(list, template, ShortName(weapon), weapon.id == upgradeTarget ? UIStyle.Gold : UIStyle.Steel, 16f,
                () => { upgradeTarget = weapon.id; Refresh(); });
            Stretch((RectTransform)button.transform, new Vector2(i * width + 0.005f, 0.88f), new Vector2((i + 1) * width - 0.005f, 1f));
        }

        UpgradeStat[] stats = { UpgradeStat.Damage, UpgradeStat.FireRate, UpgradeStat.Ammo };
        for (int i = 0; i < stats.Length; i++)
        {
            UpgradeStat stat = stats[i];
            int level = Arsenal.UpgradeLevel(upgradeTarget, stat);
            int cost = Arsenal.UpgradeCost(upgradeTarget, stat);
            string pips = new string('|', level) + "<color=#444444>" + new string('|', Arsenal.MaxUpgradeLevel - level) + "</color>";
            string effect = stat == UpgradeStat.Damage ? "+15% damage per level"
                : stat == UpgradeStat.FireRate ? "8% faster firing per level"
                : "+25% starting and crate ammo per level";

            AddRow(i, stats.Length, Arsenal.StatName(stat) + "  <size=120%>" + pips + "</size>",
                Arsenal.Get(upgradeTarget).name + "\n<color=#999999>" + effect + "</color>",
                cost > 0 ? cost.ToString("N0") + " SP" : "MAX", cost > 0 ? UIStyle.Gold : UIStyle.Toxic,
                cost > 0 ? () => Purchase(Arsenal.BuyUpgrade(upgradeTarget, stat)) : (Action)null,
                0.84f);
        }
    }

    static string ShortName(WeaponDefinition weapon)
    {
        string[] parts = weapon.name.Split(' ');
        return parts[parts.Length - 1].ToUpper();
    }

    // ---------- Items ----------

    void BuildItems()
    {
        ShopItem[] items = Inventory.Items;
        for (int i = 0; i < items.Length; i++)
        {
            ShopItem item = items[i];
            int held = Inventory.Count(item.id);
            bool full = held >= item.maxStack;
            AddRow(i, items.Length, item.name + "  <color=#5AB4FF>x" + held + "</color>",
                "<color=#999999>" + item.description + "</color>",
                full ? "FULL" : item.cost.ToString("N0") + " SP", full ? UIStyle.Steel : UIStyle.Gold,
                full ? (Action)null : () => Purchase(Inventory.Buy(item.id)),
                1f, UIStyle.Icon(item.id.ToString()));
        }
    }

    // ---------- Shared ----------

    void Purchase(bool success)
    {
        GameAudio.Play(success ? GameAudio.Pickup : GameAudio.DryFire, 0.8f, success ? 1f : 0.7f);
        if (!success) pointsText.text = "<color=#FF5040>NOT ENOUGH SUPPLY POINTS</color>";
        else Refresh();
    }

    // the gun's picture, rendered from its model in the editor (Tools > Outbreak > Render Weapon Icons)
    static Sprite WeaponIcon(WeaponId id)
    {
        return Resources.Load<Sprite>("WeaponIcons/" + id);
    }

    void AddRow(int index, int count, string title, string body, string action, Color accent, Action onClick,
        float top = 1f, Sprite picture = null, bool dimPicture = false)
    {
        float height = top / Mathf.Max(count, 4);
        float rowTop = top - index * height;
        RectTransform card = NewRect(list, "Row " + index);
        Stretch(card, new Vector2(0f, rowTop - height + 0.008f), new Vector2(1f, rowTop - 0.008f));
        UIStyle.AddPanel(card.gameObject, UIStyle.PanelLight);

        // with a picture, the text moves right to make room for it
        float textLeft = 0.04f;
        if (picture != null)
        {
            RectTransform pictureRect = NewRect(card, "Picture");
            Stretch(pictureRect, new Vector2(0.02f, 0.08f), new Vector2(0.3f, 0.92f));
            Image image = pictureRect.gameObject.AddComponent<Image>();
            image.sprite = picture;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // guns not yet unlocked show as a dark silhouette
            image.color = dimPicture ? new Color(0.1f, 0.1f, 0.1f, 0.9f) : Color.white;
            textLeft = 0.32f;
        }

        TMP_Text titleText = NewText(card, title, 28f, FontStyles.Bold, new Vector2(textLeft, 0.55f), new Vector2(0.7f, 0.95f));
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 18f;
        titleText.fontSizeMax = 28f;
        TMP_Text bodyText = NewText(card, body, 20f, FontStyles.Normal, new Vector2(textLeft, 0.05f), new Vector2(0.7f, 0.58f));
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 13f;
        bodyText.fontSizeMax = 20f;

        Button button = UIStyle.NewButton(card, template, action, accent, 22f, onClick);
        Stretch((RectTransform)button.transform, new Vector2(0.71f, 0.2f), new Vector2(0.97f, 0.8f));
        button.interactable = onClick != null;
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
