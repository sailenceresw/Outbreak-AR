using System;
using System.Collections.Generic;
using UnityEngine;

public enum MissionType
{
    KillZombies, KillRunners, KillBrutes, Headshots, ReachCombo,
    GrenadeKills, GrenadeMultiKill, ShotgunKills, ClearLevels, StrongFinish, CollectPickups,
    // added later; kept at the end so saved mission types keep their numbers
    CatchZombies, CatchRare, ClearSites, WalkDistance
}

// Three daily missions, picked once per day and saved with their progress on the device.
public static class MissionSystem
{
    public const int MissionsPerDay = 3;

    class MissionInfo
    {
        public MissionType type;
        public int minTarget, maxTarget, xp;
        // the game level the player must have unlocked for this mission to be fair
        public int requiresUnlockedLevel;
        public string format;
        // used instead of format when the target is 1
        public string singular;
    }

    public class Mission
    {
        public MissionType type;
        public int target;
        public int progress;
        public bool claimed;
        public int xp;
        public string description;
        public bool Complete => progress >= target;
    }

    static readonly MissionInfo[] Catalog =
    {
        new MissionInfo { type = MissionType.KillZombies, minTarget = 15, maxTarget = 30, xp = 150, format = "Kill {0} zombies" },
        new MissionInfo { type = MissionType.KillRunners, minTarget = 5, maxTarget = 10, xp = 150, requiresUnlockedLevel = 1, format = "Kill {0} Runners" },
        new MissionInfo { type = MissionType.KillBrutes, minTarget = 1, maxTarget = 3, xp = 200, requiresUnlockedLevel = 2, format = "Defeat {0} Brutes", singular = "Defeat a Brute" },
        new MissionInfo { type = MissionType.Headshots, minTarget = 5, maxTarget = 12, xp = 150, format = "Land {0} headshots" },
        new MissionInfo { type = MissionType.ReachCombo, minTarget = 3, maxTarget = 5, xp = 150, format = "Reach a {0}x combo" },
        new MissionInfo { type = MissionType.GrenadeKills, minTarget = 3, maxTarget = 6, xp = 150, format = "Kill {0} zombies with grenades" },
        new MissionInfo { type = MissionType.GrenadeMultiKill, minTarget = 1, maxTarget = 2, xp = 200, format = "Get {0} grenade multi-kills", singular = "Get a grenade multi-kill" },
        new MissionInfo { type = MissionType.ShotgunKills, minTarget = 5, maxTarget = 10, xp = 150, requiresUnlockedLevel = 1, format = "Kill {0} zombies with the shotgun" },
        new MissionInfo { type = MissionType.ClearLevels, minTarget = 1, maxTarget = 3, xp = 200, format = "Clear {0} levels", singular = "Clear a level" },
        new MissionInfo { type = MissionType.StrongFinish, minTarget = 1, maxTarget = 2, xp = 250, format = "Clear {0} levels above half health", singular = "Clear a level above half health" },
        new MissionInfo { type = MissionType.CatchZombies, minTarget = 2, maxTarget = 4, xp = 200, format = "Catch {0} zombies with traps" },
        new MissionInfo { type = MissionType.CatchRare, minTarget = 1, maxTarget = 1, xp = 300, requiresUnlockedLevel = 2,
            format = "Catch {0} Spitters, Brutes or mutants", singular = "Catch a Spitter, Brute or mutant" },
        new MissionInfo { type = MissionType.ClearSites, minTarget = 1, maxTarget = 2, xp = 250,
            format = "Clear {0} outbreak sites on the map", singular = "Clear an outbreak site on the map" },
        new MissionInfo { type = MissionType.WalkDistance, minTarget = 5, maxTarget = 10, xp = 200,
            format = "Walk {0}00 m with the map open" },
        new MissionInfo { type = MissionType.CollectPickups, minTarget = 2, maxTarget = 4, xp = 100, format = "Collect {0} supply drops or power-ups" },
    };

    const string DateKey = "Missions_Date";
    const string Prefix = "Missions_";

    static List<Mission> today;

    // raised the moment a mission's target is reached
    public static event Action<Mission> MissionCompleted;

    public static List<Mission> Today
    {
        get
        {
            EnsureToday();
            return today;
        }
    }

    public static int UnclaimedCount
    {
        get
        {
            int count = 0;
            foreach (Mission m in Today) if (m.Complete && !m.claimed) count++;
            return count;
        }
    }

    // add progress to every mission of this type
    public static void Report(MissionType type, int amount = 1)
    {
        for (int i = 0; i < Today.Count; i++)
        {
            Mission mission = today[i];
            if (mission.type != type || mission.Complete) continue;
            SetProgress(i, mission.progress + amount);
        }
    }

    // for "reach X" missions: progress is the best value seen
    public static void ReportBest(MissionType type, int value)
    {
        for (int i = 0; i < Today.Count; i++)
        {
            Mission mission = today[i];
            if (mission.type != type || mission.Complete || value <= mission.progress) continue;
            SetProgress(i, value);
        }
    }

    // returns the XP awarded, 0 if the mission isn't ready
    public static int Claim(int index)
    {
        EnsureToday();
        if (index < 0 || index >= today.Count) return 0;
        Mission mission = today[index];
        if (!mission.Complete || mission.claimed) return 0;

        mission.claimed = true;
        PlayerPrefs.SetInt(Prefix + "Claimed_" + index, 1);
        PlayerPrefs.Save();
        return mission.xp;
    }

    static void SetProgress(int index, int value)
    {
        Mission mission = today[index];
        mission.progress = Mathf.Min(value, mission.target);
        PlayerPrefs.SetInt(Prefix + "Progress_" + index, mission.progress);
        PlayerPrefs.Save();
        if (mission.Complete) MissionCompleted?.Invoke(mission);
    }

    static void EnsureToday()
    {
        string date = PlayerProfile.DateKey(DateTime.Now);
        if (today != null && PlayerPrefs.GetString(DateKey, string.Empty) == date) return;

        if (PlayerPrefs.GetString(DateKey, string.Empty) != date) GenerateFor(date);
        Load();
    }

    // pick three different missions; the choice is saved so it stays fixed all day
    static void GenerateFor(string date)
    {
        System.Random random = new System.Random(int.Parse(date));
        List<MissionInfo> pool = new List<MissionInfo>();
        foreach (MissionInfo info in Catalog)
        {
            if (GameProgress.UnlockedLevel >= info.requiresUnlockedLevel) pool.Add(info);
        }

        for (int i = 0; i < MissionsPerDay; i++)
        {
            MissionInfo info = pool[random.Next(pool.Count)];
            pool.Remove(info);
            PlayerPrefs.SetInt(Prefix + "Type_" + i, (int)info.type);
            PlayerPrefs.SetInt(Prefix + "Target_" + i, random.Next(info.minTarget, info.maxTarget + 1));
            PlayerPrefs.SetInt(Prefix + "Progress_" + i, 0);
            PlayerPrefs.SetInt(Prefix + "Claimed_" + i, 0);
        }
        PlayerPrefs.SetString(DateKey, date);
        PlayerPrefs.Save();
    }

    static void Load()
    {
        today = new List<Mission>();
        for (int i = 0; i < MissionsPerDay; i++)
        {
            MissionType type = (MissionType)PlayerPrefs.GetInt(Prefix + "Type_" + i, 0);
            MissionInfo info = Array.Find(Catalog, c => c.type == type) ?? Catalog[0];
            int target = Mathf.Max(PlayerPrefs.GetInt(Prefix + "Target_" + i, info.minTarget), 1);
            today.Add(new Mission
            {
                type = type,
                target = target,
                progress = PlayerPrefs.GetInt(Prefix + "Progress_" + i, 0),
                claimed = PlayerPrefs.GetInt(Prefix + "Claimed_" + i, 0) == 1,
                xp = info.xp,
                description = target == 1 && !string.IsNullOrEmpty(info.singular) ? info.singular : string.Format(info.format, target),
            });
        }
    }
}
