using System.Collections.Generic;
using UnityEngine;

// The modes started from home besides the campaign: Endless waves and the Hunt.
// Both run as encounters, like a map site fight, so they reuse the scene and the room scan.
public static class GameModes
{
    public const float WaveBreakSeconds = 8f;

    // strength follows the campaign, so later players get a tougher start
    static int Strength => Mathf.Clamp(GameProgress.UnlockedLevel, 0, 7);

    public static SiteEncounter Endless()
    {
        SiteEncounter encounter = new SiteEncounter
        {
            mutantChance = 0.06f,
            level = new LevelDefinition
            {
                chapter = "Endless", title = "Endless Night",
                story = "They never stop coming.\nHow long can you last?",
                objective = LevelObjective.Endless,
                bonusAmmo = 10,
            },
        };
        ApplyWave(encounter.level, 1);
        return encounter;
    }

    // each wave is bigger, faster and nastier than the one before
    public static void ApplyWave(LevelDefinition level, int wave)
    {
        int strength = Strength;
        level.zombieCount = 5 + wave * 2 + strength / 2;
        level.maxAlive = Mathf.Min(3 + wave / 2 + strength / 3, 9);
        level.speedMultiplier = 1f + Mathf.Min(wave * 0.04f + strength * 0.02f, 0.6f);
        level.runnerChance = Mathf.Min(0.15f + wave * 0.03f, 0.45f);
        level.bruteChance = wave >= 3 ? Mathf.Min(0.04f + wave * 0.015f, 0.2f) : 0f;
        level.spitterChance = wave >= 2 ? Mathf.Min(0.05f + wave * 0.015f, 0.18f) : 0f;
        level.crawlerChance = wave >= 2 ? 0.1f : 0f;
        level.hordeEvents = wave >= 4;
        float interval = Mathf.Max(2.6f - wave * 0.12f, 0.9f);
        level.spawnInterval = new Vector2(interval * 0.7f, interval * 1.3f);
    }

    public static SiteEncounter Hunt()
    {
        int strength = Strength;
        return new SiteEncounter
        {
            mutantChance = 0.18f,
            rewardSupply = 250 + strength * 25,
            rewardXP = 200 + strength * 20,
            rewardTraps = 2,
            level = new LevelDefinition
            {
                chapter = "Hunt", title = "The Hunt",
                story = "They're hiding somewhere in this room.\nFollow the radar. Find them first.",
                objective = LevelObjective.Hunt,
                zombieCount = Mathf.Clamp(3 + strength / 2, 3, 6),
                maxAlive = 6,
                runnerChance = 0.25f, crawlerChance = 0.2f, spitterChance = 0.1f, bruteChance = strength >= 4 ? 0.1f : 0f,
                speedMultiplier = 1.05f + strength * 0.02f,
                bonusAmmo = 10,
            },
        };
    }
}

// The best Endless runs on this device: wave reached and score.
public static class EndlessScores
{
    const string Key = "Endless_Scores";
    const int Keep = 10;

    public struct Run
    {
        public int wave;
        public int score;
    }

    public static List<Run> Top
    {
        get
        {
            List<Run> runs = new List<Run>();
            foreach (string entry in PlayerPrefs.GetString(Key, string.Empty).Split(';'))
            {
                string[] parts = entry.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int wave) && int.TryParse(parts[1], out int score))
                {
                    runs.Add(new Run { wave = wave, score = score });
                }
            }
            return runs;
        }
    }

    public static int BestWave => Top.Count > 0 ? Top[0].wave : 0;

    // returns the run's place in the top 10 (1-based), or 0 if it didn't make it
    public static int Submit(int wave, int score)
    {
        List<Run> runs = Top;
        Run run = new Run { wave = wave, score = score };
        runs.Add(run);
        runs.Sort((a, b) => a.wave != b.wave ? b.wave.CompareTo(a.wave) : b.score.CompareTo(a.score));
        if (runs.Count > Keep) runs.RemoveRange(Keep, runs.Count - Keep);

        List<string> parts = new List<string>();
        int place = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            parts.Add(runs[i].wave + "," + runs[i].score);
            if (place == 0 && runs[i].wave == wave && runs[i].score == score) place = i + 1;
        }
        PlayerPrefs.SetString(Key, string.Join(";", parts));
        PlayerPrefs.Save();
        return place;
    }
}
