using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameState = GameControllerScript.GameState;

// The run from start to end: scanning, level intros, objectives, level complete, victory, death and retry.
// Owns the game state, the current level and the run's score and stats.
public class LevelFlowManager : GameSystem
{
    const float ComboWindow = 2.5f;
    const int MaxCombo = 5;

    public event System.Action<GameState> StateChanged;
    // a level is being set up (true on a retry); its fight hasn't started yet
    public event System.Action<bool> LevelPrepared;
    // the level intro is over and zombies start rising
    public event System.Action LevelStarted;

    GameState state = GameState.Intro;
    public GameState State => state;
    public bool IsPlaying => state == GameState.Playing;

    public int CurrentLevelIndex { get; private set; }
    public LevelDefinition Level { get; private set; }
    // the current fight is a map site rather than a campaign level
    public SiteEncounter Encounter { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int ZombiesKilled { get; private set; }
    public int RunKills { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }

    int startLevelIndex;
    bool levelChosen;
    int levelStartScore;
    float surviveTimeLeft;
    float lastKillTime;
    Coroutine timeRoutine;

    protected override void OnBind()
    {
        Spawner.ZombieKilled += OnZombieKilled;
        Capture.ZombieCaptured += OnZombieCaptured;
        Player.Died += Lose;
    }

    public void SetState(GameState value)
    {
        if (state == value) return;
        state = value;
        StateChanged?.Invoke(value);
    }

    // reads how the scene was entered and sets up the run; returns true when the greeting is skipped
    public bool Setup()
    {
        SceneFlow.StartRequest request = Scenes.ConsumeStartRequest(Game.firstLevel);
        Encounter = request.encounter;
        startLevelIndex = request.startLevel;

        if (request.continuingChapter)
        {
            Scenes.RestoreCarriedRun(startLevelIndex);
        }
        else
        {
            ResetLoadout(startLevelIndex);
        }

        // the greeting only plays on a fresh start, later visits go straight to scanning
        bool skipGreeting = request.fromLevelSelect || request.continuingChapter;
        levelChosen = skipGreeting;

        Player.Refill();
        ZombiesKilled = 0;
        Level = LevelCatalog.Get(startLevelIndex);
        SetState(skipGreeting ? GameState.Scanning : GameState.Intro);
        return skipGreeting;
    }

    // starting loadout and stats for a fresh run from the given level
    void ResetLoadout(int levelIndex)
    {
        // the shop may have changed what's equipped since the last run
        Weapons.LoadEquipped();
        Score = 0;
        Weapons.ResetLoadout(levelIndex);
        RunKills = ShotsFired = ShotsHit = 0;
    }

    public void RestoreRun(int score, int kills, int shotsFired, int shotsHit)
    {
        Score = score;
        RunKills = kills;
        ShotsFired = shotsFired;
        ShotsHit = shotsHit;
    }

    // ---------- Starting a run ----------

    // the greeting's continue button: on to scanning the room
    public void EnterScanning()
    {
        SetState(GameState.Scanning);
        SetActiveSafe(Game.greetingPrompt, false);
        SetActiveSafe(Game.scanPrompt, true);
        UI.SetPlanesVisible(true);
    }

    // the scan prompt's start button
    public void OnStartPressed()
    {
        if (state != GameState.Intro && state != GameState.Scanning) return;
        SetActiveSafe(Game.scanPrompt, false);

        // returning players pick where to start; first-timers go straight in
        Canvas canvas = Game.playerHealthText != null ? Game.playerHealthText.canvas : null;
        if (!levelChosen && GameProgress.UnlockedLevel > 0 && canvas != null)
        {
            levelChosen = true;
            LevelSelectMenu.Show(canvas.rootCanvas, Game.playerHealthText, OnLevelSelected, () =>
            {
                // backed out: back to the scan prompt, which asks again next time
                levelChosen = false;
                SetActiveSafe(Game.scanPrompt, true);
            });
            return;
        }

        StartRun(startLevelIndex);
    }

    void OnLevelSelected(int index)
    {
        if (Scenes.OpenLevelInItsScene(index)) return;

        startLevelIndex = index;
        ResetLoadout(index);
        StartRun(index);
    }

    // a level picked from home: scan the room next, then fight
    public void ChooseLevel(int index)
    {
        if (Scenes.OpenLevelInItsScene(index)) return;

        startLevelIndex = index;
        ResetLoadout(index);
        levelChosen = true;
        UI.HideHomeScreen();
        EnterScanning();
    }

    void StartRun(int index)
    {
        SetActiveSafe(Game.gun, true);
        SetActiveSafe(Game.shootButton, true);
        Weapons.RefreshCrosshair();
        StartCoroutine(BeginLevel(index, false));
    }

    // ---------- Levels ----------

    IEnumerator BeginLevel(int index, bool isRetry)
    {
        CurrentLevelIndex = index;
        Level = Encounter != null ? Encounter.level : LevelCatalog.Get(index);
        SetState(GameState.LevelIntro);
        UI.SetPlanesVisible(false);
        // the HUD is hidden on the scan screen; it has to be live before the level banner starts
        if (Hud != null) Hud.SetVisible(true);

        bool newWeapon = Weapons.PrepareForLevel(index, isRetry, startLevelIndex, Level.bonusAmmo);
        UI.ShowCombatButtons(Weapons.HeavyUnlocked);
        if (Encounter == null) GameProgress.Unlock(index);

        // shop items kick in at the start of a level (not on a retry, so they aren't wasted twice)
        string itemNote = isRetry ? string.Empty : Weapons.UseLevelStartItems();

        levelStartScore = Score;
        Weapons.SnapshotLevelStart();

        ZombiesKilled = 0;
        surviveTimeLeft = Level.surviveSeconds;
        // Endless always starts (and restarts) on wave 1
        if (Level.objective == LevelObjective.Endless)
        {
            Wave = 1;
            waveBreak = false;
            GameModes.ApplyWave(Level, Wave);
        }
        LevelPrepared?.Invoke(isRetry);

        if (Hud != null)
        {
            Hud.SetCrosshairVisible(true);
            Hud.HideBoss();
            Hud.SetObjective(string.Empty);
            Hud.ShowBanner(Encounter != null ? Level.title.ToUpper() : "LEVEL " + (index + 1) + ": " + Level.title.ToUpper(),
                "<color=#9A9A9A>" + Level.chapter + "</color>\n" + Level.story +
                "\n\n<color=#FFC832>" + ObjectiveDescription() + "</color>" +
                (newWeapon ? "\n<color=#FF8C32>NEW WEAPON: " + Weapons.Heavy.name.ToUpper() + "  -  tap SWAP</color>" : "") +
                itemNote, 3.5f);
        }
        GameAudio.Play(GameAudio.Impact, 0.9f);
        if (newWeapon) GameAudio.Play(GameAudio.PowerUp, 0.7f, 0.8f);

        yield return new WaitForSeconds(4.2f);

        SetState(GameState.Playing);
        LevelStarted?.Invoke();
        ShowMessage("GO!", 1f);
    }

    string ObjectiveDescription()
    {
        switch (Level.objective)
        {
            case LevelObjective.Survive:
                return "Survive for " + Mathf.RoundToInt(Level.surviveSeconds) + " seconds";
            case LevelObjective.Boss:
                if (Encounter != null) return "Destroy the nest's guardian";
                return "Defeat " + (LevelCatalog.IsFinalBoss(CurrentLevelIndex) ? "the Abomination" : "the Brute King");
            case LevelObjective.Endless:
                return "Survive as many waves as you can";
            case LevelObjective.Hunt:
                return "Find and kill " + Level.zombieCount + " hidden zombies";
            default:
                return "Eliminate " + Level.zombieCount + " zombies";
        }
    }

    public void TickObjective()
    {
        string objective;
        switch (Level.objective)
        {
            case LevelObjective.Endless:
                if (waveBreak)
                {
                    objective = "WAVE " + (Wave + 1) + " INCOMING";
                    break;
                }
                objective = "WAVE " + Wave + "   LEFT " + Mathf.Max(Level.zombieCount - ZombiesKilled, 0);
                if (ZombiesKilled >= Level.zombieCount) StartCoroutine(NextWave());
                break;

            case LevelObjective.Hunt:
                objective = "HUNT   " + Mathf.Max(Level.zombieCount - ZombiesKilled, 0) + " LEFT";
                if (ZombiesKilled >= Level.zombieCount)
                {
                    CompleteLevel();
                    return;
                }
                break;

            case LevelObjective.Survive:
                surviveTimeLeft -= Time.deltaTime;
                int seconds = Mathf.CeilToInt(Mathf.Max(surviveTimeLeft, 0f));
                string time = (seconds / 60) + ":" + (seconds % 60).ToString("00");
                objective = seconds <= 10 ? "SURVIVE  <color=#FF4040>" + time + "</color>" : "SURVIVE  " + time;
                if (surviveTimeLeft <= 0f)
                {
                    CompleteLevel();
                    return;
                }
                break;

            case LevelObjective.Boss:
                objective = "DEFEAT THE BOSS";
                if (Spawner.BossSpawned)
                {
                    ZombieController boss = Spawner.Boss;
                    if (boss == null || boss.IsDead)
                    {
                        CompleteLevel();
                        return;
                    }
                    if (Hud != null) Hud.SetBoss(boss.DisplayName + (boss.IsEnraged ? "  [ENRAGED]" : ""), boss.zombieHealth / boss.MaxHealth);
                }
                break;

            default:
                objective = "ZOMBIES LEFT: " + Mathf.Max(Level.zombieCount - ZombiesKilled, 0);
                if (ZombiesKilled >= Level.zombieCount)
                {
                    CompleteLevel();
                    return;
                }
                break;
        }

        if (Hud != null) Hud.SetObjective(objective + Pickups.PowerUpStatus());
    }

    // ---------- Endless waves ----------

    public int Wave { get; private set; }
    bool waveBreak;

    // a short breather, then the next, harder wave
    IEnumerator NextWave()
    {
        waveBreak = true;
        int cleared = Wave;
        Progression.AwardXP(15 * cleared);
        Player.Heal(20f);
        GameAudio.Play(GameAudio.Fanfare, 0.6f, 1.15f);
        if (Hud != null) Hud.ShowBanner("WAVE " + cleared + " CLEARED", "+20 health\n<color=#FFC832>Wave " + (cleared + 1) + " in " +
            Mathf.RoundToInt(GameModes.WaveBreakSeconds) + " seconds</color>", 3f);

        Wave = cleared + 1;
        GameModes.ApplyWave(Level, Wave);
        ZombiesKilled = 0;
        Spawner.StartWave(GameModes.WaveBreakSeconds);

        yield return new WaitForSeconds(GameModes.WaveBreakSeconds);
        if (!IsPlaying) yield break;
        waveBreak = false;
        GameAudio.Play(GameAudio.Siren, 0.4f, 1.1f);
        ShowMessage("WAVE " + Wave, 1.5f, new Color(1f, 0.35f, 0.25f));
    }

    void CompleteLevel()
    {
        SetState(GameState.LevelComplete);
        StartCoroutine(LevelCompleteSequence());
    }

    IEnumerator LevelCompleteSequence()
    {
        // the final kill plays out in slow motion
        PlayTimeEffect(0.3f, 1.3f);

        if (Encounter != null)
        {
            yield return SiteClearedSequence();
            yield break;
        }

        Spawner.CollapseAll();

        int healthBonus = Mathf.RoundToInt(Player.Current) * 10;
        AddScore(healthBonus);
        GameAudio.Play(GameAudio.Fanfare, 0.8f);

        GameProgress.SubmitLevelScore(CurrentLevelIndex, Score - levelStartScore);
        GameProgress.Unlock(CurrentLevelIndex + 1);

        MissionSystem.Report(MissionType.ClearLevels);
        LifetimeStats.Add(Stat.LevelsCleared);
        if (Player.Current >= Player.Max * 0.5f) MissionSystem.Report(MissionType.StrongFinish);
        Progression.AwardXP(50 + 10 * CurrentLevelIndex);

        if (Hud != null)
        {
            Hud.HideBoss();
            Hud.SetObjective(string.Empty);
            Hud.ShowBanner("LEVEL CLEARED", "Health bonus  +" + healthBonus + "\nScore  " + Score.ToString("N0"), 3f);
        }

        yield return new WaitForSeconds(3.8f);

        int next = CurrentLevelIndex + 1;
        if (next <= Game.lastLevel && next < LevelCatalog.Count)
        {
            Player.Heal(Game.healBetweenLevels);
            yield return BeginLevel(next, false);
        }
        else if (next < LevelCatalog.Count && Scenes.HasNextChapterScene)
        {
            // next chapter lives in the next scene
            Scenes.CarryRunToNextChapter();
            GameProgress.SubmitHighScore(Score);
            if (Hud != null) Hud.ShowBanner("CHAPTER COMPLETE", "The night is only getting darker...", 0f);
            yield return new WaitForSeconds(2.5f);
            yield return Scenes.LoadNextChapter();
        }
        else
        {
            yield return Victory();
        }
    }

    IEnumerator SiteClearedSequence()
    {
        Spawner.CollapseAll();
        GameAudio.Play(GameAudio.Fanfare, 0.9f);

        bool hunt = Level.objective == LevelObjective.Hunt;
        if (!string.IsNullOrEmpty(Encounter.siteId))
        {
            MapSites.MarkCleared(Encounter.siteId);
            MissionSystem.Report(MissionType.ClearSites);
            LifetimeStats.Add(Stat.SitesCleared);
        }
        if (Encounter.nest) LifetimeStats.Add(Stat.NestsDestroyed);
        if (hunt) LifetimeStats.Add(Stat.HuntsCompleted);
        PlayerProfile.AddSupply(Encounter.rewardSupply);
        if (Encounter.rewardTraps > 0) Inventory.Add(ItemId.Trap, Encounter.rewardTraps);
        Progression.AwardXP(Encounter.rewardXP);
        GameProgress.SubmitHighScore(Score);

        yield return new WaitForSecondsRealtime(1.3f);

        SetState(GameState.Won);
        if (Hud != null)
        {
            Hud.HideBoss();
            Hud.SetObjective(string.Empty);
            Hud.SetCrosshairVisible(false);
            Hud.ShowBanner(Encounter.nest ? "NEST DESTROYED" : hunt ? "HUNT COMPLETE" : "SITE CLEARED",
                "<color=#FFC832>+" + Encounter.rewardSupply + " SP</color>   <color=#5AB4FF>+" + Encounter.rewardXP + " XP</color>" +
                (Encounter.rewardTraps > 0 ? "\n<color=#66E6FF>+" + Encounter.rewardTraps + " traps</color>" : string.Empty) +
                "\n\n<size=80%><color=#999999>Tap HOME to return</color></size>", 0f);
        }
    }

    IEnumerator Victory()
    {
        SetState(GameState.Won);
        LifetimeStats.Add(Stat.CampaignWins);
        bool newHighScore = GameProgress.SubmitHighScore(Score);
        if (Hud != null)
        {
            Hud.SetCrosshairVisible(false);
            Hud.ShowBanner("YOU SURVIVED THE OUTBREAK",
                "Final score  " + Score.ToString("N0") +
                (newHighScore ? "\n<color=#FFC832>NEW HIGH SCORE!</color>" : "\nBest  " + GameProgress.HighScore.ToString("N0")) +
                "\n<color=#BBBBBB>" + RunKills + " kills   " + AccuracyPercent() + "% accuracy</color>", 0f);
        }
        GameAudio.Play(GameAudio.Fanfare, 1f, 0.8f);

        yield return new WaitForSeconds(3.5f);

        if (Game.youWonPrompt != null)
        {
            yield return Scenes.Fade(0f, 1f);
            SetActiveSafe(Game.gun, false);
            if (Hud != null) Hud.HideBanner();
            Game.youWonPrompt.SetActive(true);
            yield return Scenes.Fade(1f, 0f);
        }
    }

    void Lose()
    {
        SetState(GameState.Lost);
        LifetimeStats.Add(Stat.Deaths);
        if (timeRoutine != null) StopCoroutine(timeRoutine);
        Time.timeScale = 1f;
        bool newHighScore = GameProgress.SubmitHighScore(Score);
        SetActiveSafe(Game.youLostPrompt, true);
        if (Hud != null)
        {
            Hud.HideBoss();
            Hud.HideBanner();
            Hud.SetCrosshairVisible(false);
            Hud.SetObjective(string.Empty);
            // the results card replaces the prompt's own text, which is hard to read over the camera
            SetPromptTextVisible(Game.youLostPrompt, false);
            string fell = "Fell on level " + (CurrentLevelIndex + 1) + ": " + Level.title;
            if (Level.objective == LevelObjective.Endless)
            {
                // an Endless run is over when you die; it goes on the board of best runs
                int place = EndlessScores.Submit(Wave, Score);
                fell = "Reached <color=#FFC832>wave " + Wave + "</color>" +
                    (place == 1 ? "   <color=#FFC832>BEST RUN!</color>" : place > 0 ? "   <color=#BBBBBB>#" + place + " of your runs</color>" : string.Empty);
                Achievements.Check();
            }
            else if (Encounter != null)
            {
                fell = "Fell at " + Level.title;
            }
            Hud.ShowResults("YOU DIED", new Color(0.95f, 0.15f, 0.1f),
                fell +
                "\n\n<color=#BBBBBB>Kills</color>  " + RunKills +
                "     <color=#BBBBBB>Accuracy</color>  " + AccuracyPercent() + "%" +
                "\n<color=#BBBBBB>Score</color>  " + Score.ToString("N0") +
                (newHighScore ? "     <color=#FFC832>NEW BEST!</color>" : "     <color=#BBBBBB>Best</color>  " + GameProgress.HighScore.ToString("N0")) +
                "\n\n<size=80%><color=#999999>Tap Restart to retry this level</color></size>");
        }
    }

    // retry the current level in place, so the player doesn't have to rescan the room
    public void RetryLevel()
    {
        // also used from the pause menu mid-fight, so stop the level intro and horde routines
        StopAllCoroutines();
        Spawner.ClearForRetry();
        Pickups.ClearAll();

        Time.timeScale = 1f;
        Score = levelStartScore;
        Weapons.RestoreForRetry();
        Player.Refill();
        Combo = 0;

        SetActiveSafe(Game.youLostPrompt, false);
        SetActiveSafe(Game.gun, true);
        SetActiveSafe(Game.shootButton, true);
        if (Hud != null) Hud.HideResults();
        StartCoroutine(BeginLevel(CurrentLevelIndex, true));
    }

    int AccuracyPercent()
    {
        return ShotsFired > 0 ? Mathf.RoundToInt(100f * ShotsHit / ShotsFired) : 0;
    }

    // hide a prompt's labels but keep its buttons (and their labels) visible
    static void SetPromptTextVisible(GameObject prompt, bool visible)
    {
        if (prompt == null) return;
        foreach (TMP_Text text in prompt.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>() == null) text.enabled = visible;
        }
        foreach (Text text in prompt.GetComponentsInChildren<Text>(true))
        {
            if (text.GetComponentInParent<Button>() == null) text.enabled = visible;
        }
    }

    // ---------- Score and kills ----------

    public void AddScore(int amount)
    {
        Score += amount;
    }

    public void RecordShot(bool hitZombie)
    {
        ShotsFired++;
        LifetimeStats.Add(Stat.ShotsFired, 1, false);
        if (hitZombie)
        {
            ShotsHit++;
            LifetimeStats.Add(Stat.ShotsHit, 1, false);
        }
    }

    public void TickCombo()
    {
        if (Combo > 0 && Time.time - lastKillTime > ComboWindow) Combo = 0;
    }

    void OnZombieKilled(ZombieController zombie)
    {
        if (!IsPlaying) return;

        ZombiesKilled++;
        RunKills++;
        LifetimeStats.Add(Stat.Kills, 1, false);
        if (zombie.IsBoss) LifetimeStats.Add(Stat.BossesKilled);
        if (Weapons.CurrentDamageSource == DamageSource.Grenade) LifetimeStats.Add(Stat.GrenadeKills, 1, false);

        Combo = Time.time - lastKillTime <= ComboWindow ? Mathf.Min(Combo + 1, MaxCombo) : 1;
        lastKillTime = Time.time;
        AddScore(zombie.ScoreValue * Combo);
        if (Combo >= 3) ShowMessage(Combo + "x COMBO!", 1.5f, new Color(1f, 0.55f, 0.15f));

        MissionSystem.Report(MissionType.KillZombies);
        if (zombie.Type == ZombieType.Runner) MissionSystem.Report(MissionType.KillRunners);
        if (zombie.Type == ZombieType.Brute || zombie.IsBoss) MissionSystem.Report(MissionType.KillBrutes);
        if (Weapons.CurrentDamageSource == DamageSource.Grenade) MissionSystem.Report(MissionType.GrenadeKills);
        if (Weapons.CurrentDamageSource == DamageSource.Gun && Weapons.ActiveWeapon.id == WeaponId.Shotgun) MissionSystem.Report(MissionType.ShotgunKills);
        MissionSystem.ReportBest(MissionType.ReachCombo, Combo);
        Progression.AwardXP(zombie.ScoreValue / 10);

        // a hair of hit-stop sells every kill
        PlayTimeEffect(0.05f, zombie.IsBoss ? 0.25f : 0.05f);
    }

    // a catch takes the zombie out of the fight just like a kill
    void OnZombieCaptured(ZombieController zombie)
    {
        if (!IsPlaying) return;
        ZombiesKilled++;
        AddScore(zombie.ScoreValue * 2);
    }

    // ---------- Time effects ----------

    // hit-stop and slow motion; never fights the pause menu for the time scale
    public void PlayTimeEffect(float timeScale, float realSeconds)
    {
        if (PauseMenu.IsOpen) return;
        if (timeRoutine != null) StopCoroutine(timeRoutine);
        timeRoutine = StartCoroutine(TimeEffect(timeScale, realSeconds));
    }

    IEnumerator TimeEffect(float timeScale, float realSeconds)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(realSeconds);
        if (!PauseMenu.IsOpen) Time.timeScale = 1f;
        timeRoutine = null;
    }
}
