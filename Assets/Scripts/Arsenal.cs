using UnityEngine;

public enum WeaponId { Pistol, Revolver, Shotgun, SMG, Rifle }
public enum WeaponSlot { Sidearm, Heavy }
public enum UpgradeStat { Damage, FireRate, Ammo }

public class WeaponDefinition
{
    public WeaponId id;
    public string name;
    public string ammoName;
    public WeaponSlot slot;
    public float damage;
    // seconds between shots
    public float fireInterval;
    public int pellets = 1;
    public float spreadAngle;
    public float range = 30f;
    // full damage up to this distance, then falling to 40% at range
    public float falloffStart = 30f;
    public float headshotMultiplier = 2f;
    // keeps firing while the button is held
    public bool automatic;
    public float recoilAngle = 8f;
    public float shake;
    public int startAmmo;
    public int ammoPerCrate;
    public int cost;
    public int requiredLevel;
    public float soundPitch = 1.25f;
    public float soundVolume = 0.7f;
    public string description;
}

// Every gun in the game, plus what the player owns, has equipped and has upgraded (saved on the device).
public static class Arsenal
{
    public const int MaxUpgradeLevel = 3;
    static readonly int[] UpgradeCosts = { 300, 650, 1100 };

    static readonly WeaponDefinition[] Weapons =
    {
        new WeaponDefinition
        {
            id = WeaponId.Pistol, name = "PT-9 Pistol", ammoName = "AMMO", slot = WeaponSlot.Sidearm,
            damage = 50f, fireInterval = 0.2f, recoilAngle = 8f, startAmmo = 20, ammoPerCrate = 15,
            soundPitch = 1.25f, soundVolume = 0.7f,
            description = "Reliable sidearm. Two body shots or one headshot drops a walker.",
        },
        new WeaponDefinition
        {
            id = WeaponId.Revolver, name = "Magnum Revolver", ammoName = "AMMO", slot = WeaponSlot.Sidearm,
            damage = 105f, fireInterval = 0.55f, headshotMultiplier = 2.5f, recoilAngle = 16f, shake = 3f,
            startAmmo = 14, ammoPerCrate = 9, cost = 1200, requiredLevel = 4, soundPitch = 0.95f, soundVolume = 0.9f,
            description = "Slow, brutal. One shot kills walkers; headshots hit 2.5x.",
        },
        new WeaponDefinition
        {
            id = WeaponId.Shotgun, name = "Pump Shotgun", ammoName = "SHELLS", slot = WeaponSlot.Heavy,
            damage = 22f, fireInterval = 0.85f, pellets = 8, spreadAngle = 6f, range = 12f, falloffStart = 3f,
            recoilAngle = 18f, shake = 5f, startAmmo = 6, ammoPerCrate = 3, soundPitch = 0.9f, soundVolume = 1f,
            description = "Eight pellets. Devastating up close, weak at range.",
        },
        new WeaponDefinition
        {
            id = WeaponId.SMG, name = "Vector SMG", ammoName = "SMG ROUNDS", slot = WeaponSlot.Heavy,
            damage = 21f, fireInterval = 0.08f, spreadAngle = 2.5f, range = 20f, falloffStart = 10f,
            automatic = true, recoilAngle = 3f, shake = 1f, startAmmo = 60, ammoPerCrate = 30,
            cost = 1500, requiredLevel = 6, soundPitch = 1.45f, soundVolume = 0.55f,
            description = "Hold FIRE to spray. Shreds runners at close range.",
        },
        new WeaponDefinition
        {
            id = WeaponId.Rifle, name = "Assault Rifle", ammoName = "RIFLE ROUNDS", slot = WeaponSlot.Heavy,
            damage = 38f, fireInterval = 0.12f, spreadAngle = 1f, range = 35f, falloffStart = 25f,
            automatic = true, recoilAngle = 5f, shake = 1.5f, startAmmo = 45, ammoPerCrate = 25,
            cost = 2600, requiredLevel = 10, soundPitch = 1.1f, soundVolume = 0.75f,
            description = "Hold FIRE. Accurate, hard-hitting full auto for bosses and brutes.",
        },
    };

    const string OwnedPrefix = "Arsenal_Owned_";
    const string EquippedPrefix = "Arsenal_Equipped_";
    const string UpgradePrefix = "Arsenal_Upgrade_";

    public static WeaponDefinition[] All => Weapons;

    public static WeaponDefinition Get(WeaponId id)
    {
        foreach (WeaponDefinition weapon in Weapons) if (weapon.id == id) return weapon;
        return Weapons[0];
    }

    // the pistol and shotgun come free; the shotgun is still announced on its campaign level
    public static bool Owns(WeaponId id)
    {
        if (id == WeaponId.Pistol || id == WeaponId.Shotgun) return true;
        return PlayerPrefs.GetInt(OwnedPrefix + id, 0) == 1;
    }

    public static WeaponDefinition Equipped(WeaponSlot slot)
    {
        WeaponId fallback = slot == WeaponSlot.Sidearm ? WeaponId.Pistol : WeaponId.Shotgun;
        WeaponId id = (WeaponId)PlayerPrefs.GetInt(EquippedPrefix + slot, (int)fallback);
        return Owns(id) && Get(id).slot == slot ? Get(id) : Get(fallback);
    }

    public static void Equip(WeaponId id)
    {
        if (!Owns(id)) return;
        PlayerPrefs.SetInt(EquippedPrefix + Get(id).slot, (int)id);
        PlayerPrefs.Save();
    }

    // a gun found at a rare cache: owned for free, equipped from the shop like any other
    public static void Grant(WeaponId id)
    {
        PlayerPrefs.SetInt(OwnedPrefix + id, 1);
        PlayerPrefs.Save();
    }

    public static bool Buy(WeaponId id)
    {
        WeaponDefinition weapon = Get(id);
        if (Owns(id) || PlayerProfile.Level < weapon.requiredLevel) return false;
        if (!PlayerProfile.Spend(weapon.cost)) return false;
        PlayerPrefs.SetInt(OwnedPrefix + id, 1);
        Equip(id);
        return true;
    }

    // ---------- Upgrades ----------

    public static int UpgradeLevel(WeaponId id, UpgradeStat stat)
    {
        return PlayerPrefs.GetInt(UpgradePrefix + id + "_" + stat, 0);
    }

    // 0 when maxed
    public static int UpgradeCost(WeaponId id, UpgradeStat stat)
    {
        int level = UpgradeLevel(id, stat);
        return level >= MaxUpgradeLevel ? 0 : UpgradeCosts[level];
    }

    public static bool BuyUpgrade(WeaponId id, UpgradeStat stat)
    {
        int cost = UpgradeCost(id, stat);
        if (cost <= 0 || !Owns(id) || !PlayerProfile.Spend(cost)) return false;
        PlayerPrefs.SetInt(UpgradePrefix + id + "_" + stat, UpgradeLevel(id, stat) + 1);
        PlayerPrefs.Save();
        return true;
    }

    // +15% damage per level
    public static float DamageMultiplier(WeaponId id) => 1f + 0.15f * UpgradeLevel(id, UpgradeStat.Damage);
    // 8% faster per level
    public static float FireIntervalMultiplier(WeaponId id) => 1f - 0.08f * UpgradeLevel(id, UpgradeStat.FireRate);
    // +25% starting and crate ammo per level
    public static float AmmoMultiplier(WeaponId id) => 1f + 0.25f * UpgradeLevel(id, UpgradeStat.Ammo);

    public static string StatName(UpgradeStat stat)
    {
        switch (stat)
        {
            case UpgradeStat.Damage: return "Damage";
            case UpgradeStat.FireRate: return "Fire rate";
            default: return "Ammo";
        }
    }
}
