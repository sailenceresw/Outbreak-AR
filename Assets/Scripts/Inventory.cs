using UnityEngine;

public enum ItemId { GrenadePack, Adrenaline, AmmoCache, Trap }

public class ShopItem
{
    public ItemId id;
    public string name;
    public string description;
    public int cost;
    public int maxStack;
}

// Consumables bought in the shop or found at supply caches, saved on the device.
public static class Inventory
{
    static readonly ShopItem[] Catalog =
    {
        new ShopItem { id = ItemId.Trap, name = "Containment Trap", cost = 100, maxStack = 20,
            description = "Throw at a weakened zombie to catch it for your Zombie-dex." },
        new ShopItem { id = ItemId.GrenadePack, name = "Grenade Pack", cost = 250, maxStack = 5,
            description = "+2 grenades at the start of your next level." },
        new ShopItem { id = ItemId.Adrenaline, name = "Adrenaline Shot", cost = 600, maxStack = 3,
            description = "Cheat death once: revive with half health instead of dying." },
        new ShopItem { id = ItemId.AmmoCache, name = "Ammo Cache", cost = 150, maxStack = 5,
            description = "Doubles the ammo you start your next level with." },
    };

    const string Prefix = "Inventory_";

    public static ShopItem[] Items => Catalog;

    public static ShopItem Get(ItemId id)
    {
        foreach (ShopItem item in Catalog) if (item.id == id) return item;
        return Catalog[0];
    }

    public static int Count(ItemId id) => PlayerPrefs.GetInt(Prefix + id, 0);

    public static bool Buy(ItemId id)
    {
        ShopItem item = Get(id);
        if (Count(id) >= item.maxStack || !PlayerProfile.Spend(item.cost)) return false;
        Add(id, 1);
        return true;
    }

    // found at supply caches or given as rewards; respects the stack limit
    public static void Add(ItemId id, int amount)
    {
        PlayerPrefs.SetInt(Prefix + id, Mathf.Clamp(Count(id) + amount, 0, Get(id).maxStack));
        PlayerPrefs.Save();
    }

    // returns true if one was used
    public static bool Use(ItemId id)
    {
        if (Count(id) <= 0) return false;
        PlayerPrefs.SetInt(Prefix + id, Count(id) - 1);
        PlayerPrefs.Save();
        return true;
    }
}
