using UnityEngine;

public enum Stat
{
    Kills,
    HeadshotKills,
    Catches,
    MutantsCaught,
    SitesCleared,
    NestsDestroyed,
    LevelsCleared,
    Deaths,
    ShotsFired,
    ShotsHit,
    GrenadeKills,
    BossesKilled,
    CachesOpened,
    // metres
    DistanceWalked,
    // seconds spent in fights
    TimePlayed,
    CampaignWins,
    HuntsCompleted,
}

// Lifetime totals for the profile screen and achievements, saved on the device.
public static class LifetimeStats
{
    // raised after any stat changes, with the stat and its new total
    public static event System.Action<Stat, long> Changed;

    const string Prefix = "Stat_";

    public static long Get(Stat stat)
    {
        // PlayerPrefs has no long; totals are kept as strings
        return long.TryParse(PlayerPrefs.GetString(Prefix + stat, "0"), out long value) ? value : 0;
    }

    public static void Add(Stat stat, long amount = 1, bool save = true)
    {
        if (amount <= 0) return;
        long total = Get(stat) + amount;
        PlayerPrefs.SetString(Prefix + stat, total.ToString());
        if (save) PlayerPrefs.Save();
        Changed?.Invoke(stat, total);
    }

    public static int AccuracyPercent
    {
        get
        {
            long fired = Get(Stat.ShotsFired);
            return fired > 0 ? Mathf.RoundToInt(100f * Get(Stat.ShotsHit) / fired) : 0;
        }
    }

    public static string TimePlayedLabel
    {
        get
        {
            long seconds = Get(Stat.TimePlayed);
            long hours = seconds / 3600;
            long minutes = seconds % 3600 / 60;
            return hours > 0 ? hours + "h " + minutes + "m" : minutes + "m";
        }
    }

    // fight time comes in every frame; it's banked here and written out every few seconds
    static float unsavedSeconds;

    public static void AddPlayTime(float seconds)
    {
        unsavedSeconds += seconds;
        if (unsavedSeconds < 10f) return;
        int whole = Mathf.FloorToInt(unsavedSeconds);
        unsavedSeconds -= whole;
        Add(Stat.TimePlayed, whole);
    }
}
