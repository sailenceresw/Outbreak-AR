using UnityEngine;

public enum LevelObjective { Eliminate, Survive, Boss }

[System.Serializable]
public class LevelDefinition
{
    public string chapter;
    public string title;
    public string story;
    public LevelObjective objective;
    // Eliminate: zombies to kill. Boss: escorts spawned alongside the boss. Survive: unused
    public int zombieCount;
    public float surviveSeconds;
    public float speedMultiplier = 1f;
    public int maxAlive = 4;
    public Vector2 spawnInterval = new Vector2(2f, 4f);
    // chance a spawn is a runner / brute instead of a walker
    public float runnerChance;
    public float bruteChance;
    // periodic bursts of zombies with a siren warning
    public bool hordeEvents;
    public int bonusAmmo;
}

public static class LevelCatalog
{
    public static readonly LevelDefinition[] Levels =
    {
        // ---------- Chapter 1: Outbreak ----------
        new LevelDefinition
        {
            chapter = "Chapter 1: Outbreak", title = "First Contact",
            story = "Something is moving out there.\nStay calm. Aim for the head.",
            objective = LevelObjective.Eliminate, zombieCount = 6,
            maxAlive = 3, spawnInterval = new Vector2(3f, 4.5f), bonusAmmo = 0,
        },
        new LevelDefinition
        {
            chapter = "Chapter 1: Outbreak", title = "They're Fast Now",
            story = "Some of them can run.\nDon't let them flank you.",
            objective = LevelObjective.Eliminate, zombieCount = 10,
            maxAlive = 4, runnerChance = 0.35f, bonusAmmo = 10,
        },
        new LevelDefinition
        {
            chapter = "Chapter 1: Outbreak", title = "Hold the Line",
            story = "They keep coming.\nSurvive until the rescue signal.",
            objective = LevelObjective.Survive, surviveSeconds = 60f,
            maxAlive = 5, spawnInterval = new Vector2(1.8f, 3f),
            runnerChance = 0.3f, bruteChance = 0.1f, bonusAmmo = 15,
        },
        new LevelDefinition
        {
            chapter = "Chapter 1: Outbreak", title = "The Brute",
            story = "Something big is breaking through.\nIt takes more than one bullet.",
            objective = LevelObjective.Boss, zombieCount = 6,
            maxAlive = 4, runnerChance = 0.3f, bonusAmmo = 20,
        },

        // ---------- Chapter 2: Nightfall ----------
        new LevelDefinition
        {
            chapter = "Chapter 2: Nightfall", title = "Nightfall",
            story = "The dead are restless after dark.\nListen for the siren.",
            objective = LevelObjective.Eliminate, zombieCount = 14, speedMultiplier = 1.15f,
            maxAlive = 5, runnerChance = 0.35f, bruteChance = 0.1f, hordeEvents = true, bonusAmmo = 5,
        },
        new LevelDefinition
        {
            chapter = "Chapter 2: Nightfall", title = "Surrounded",
            story = "They're coming from every side.\nKeep turning. Keep shooting.",
            objective = LevelObjective.Survive, surviveSeconds = 75f, speedMultiplier = 1.2f,
            maxAlive = 7, spawnInterval = new Vector2(1.2f, 2.2f),
            runnerChance = 0.4f, bruteChance = 0.15f, hordeEvents = true, bonusAmmo = 15,
        },
        new LevelDefinition
        {
            chapter = "Chapter 2: Nightfall", title = "No Way Out",
            story = "No more running.\nEvery bullet counts.",
            objective = LevelObjective.Eliminate, zombieCount = 18, speedMultiplier = 1.3f,
            maxAlive = 6, spawnInterval = new Vector2(1.2f, 2.5f),
            runnerChance = 0.4f, bruteChance = 0.2f, hordeEvents = true, bonusAmmo = 15,
        },
        new LevelDefinition
        {
            chapter = "Chapter 2: Nightfall", title = "The Abomination",
            story = "It's the source of the outbreak.\nEnd this. Now.",
            objective = LevelObjective.Boss, zombieCount = 10, speedMultiplier = 1.2f,
            maxAlive = 5, runnerChance = 0.4f, bruteChance = 0.15f, bonusAmmo = 30,
        },
    };

    // first level of each chapter scene, in build order
    static readonly int[] ChapterFirstLevels = { 0, 4 };

    // the shotgun becomes available from this level on
    public const int ShotgunUnlockLevel = 1;

    public static int Count => Levels.Length;

    public static int SceneForLevel(int index)
    {
        int scene = 0;
        for (int i = 0; i < ChapterFirstLevels.Length; i++)
        {
            if (index >= ChapterFirstLevels[i]) scene = i;
        }
        return scene;
    }

    public static LevelDefinition Get(int index)
    {
        return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
    }

    // the boss of chapter 1 is a big brute, the final boss is the abomination
    public static bool IsFinalBoss(int index)
    {
        return index == Levels.Length - 1;
    }
}

// Progress saved on the device between sessions.
public static class GameProgress
{
    const string UnlockedKey = "UnlockedLevel";
    const string HighScoreKey = "HighScore";
    const string LevelBestKey = "LevelBest_";

    // highest level index the player may start from
    public static int UnlockedLevel => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 0), 0, LevelCatalog.Count - 1);

    public static int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    public static void Unlock(int levelIndex)
    {
        if (levelIndex <= UnlockedLevel || levelIndex >= LevelCatalog.Count) return;
        PlayerPrefs.SetInt(UnlockedKey, levelIndex);
        PlayerPrefs.Save();
    }

    // returns true when this is a new overall high score
    public static bool SubmitHighScore(int score)
    {
        if (score <= HighScore) return false;
        PlayerPrefs.SetInt(HighScoreKey, score);
        PlayerPrefs.Save();
        return true;
    }

    public static int LevelBest(int levelIndex)
    {
        return PlayerPrefs.GetInt(LevelBestKey + levelIndex, 0);
    }

    public static void SubmitLevelScore(int levelIndex, int levelScore)
    {
        if (levelScore <= LevelBest(levelIndex)) return;
        PlayerPrefs.SetInt(LevelBestKey + levelIndex, levelScore);
        PlayerPrefs.Save();
    }
}
