using System;
using UnityEngine;

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

public class DexEntry
{
    public ZombieType type;
    public string name;
    public Rarity rarity;
    public bool catchable;
    public string description;
}

// The Zombie-dex: every kind of zombie, how rare it is, and what the player has seen and caught.
// Mutants are rare glowing variants of the catchable kinds. Saved on the device.
public static class ZombieDex
{
    public static readonly DexEntry[] Entries =
    {
        new DexEntry { type = ZombieType.Walker, name = "Walker", rarity = Rarity.Common, catchable = true,
            description = "Slow and relentless. The first sign of an outbreak." },
        new DexEntry { type = ZombieType.Runner, name = "Runner", rarity = Rarity.Common, catchable = true,
            description = "Freshly turned and fast. Weaves as it charges." },
        new DexEntry { type = ZombieType.Crawler, name = "Crawler", rarity = Rarity.Uncommon, catchable = true,
            description = "Drags itself low along the floor. Aim down." },
        new DexEntry { type = ZombieType.Spitter, name = "Spitter", rarity = Rarity.Rare, catchable = true,
            description = "Keeps its distance and lobs acid. Step aside to dodge." },
        new DexEntry { type = ZombieType.Brute, name = "Brute", rarity = Rarity.Epic, catchable = true,
            description = "Armoured with rotting muscle. Shrugs off small arms." },
        new DexEntry { type = ZombieType.Boss, name = "Boss", rarity = Rarity.Legendary, catchable = false,
            description = "The Brute King and the Abomination. Too strong to trap." },
    };

    const string SeenPrefix = "Dex_Seen_";
    const string CaughtPrefix = "Dex_Caught_";
    const string MutantPrefix = "Dex_Mutant_";

    public static DexEntry Get(ZombieType type)
    {
        return Array.Find(Entries, e => e.type == type) ?? Entries[0];
    }

    public static int Seen(ZombieType type) => PlayerPrefs.GetInt(SeenPrefix + type, 0);
    public static int Caught(ZombieType type) => PlayerPrefs.GetInt(CaughtPrefix + type, 0);
    public static int MutantsCaught(ZombieType type) => PlayerPrefs.GetInt(MutantPrefix + type, 0);

    public static int SpeciesCaught
    {
        get
        {
            int count = 0;
            foreach (DexEntry entry in Entries) if (Caught(entry.type) > 0) count++;
            return count;
        }
    }

    public static int CatchableSpecies
    {
        get
        {
            int count = 0;
            foreach (DexEntry entry in Entries) if (entry.catchable) count++;
            return count;
        }
    }

    public static void RecordSeen(ZombieType type)
    {
        PlayerPrefs.SetInt(SeenPrefix + type, Seen(type) + 1);
    }

    // returns true the first time this kind (or this kind's mutant) is caught
    public static bool RecordCaught(ZombieType type, bool mutant)
    {
        bool first = mutant ? MutantsCaught(type) == 0 : Caught(type) == 0;
        PlayerPrefs.SetInt(CaughtPrefix + type, Caught(type) + 1);
        if (mutant) PlayerPrefs.SetInt(MutantPrefix + type, MutantsCaught(type) + 1);
        PlayerPrefs.Save();
        return first;
    }

    // ---------- Catching ----------

    static float BaseChance(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return 0.5f;
            case Rarity.Uncommon: return 0.4f;
            case Rarity.Rare: return 0.3f;
            case Rarity.Epic: return 0.2f;
            default: return 0f;
        }
    }

    // weaker zombies are easier to trap: 40% of the base chance at full health, ~150% when nearly dead
    public static float CatchChance(ZombieType type, bool mutant, float healthFraction)
    {
        DexEntry entry = Get(type);
        if (!entry.catchable) return 0f;
        float missing = 1f - Mathf.Clamp01(healthFraction);
        float chance = BaseChance(entry.rarity) * (0.4f + 1.1f * missing);
        if (mutant) chance *= 0.6f;
        return Mathf.Clamp(chance, 0.02f, 0.95f);
    }

    public static int CatchXP(ZombieType type, bool mutant)
    {
        int xp;
        switch (Get(type).rarity)
        {
            case Rarity.Common: xp = 20; break;
            case Rarity.Uncommon: xp = 35; break;
            case Rarity.Rare: xp = 60; break;
            default: xp = 120; break;
        }
        return mutant ? xp * 3 : xp;
    }

    public static Color RarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return new Color(0.8f, 0.8f, 0.8f);
            case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.35f);
            case Rarity.Rare: return new Color(0.35f, 0.65f, 1f);
            case Rarity.Epic: return new Color(0.75f, 0.4f, 1f);
            default: return new Color(1f, 0.7f, 0.15f);
        }
    }

    public static readonly Color MutantColor = new Color(0.75f, 0.4f, 1f);
}
