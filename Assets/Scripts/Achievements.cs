using System;
using System.Collections.Generic;
using UnityEngine;

public class Achievement
{
    public string id;
    public string name;
    public string description;
    public string group;
    public long target;
    public Func<long> progress;
    public int rewardSupply;
    public int rewardTraps;

    public long Progress => Math.Min(progress(), target);
    public bool Unlocked => PlayerPrefs.GetInt("Ach_" + id, 0) == 1;
}

// Long-term goals across combat, collecting, exploring and surviving. Each one pays out once,
// the moment its stat crosses the target; they're re-checked whenever a lifetime stat changes.
public static class Achievements
{
    // raised once per achievement, when it unlocks
    public static event Action<Achievement> Unlocked;

    static Achievement[] all;
    static bool listening;

    public static Achievement[] All => all ?? (all = Build());

    public static int UnlockedCount
    {
        get
        {
            int count = 0;
            foreach (Achievement achievement in All) if (achievement.Unlocked) count++;
            return count;
        }
    }

    static Achievement New(string id, string group, string name, string description, long target, Func<long> progress, int supply, int traps = 0)
    {
        return new Achievement
        {
            id = id, group = group, name = name, description = description,
            target = target, progress = progress, rewardSupply = supply, rewardTraps = traps,
        };
    }

    static Func<long> StatOf(Stat stat) => () => LifetimeStats.Get(stat);

    static Achievement[] Build()
    {
        return new[]
        {
            New("first_blood", "Combat", "First Blood", "Kill your first zombie", 1, StatOf(Stat.Kills), 50),
            New("centurion", "Combat", "Centurion", "Kill 100 zombies", 100, StatOf(Stat.Kills), 300),
            New("massacre", "Combat", "Massacre", "Kill 1,000 zombies", 1000, StatOf(Stat.Kills), 1500),
            New("sharpshooter", "Combat", "Sharpshooter", "50 headshot kills", 50, StatOf(Stat.HeadshotKills), 400),
            New("dead_eye", "Combat", "Dead Eye", "500 headshot kills", 500, StatOf(Stat.HeadshotKills), 1500),
            New("boom", "Combat", "Boom", "25 grenade kills", 25, StatOf(Stat.GrenadeKills), 300),
            New("giant_slayer", "Combat", "Giant Slayer", "Bring down a boss", 1, StatOf(Stat.BossesKilled), 400),
            New("kingslayer", "Combat", "Kingslayer", "Bring down 5 bosses", 5, StatOf(Stat.BossesKilled), 1200),

            New("first_catch", "Collector", "First Catch", "Trap your first zombie", 1, StatOf(Stat.Catches), 100, 2),
            New("zoologist", "Collector", "Zoologist", "Catch every kind that can be trapped", ZombieDex.CatchableSpecies, () => ZombieDex.SpeciesCaught, 1000, 5),
            New("mutant_hunter", "Collector", "Mutant Hunter", "Catch a glowing mutant", 1, StatOf(Stat.MutantsCaught), 400, 2),
            New("rare_breed", "Collector", "Rare Breed", "Catch 5 mutants", 5, StatOf(Stat.MutantsCaught), 1200, 5),

            New("first_steps", "Explorer", "First Steps", "Walk 1 km with the map open", 1000, StatOf(Stat.DistanceWalked), 150),
            New("marathon", "Explorer", "Marathon", "Walk 10 km with the map open", 10000, StatOf(Stat.DistanceWalked), 1000),
            New("site_cleaner", "Explorer", "Site Cleaner", "Clear 5 outbreak sites", 5, StatOf(Stat.SitesCleared), 500),
            New("nest_breaker", "Explorer", "Nest Breaker", "Destroy a boss nest", 1, StatOf(Stat.NestsDestroyed), 500),
            New("scavenger", "Explorer", "Scavenger", "Open 10 supply caches", 10, StatOf(Stat.CachesOpened), 400, 3),

            New("chapter_one", "Survivor", "Chapter One", "Reach campaign level 4", 4, () => GameProgress.UnlockedLevel + 1, 300),
            New("outbreak_survivor", "Survivor", "Outbreak Survivor", "Beat the whole campaign", 1, StatOf(Stat.CampaignWins), 2000),
            New("dedicated", "Survivor", "Dedicated", "Play 7 days in a row", 7, () => PlayerProfile.Streak, 700),
            New("veteran", "Survivor", "Veteran", "Reach survivor level 15", 15, () => PlayerProfile.Level, 1000),
        };
    }

    // starts re-checking on every stat change; also pays out anything already earned
    public static void Listen()
    {
        if (!listening)
        {
            listening = true;
            LifetimeStats.Changed += (stat, total) => Check();
        }
        Check();
    }

    public static void Check()
    {
        foreach (Achievement achievement in All)
        {
            if (achievement.Unlocked || achievement.progress() < achievement.target) continue;

            PlayerPrefs.SetInt("Ach_" + achievement.id, 1);
            PlayerProfile.AddSupply(achievement.rewardSupply);
            if (achievement.rewardTraps > 0) Inventory.Add(ItemId.Trap, achievement.rewardTraps);
            PlayerPrefs.Save();
            Unlocked?.Invoke(achievement);
        }
    }

    public static string RewardLabel(Achievement achievement)
    {
        return "+" + achievement.rewardSupply + " SP" + (achievement.rewardTraps > 0 ? "  +" + achievement.rewardTraps + " traps" : string.Empty);
    }
}
