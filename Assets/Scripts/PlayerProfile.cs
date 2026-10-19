using System;
using UnityEngine;

// Survivor level, XP and daily play streak, saved on the device.
public static class PlayerProfile
{
    public const int MaxLevel = 30;
    // a full week of play in a row earns this bonus
    public const int StreakBonusXP = 500;

    const string XPKey = "Profile_XP";
    const string StreakKey = "Profile_Streak";
    const string LastPlayKey = "Profile_LastPlay";

    public static int TotalXP => PlayerPrefs.GetInt(XPKey, 0);
    public static int Streak => PlayerPrefs.GetInt(StreakKey, 0);

    public static int Level => LevelFromXP(TotalXP, out _);

    // 0..1 progress through the current level
    public static float LevelProgress
    {
        get
        {
            int level = LevelFromXP(TotalXP, out int intoLevel);
            return level >= MaxLevel ? 1f : intoLevel / (float)XPToNext(level);
        }
    }

    // XP needed to go from this level to the next
    public static int XPToNext(int level)
    {
        return 100 + 50 * (level - 1);
    }

    static int LevelFromXP(int xp, out int intoLevel)
    {
        int level = 1;
        while (level < MaxLevel && xp >= XPToNext(level))
        {
            xp -= XPToNext(level);
            level++;
        }
        intoLevel = xp;
        return level;
    }

    // returns how many levels were gained; every XP point also earns a Supply Point to spend
    public static int AddXP(int amount)
    {
        if (amount <= 0) return 0;
        int before = Level;
        PlayerPrefs.SetInt(SupplyKey, SupplyPoints + amount);
        PlayerPrefs.SetInt(XPKey, TotalXP + amount);
        PlayerPrefs.Save();
        return Level - before;
    }

    // ---------- Supply Points (shop currency) ----------

    const string SupplyKey = "Profile_Supply";

    public static int SupplyPoints
    {
        get
        {
            // players from before the shop start with the XP they had already earned
            if (!PlayerPrefs.HasKey(SupplyKey)) PlayerPrefs.SetInt(SupplyKey, TotalXP);
            return PlayerPrefs.GetInt(SupplyKey, 0);
        }
    }

    public static void AddSupply(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(SupplyKey, SupplyPoints + amount);
        PlayerPrefs.Save();
    }

    // returns false (and spends nothing) when there aren't enough points
    public static bool Spend(int amount)
    {
        if (amount < 0 || SupplyPoints < amount) return false;
        PlayerPrefs.SetInt(SupplyKey, SupplyPoints - amount);
        PlayerPrefs.Save();
        return true;
    }

    // ---------- Level rewards ----------

    public static int BonusStartingAmmo => (Level >= 3 ? 5 : 0) + (Level >= 10 ? 5 : 0) + (Level >= 25 ? 5 : 0);
    public static int BonusGrenades => (Level >= 5 ? 1 : 0) + (Level >= 15 ? 1 : 0) + (Level >= 30 ? 1 : 0);
    public static float BonusMaxHealth => (Level >= 8 ? 10f : 0f) + (Level >= 20 ? 10f : 0f);

    // what reaching this level unlocks, or empty when nothing
    public static string RewardFor(int level)
    {
        switch (level)
        {
            case 3: case 10: case 25: return "+5 starting ammo";
            case 5: case 15: case 30: return "+1 grenade every level";
            case 8: case 20: return "+10 max health";
            default: return string.Empty;
        }
    }

    public static int NextRewardLevel(int level)
    {
        for (int l = level + 1; l <= MaxLevel; l++)
        {
            if (RewardFor(l).Length > 0) return l;
        }
        return 0;
    }

    // ---------- Daily streak ----------

    // call once per session; returns the bonus XP awarded (on every 7th day in a row)
    public static int RegisterPlayToday()
    {
        string today = DateKey(DateTime.Now);
        string last = PlayerPrefs.GetString(LastPlayKey, string.Empty);
        if (last == today) return 0;

        int streak = last == DateKey(DateTime.Now.AddDays(-1)) ? Streak + 1 : 1;
        PlayerPrefs.SetInt(StreakKey, streak);
        PlayerPrefs.SetString(LastPlayKey, today);
        PlayerPrefs.Save();

        if (streak % 7 != 0) return 0;
        AddXP(StreakBonusXP);
        return StreakBonusXP;
    }

    public static string DateKey(DateTime date)
    {
        return date.ToString("yyyyMMdd");
    }
}
