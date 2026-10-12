using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameControllerScript : MonoBehaviour
{
    public enum GameState { Intro, Scanning, LevelIntro, Playing, LevelComplete, Won, Lost }

    const string HighScoreKey = "HighScore";
    const float ComboWindow = 2.5f;
    const int MaxCombo = 5;

    public static GameControllerScript Instance { get; private set; }

    // carried from one chapter scene to the next
    static int carriedScore;
    static int carriedAmmo;

    [Header("Prefabs")]
    public GameObject bulletPrefab;
    public GameObject zombiePrefab;
    // alternative rigged models for the runner and brute/boss zombies
    public GameObject runnerModel;
    public GameObject bruteModel;
    public GameObject ammoPickupPrefab;
    public GameObject healthPickupPrefab;

    [Header("Scene References")]
    public GameObject gun;
    public GameObject greetingPrompt;
    public GameObject scanPrompt;
    public GameObject youLostPrompt;
    public GameObject youWonPrompt;
    public GameObject fadeImage;
    public GameObject shootButton;
    public TMP_Text ammoCountText;
    public TMP_Text playerHealthText;
    // optional, created by cloning the health text when left empty
    public TMP_Text infoText;
    public AudioSource musicSource;

    [Header("Campaign")]
    // range of LevelCatalog levels played in this scene
    public int firstLevel = 0;
    public int lastLevel = 3;
    public int startingAmmo = 20;
    public float maxPlayerHealth = 100f;
    public float healBetweenLevels = 25f;
    public Vector2 spawnDistanceRange = new Vector2(4f, 5f);

    [Header("Shooting")]
    public float bulletSpeed = 40f;
    public float fireCooldown = 0.2f;
    public float rapidFireCooldown = 0.08f;
    public float shotRadius = 0.08f;
    public float maxShotDistance = 30f;
    public float bulletDamage = 50f;
    public float headshotMultiplier = 2f;
    public float gunRecoilAngle = 8f;

    [Header("Pickups")]
    public float pickupSpawnInterval = 20f;
    public float pickupLifetime = 20f;
    public int lowAmmoThreshold = 5;
    public float powerUpDropChance = 0.08f;
    public float bruteDropChance = 0.35f;

    [Header("Feedback")]
    public float fadeDuration = 1f;

    public GameState State { get; private set; } = GameState.Intro;
    public int AmmoCount { get; private set; }
    public float PlayerHealth { get; private set; }
    public int ZombiesKilled { get; private set; }
    public int Score { get; private set; }
    public bool IsPlaying => State == GameState.Playing;
    // zombies move and attack slower while the cryo pulse is active
    public float ZombieTimeScale => Time.time < cryoUntil ? 0.3f : 1f;

    Camera mainCamera;
    AudioSource gunAudioSource;
    RawImage fadeRawImage;
    Transform gunTransform;
    Quaternion gunRestRotation;
    GameHUD hud;

    // level progress
    int currentLevelIndex;
    LevelDefinition level;
    readonly List<ZombieController> aliveZombies = new List<ZombieController>();
    readonly List<GameObject> spawnedPickups = new List<GameObject>();
    int zombiesSpawned;
    float surviveTimeLeft;
    ZombieController boss;
    bool bossSpawned;
    float bossSpawnTime;
    float nextSpawnTime;
    float nextPickupTime;
    float nextHordeTime;
    bool hordeActive;
    int levelStartScore;
    int levelStartAmmo;
    GameObject activeAmmoPickup;

    // combat
    float nextFireTime;
    int combo;
    float lastKillTime;
    float rapidFireUntil;
    float hollowPointsUntil;
    float cryoUntil;

    // feedback
    float infoMessageUntil;
    float nextHapticTime;
    float nextHeartbeatTime;
    float baseMusicVolume = 0.7f;
    Coroutine hordeRoutine;
    static readonly List<ZombieController> noZombies = new List<ZombieController>();
    int shownScore = -1;
    int shownCombo = -1;

    void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        if (gun != null)
        {
            gunAudioSource = gun.GetComponent<AudioSource>();
            gunTransform = gun.transform;
            gunRestRotation = gunTransform.localRotation;
        }
        if (fadeImage != null)
        {
            fadeRawImage = fadeImage.GetComponent<RawImage>();
        }
        if (musicSource != null)
        {
            baseMusicVolume = musicSource.volume;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        bool continuingCampaign = firstLevel > 0;
        Score = continuingCampaign ? carriedScore : 0;
        AmmoCount = continuingCampaign ? Mathf.Max(carriedAmmo, startingAmmo) : startingAmmo;
        PlayerHealth = maxPlayerHealth;
        ZombiesKilled = 0;
        level = LevelCatalog.Get(firstLevel);

        SetActiveSafe(shootButton, false);
        SetActiveSafe(greetingPrompt, true);
        SetActiveSafe(scanPrompt, false);
        SetActiveSafe(youLostPrompt, false);
        SetActiveSafe(youWonPrompt, false);
        SetActiveSafe(gun, false);

        if (infoText == null) CreateInfoText();
        if (playerHealthText != null && playerHealthText.canvas != null)
        {
            hud = GameHUD.Create(playerHealthText.canvas.rootCanvas, playerHealthText);
            hud.SetCrosshairVisible(false);
        }
        RefreshHud();

        StartCoroutine(Fade(1f, 0f));
    }

    void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        aliveZombies.RemoveAll(z => z == null);

        if (State == GameState.Playing)
        {
            UpdateSpawning();
            UpdateHorde();
            UpdatePickups();
            UpdateObjective();
        }

        if (combo > 0 && Time.time - lastKillTime > ComboWindow) combo = 0;

        UpdateGunRecoil();
        UpdateTension();
        RefreshHud();
    }

    // ---------- Level flow ----------

    IEnumerator BeginLevel(int index, bool isRetry)
    {
        currentLevelIndex = index;
        level = LevelCatalog.Get(index);
        State = GameState.LevelIntro;

        if (!isRetry && level.bonusAmmo > 0 && index != firstLevel)
        {
            AmmoCount += level.bonusAmmo;
        }
        levelStartScore = Score;
        levelStartAmmo = AmmoCount;

        zombiesSpawned = 0;
        ZombiesKilled = 0;
        boss = null;
        bossSpawned = false;
        if (hordeRoutine != null) StopCoroutine(hordeRoutine);
        hordeActive = false;
        surviveTimeLeft = level.surviveSeconds;
        rapidFireUntil = hollowPointsUntil = cryoUntil = 0f;

        if (hud != null)
        {
            hud.SetCrosshairVisible(true);
            hud.HideBoss();
            hud.SetObjective(string.Empty);
            hud.ShowBanner("LEVEL " + (index + 1) + ": " + level.title.ToUpper(),
                "<color=#9A9A9A>" + level.chapter + "</color>\n" + level.story +
                "\n\n<color=#FFC832>" + ObjectiveDescription() + "</color>", 3.5f);
        }
        GameAudio.Play(GameAudio.Impact, 0.9f);

        yield return new WaitForSeconds(4.2f);

        State = GameState.Playing;
        nextSpawnTime = Time.time + 0.5f;
        nextPickupTime = Time.time + pickupSpawnInterval;
        nextHordeTime = Time.time + Random.Range(18f, 26f);
        bossSpawnTime = Time.time + 1f;
        ShowMessage("GO!", 1f);
    }

    string ObjectiveDescription()
    {
        switch (level.objective)
        {
            case LevelObjective.Survive:
                return "Survive for " + Mathf.RoundToInt(level.surviveSeconds) + " seconds";
            case LevelObjective.Boss:
                return "Defeat " + (LevelCatalog.IsFinalBoss(currentLevelIndex) ? "the Abomination" : "the Brute King");
            default:
                return "Eliminate " + level.zombieCount + " zombies";
        }
    }

    void UpdateObjective()
    {
        string objective;
        switch (level.objective)
        {
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
                if (bossSpawned)
                {
                    if (boss == null || boss.IsDead)
                    {
                        CompleteLevel();
                        return;
                    }
                    if (hud != null) hud.SetBoss(boss.DisplayName + (boss.IsEnraged ? "  [ENRAGED]" : ""), boss.zombieHealth / boss.MaxHealth);
                }
                break;

            default:
                objective = "ZOMBIES LEFT: " + Mathf.Max(level.zombieCount - ZombiesKilled, 0);
                if (ZombiesKilled >= level.zombieCount)
                {
                    CompleteLevel();
                    return;
                }
                break;
        }

        if (hud != null) hud.SetObjective(objective + PowerUpStatus());
    }

    string PowerUpStatus()
    {
        string status = string.Empty;
        status += TimerLabel(PowerUpType.RapidFire, rapidFireUntil);
        status += TimerLabel(PowerUpType.HollowPoints, hollowPointsUntil);
        status += TimerLabel(PowerUpType.CryoPulse, cryoUntil);
        return status;
    }

    static string TimerLabel(PowerUpType type, float until)
    {
        float left = until - Time.time;
        if (left <= 0f) return string.Empty;
        string hex = ColorUtility.ToHtmlStringRGB(PowerUpPickup.TypeColor(type));
        return "\n<color=#" + hex + ">" + PowerUpPickup.DisplayName(type) + " " + Mathf.CeilToInt(left) + "s</color>";
    }

    void CompleteLevel()
    {
        State = GameState.LevelComplete;
        StartCoroutine(LevelCompleteSequence());
    }

    IEnumerator LevelCompleteSequence()
    {
        // whatever is still standing collapses with the level
        foreach (ZombieController zombie in aliveZombies.ToArray())
        {
            if (zombie != null) zombie.Collapse();
        }
        aliveZombies.Clear();

        int healthBonus = Mathf.RoundToInt(PlayerHealth) * 10;
        AddScore(healthBonus);
        GameAudio.Play(GameAudio.Fanfare, 0.8f);

        if (hud != null)
        {
            hud.HideBoss();
            hud.SetObjective(string.Empty);
            hud.ShowBanner("LEVEL CLEARED", "Health bonus  +" + healthBonus + "\nScore  " + Score.ToString("N0"), 3f);
        }

        yield return new WaitForSeconds(3.8f);

        int next = currentLevelIndex + 1;
        if (next <= lastLevel && next < LevelCatalog.Count)
        {
            PlayerHealth = Mathf.Min(PlayerHealth + healBetweenLevels, maxPlayerHealth);
            yield return BeginLevel(next, false);
        }
        else if (next < LevelCatalog.Count && SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings)
        {
            // next chapter lives in the next scene
            carriedScore = Score;
            carriedAmmo = AmmoCount;
            SaveHighScore();
            if (hud != null) hud.ShowBanner("CHAPTER COMPLETE", "The night is only getting darker...", 0f);
            yield return new WaitForSeconds(2.5f);
            yield return LoadSceneWithFade(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
        {
            yield return Victory();
        }
    }

    IEnumerator Victory()
    {
        State = GameState.Won;
        bool newHighScore = SaveHighScore();
        SetActiveSafe(shootButton, false);
        if (hud != null)
        {
            hud.SetCrosshairVisible(false);
            hud.ShowBanner("YOU SURVIVED THE OUTBREAK",
                "Final score  " + Score.ToString("N0") +
                (newHighScore ? "\n<color=#FFC832>NEW HIGH SCORE!</color>" : "\nBest  " + PlayerPrefs.GetInt(HighScoreKey).ToString("N0")), 0f);
        }
        GameAudio.Play(GameAudio.Fanfare, 1f, 0.8f);

        yield return new WaitForSeconds(3.5f);

        if (youWonPrompt != null)
        {
            yield return Fade(0f, 1f);
            SetActiveSafe(gun, false);
            if (hud != null) hud.HideBanner();
            youWonPrompt.SetActive(true);
            yield return Fade(1f, 0f);
        }
    }

    void Lose()
    {
        State = GameState.Lost;
        SaveHighScore();
        SetActiveSafe(shootButton, false);
        SetActiveSafe(gun, false);
        SetActiveSafe(youLostPrompt, true);
        if (hud != null)
        {
            hud.HideBoss();
            hud.HideBanner();
            hud.SetCrosshairVisible(false);
            hud.SetObjective("Fell on level " + (currentLevelIndex + 1) + "   Score " + Score.ToString("N0"));
        }
        foreach (ZombieController zombie in aliveZombies)
        {
            if (zombie != null) zombie.Celebrate();
        }
    }

    // retry the current level in place, so the player doesn't have to rescan the room
    void RetryLevel()
    {
        foreach (ZombieController zombie in aliveZombies)
        {
            if (zombie != null) Destroy(zombie.gameObject);
        }
        aliveZombies.Clear();
        foreach (GameObject pickup in spawnedPickups)
        {
            if (pickup != null) Destroy(pickup);
        }
        spawnedPickups.Clear();

        Score = levelStartScore;
        AmmoCount = Mathf.Max(levelStartAmmo, 15);
        PlayerHealth = maxPlayerHealth;
        combo = 0;

        SetActiveSafe(youLostPrompt, false);
        SetActiveSafe(gun, true);
        SetActiveSafe(shootButton, true);
        StartCoroutine(BeginLevel(currentLevelIndex, true));
    }

    bool SaveHighScore()
    {
        if (Score <= PlayerPrefs.GetInt(HighScoreKey, 0)) return false;
        PlayerPrefs.SetInt(HighScoreKey, Score);
        PlayerPrefs.Save();
        return true;
    }

    // ---------- Spawning ----------

    void UpdateSpawning()
    {
        if (level.objective == LevelObjective.Boss && !bossSpawned)
        {
            if (Time.time >= bossSpawnTime) SpawnBoss();
            return;
        }

        if (Time.time < nextSpawnTime) return;
        int maxAlive = level.maxAlive + (boss != null && !boss.IsDead ? 1 : 0);
        if (aliveZombies.Count >= maxAlive) return;
        if (level.objective != LevelObjective.Survive && zombiesSpawned >= level.zombieCount) return;

        SpawnZombie(RollZombieType(), RingPosition(Random.Range(0f, Mathf.PI * 2f)), true);
        nextSpawnTime = Time.time + Random.Range(level.spawnInterval.x, level.spawnInterval.y);
    }

    ZombieType RollZombieType()
    {
        float roll = Random.value;
        if (roll < level.bruteChance) return ZombieType.Brute;
        if (roll < level.bruteChance + level.runnerChance) return ZombieType.Runner;
        return ZombieType.Walker;
    }

    Vector3 RingPosition(float angle)
    {
        Vector3 cameraPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float distance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
        return cameraPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    ZombieController SpawnZombie(ZombieType type, Vector3 position, bool countsTowardLevel)
    {
        if (zombiePrefab == null || mainCamera == null) return null;

        Vector3 toCamera = mainCamera.transform.position - position;
        toCamera.y = 0f;
        GameObject zombieObject = Instantiate(zombiePrefab, position,
            toCamera.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCamera) : Quaternion.identity);

        ZombieController zombie = zombieObject.GetComponent<ZombieController>();
        if (zombie == null) return null;

        GameObject model = null;
        if (type == ZombieType.Runner) model = runnerModel;
        else if (type == ZombieType.Brute || type == ZombieType.Boss) model = bruteModel;
        zombie.Configure(type, level.speedMultiplier, model, LevelCatalog.IsFinalBoss(currentLevelIndex));

        aliveZombies.Add(zombie);
        if (countsTowardLevel) zombiesSpawned++;
        return zombie;
    }

    void SpawnBoss()
    {
        bossSpawned = true;

        // rise right in front of the player for maximum drama
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 position = mainCamera.transform.position + forward.normalized * 5f;

        boss = SpawnZombie(ZombieType.Boss, position, false);
        if (hud != null)
        {
            hud.Shake(14f);
            hud.ShowBanner(boss != null ? boss.DisplayName : "BOSS", "It's here.", 2f);
        }
        nextSpawnTime = Time.time + 4f;
    }

    // called by the boss to call in reinforcements
    public void SummonMinions(Vector3 center, int count)
    {
        if (State != GameState.Playing) return;

        ShowMessage("It's calling the horde!");
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(1.2f, 2f);
            SpawnZombie(Random.value < 0.5f ? ZombieType.Runner : ZombieType.Walker,
                center + new Vector3(offset.x, 0f, offset.y), false);
        }
    }

    public void OnBossEnraged(ZombieController enragedBoss)
    {
        if (hud != null)
        {
            hud.Shake(16f);
            hud.ShowBanner("IT'S ENRAGED!", "Keep firing!", 1.5f);
        }
    }

    void UpdateHorde()
    {
        if (!level.hordeEvents || hordeActive || Time.time < nextHordeTime) return;
        if (level.objective == LevelObjective.Eliminate && zombiesSpawned >= level.zombieCount) return;

        nextHordeTime = Time.time + Random.Range(25f, 35f);
        hordeRoutine = StartCoroutine(HordeRush());
    }

    // siren, then a pack of zombies charges from one direction
    IEnumerator HordeRush()
    {
        hordeActive = true;
        GameAudio.Play(GameAudio.Siren, 0.6f);
        if (hud != null)
        {
            hud.ShowBanner("HORDE INCOMING!", "Brace yourself", 1.5f);
            hud.Shake(6f);
        }

        yield return new WaitForSeconds(2.5f);

        float angle = Random.Range(0f, Mathf.PI * 2f);
        int count = 4 + currentLevelIndex / 3;
        if (level.objective == LevelObjective.Eliminate)
        {
            count = Mathf.Min(count, level.zombieCount - zombiesSpawned);
        }

        for (int i = 0; i < count && State == GameState.Playing; i++)
        {
            ZombieType type = Random.value < 0.5f ? ZombieType.Runner : ZombieType.Walker;
            SpawnZombie(type, RingPosition(angle + Random.Range(-0.4f, 0.4f)), true);
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(6f);
        hordeActive = false;
    }

    public void OnZombieKilled(ZombieController zombie)
    {
        aliveZombies.Remove(zombie);
        if (State != GameState.Playing) return;

        ZombiesKilled++;

        combo = Time.time - lastKillTime <= ComboWindow ? Mathf.Min(combo + 1, MaxCombo) : 1;
        lastKillTime = Time.time;
        AddScore(zombie.ScoreValue * combo);
        if (combo >= 3) ShowMessage(combo + "x COMBO!");

        float dropChance = zombie.Type == ZombieType.Brute ? bruteDropChance : powerUpDropChance;
        if (!zombie.IsBoss && Random.value < dropChance)
        {
            PowerUpType type = (PowerUpType)Random.Range(0, 4);
            PowerUpPickup powerUp = PowerUpPickup.Spawn(type, zombie.transform.position + Vector3.up * 0.9f);
            spawnedPickups.Add(powerUp.gameObject);
            ShowMessage(PowerUpPickup.DisplayName(type) + " dropped!");
        }
    }

    void AddScore(int amount)
    {
        Score += amount;
    }

    // ---------- Pickups ----------

    void UpdatePickups()
    {
        // never leave the player stuck without ammo
        bool outOfAmmo = AmmoCount <= 0 && activeAmmoPickup == null;
        if (!outOfAmmo && Time.time < nextPickupTime) return;
        nextPickupTime = Time.time + pickupSpawnInterval;

        bool needsAmmo = outOfAmmo || AmmoCount <= lowAmmoThreshold;
        bool needsHealth = PlayerHealth < maxPlayerHealth * 0.5f;
        GameObject prefab = needsAmmo ? ammoPickupPrefab
            : needsHealth ? healthPickupPrefab
            : (Random.value < 0.5f ? ammoPickupPrefab : healthPickupPrefab);
        if (prefab == null) return;

        GameObject pickup = SpawnPickup(prefab);
        if (prefab == ammoPickupPrefab) activeAmmoPickup = pickup;
    }

    GameObject SpawnPickup(GameObject prefab)
    {
        // place it in front of the player, roughly at floor level, so it's easy to find
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * forward.normalized;

        Vector3 position = mainCamera.transform.position + forward * Random.Range(1.5f, 2.5f);
        position.y = mainCamera.transform.position.y - ZombieController.CameraHeightAboveFloor;

        GameObject pickup = Instantiate(prefab, position, Quaternion.LookRotation(-forward));
        TimedObjectDestructor destructor = pickup.AddComponent<TimedObjectDestructor>();
        destructor.time = pickupLifetime;
        spawnedPickups.RemoveAll(p => p == null);
        spawnedPickups.Add(pickup);

        GameFX.Sparkle(position + Vector3.up * 0.2f, Color.white);
        ShowMessage(prefab == ammoPickupPrefab ? "Supply drop: ammo crate!" : "Supply drop: med kit!");
        return pickup;
    }

    public void AddAmmo(int amount)
    {
        AmmoCount += amount;
        GameAudio.Play(GameAudio.Pickup, 0.7f);
        ShowMessage("+" + amount + " ammo");
    }

    public void AddHealth(float amount)
    {
        PlayerHealth = Mathf.Min(PlayerHealth + amount, maxPlayerHealth);
        GameAudio.Play(GameAudio.Pickup, 0.7f, 1.2f);
        ShowMessage("+" + Mathf.RoundToInt(amount) + " health");
    }

    public void ActivatePowerUp(PowerUpType type)
    {
        GameAudio.Play(GameAudio.PowerUp, 0.8f);
        ShowMessage(PowerUpPickup.DisplayName(type) + "!", 2f);

        switch (type)
        {
            case PowerUpType.RapidFire:
                rapidFireUntil = Time.time + 8f;
                break;
            case PowerUpType.HollowPoints:
                hollowPointsUntil = Time.time + 10f;
                break;
            case PowerUpType.CryoPulse:
                cryoUntil = Time.time + 8f;
                GameFX.Shockwave(FloorUnderPlayer(), PowerUpPickup.TypeColor(type));
                break;
            case PowerUpType.Shockwave:
                Vector3 center = FloorUnderPlayer();
                GameFX.Shockwave(center, PowerUpPickup.TypeColor(type));
                GameAudio.Play(GameAudio.Impact, 1f, 1.3f);
                if (hud != null) hud.Shake(12f);
                foreach (ZombieController zombie in aliveZombies.ToArray())
                {
                    if (zombie == null) continue;
                    Vector3 offset = zombie.transform.position - center;
                    offset.y = 0f;
                    if (offset.magnitude <= 5f) zombie.TakeDamage(zombie.IsBoss ? 200f : 250f, offset.normalized);
                }
                break;
        }
    }

    Vector3 FloorUnderPlayer()
    {
        Vector3 position = mainCamera.transform.position;
        position.y -= ZombieController.CameraHeightAboveFloor;
        return position;
    }

    // ---------- Combat ----------

    public void ShootBullet()
    {
        if (State != GameState.Playing || mainCamera == null) return;
        if (Time.time < nextFireTime) return;

        bool rapidFire = Time.time < rapidFireUntil;
        nextFireTime = Time.time + (rapidFire ? rapidFireCooldown : fireCooldown);

        Transform cameraTransform = mainCamera.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (AmmoCount <= 0 && !rapidFire)
        {
            // dry fire can still grab a pickup, otherwise the player could soft-lock with no ammo
            if (CastShot(ray, out RaycastHit pickupHit))
            {
                TryCollectPickup(pickupHit.collider);
            }
            GameAudio.Play(GameAudio.DryFire, 0.8f);
            ShowMessage("Out of ammo! Find an ammo crate");
            return;
        }

        // rapid fire rounds are free
        if (!rapidFire) AmmoCount--;

        if (gunAudioSource != null && gunAudioSource.clip != null)
        {
            gunAudioSource.pitch = Random.Range(0.95f, 1.05f) * (rapidFire ? 1.15f : 1f);
            gunAudioSource.PlayOneShot(gunAudioSource.clip);
        }
        if (gunTransform != null)
        {
            gunTransform.localRotation = gunRestRotation * Quaternion.Euler(-gunRecoilAngle, 0f, 0f);
        }

        Vector3 muzzle = gunTransform != null
            ? gunTransform.position + cameraTransform.forward * 0.25f
            : cameraTransform.position + cameraTransform.forward * 0.4f;
        GameFX.MuzzleFlash(muzzle, cameraTransform.forward);

        // hitscan so fast bullets can't tunnel through zombies; the bullet prefab is only a tracer
        Vector3 endPoint = ray.GetPoint(maxShotDistance);
        if (CastShot(ray, out RaycastHit hit))
        {
            endPoint = hit.point;
            ZombieController zombie = hit.collider.GetComponentInParent<ZombieController>();
            if (zombie != null)
            {
                bool headshot = zombie.IsHeadshot(hit.point);
                float damage = bulletDamage * (headshot ? headshotMultiplier : 1f) * (Time.time < hollowPointsUntil ? 2f : 1f);
                bool killed = zombie.TakeDamage(damage, ray.direction);

                GameFX.Blood(hit.point, -ray.direction + Vector3.up * 0.3f, headshot);
                GameAudio.Play(killed ? GameAudio.KillMarker : GameAudio.HitMarker, 0.5f);
                if (hud != null) hud.HitMarker(killed, headshot);

                if (headshot)
                {
                    if (killed) AddScore(50 * Mathf.Max(combo, 1));
                    ShowMessage(killed ? "HEADSHOT!" : "Headshot!");
                }
            }
            else
            {
                TryCollectPickup(hit.collider);
            }
        }

        SpawnTracer(cameraTransform, endPoint);
    }

    // nearest hit along the shot, ignoring the gun model and tracers
    bool CastShot(Ray ray, out RaycastHit closest)
    {
        closest = default;
        float closestDistance = float.MaxValue;
        RaycastHit[] hits = Physics.SphereCastAll(ray, shotRadius, maxShotDistance, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closestDistance) continue;
            if (gunTransform != null && hit.collider.transform.IsChildOf(gunTransform)) continue;
            if (hit.collider.CompareTag("Bullet")) continue;
            closest = hit;
            closestDistance = hit.distance;
        }
        return closestDistance < float.MaxValue;
    }

    bool TryCollectPickup(Collider other)
    {
        PowerUpPickup powerUp = other.GetComponentInParent<PowerUpPickup>();
        if (powerUp != null)
        {
            powerUp.Collect();
            return true;
        }
        IncreaseAmmoScript ammo = other.GetComponentInParent<IncreaseAmmoScript>();
        if (ammo != null)
        {
            ammo.Collect();
            return true;
        }
        IncreaseHealthScript health = other.GetComponentInParent<IncreaseHealthScript>();
        if (health != null)
        {
            health.Collect();
            return true;
        }
        return false;
    }

    void SpawnTracer(Transform cameraTransform, Vector3 endPoint)
    {
        if (bulletPrefab == null) return;

        // start slightly below the view so the tracer doesn't fill the screen
        Vector3 start = cameraTransform.position + cameraTransform.forward * 0.2f - cameraTransform.up * 0.05f;
        Vector3 direction = endPoint - start;
        if (direction.sqrMagnitude < 0.0001f) return;
        GameObject bullet = Instantiate(bulletPrefab, start, Quaternion.LookRotation(direction));

        // damage is applied by the raycast, so the tracer must not trigger anything
        foreach (Collider c in bullet.GetComponentsInChildren<Collider>()) c.enabled = false;

        Rigidbody body = bullet.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.velocity = direction.normalized * bulletSpeed;
        }
        Destroy(bullet, Mathf.Min(direction.magnitude / bulletSpeed, 3f));
    }

    public void DamagePlayer(float amount)
    {
        if (State != GameState.Playing) return;

        PlayerHealth = Mathf.Max(PlayerHealth - amount, 0f);
        if (hud != null) hud.FlashDamage(0.45f);

#if UNITY_ANDROID || UNITY_IOS
        if (Time.time >= nextHapticTime)
        {
            Handheld.Vibrate();
            nextHapticTime = Time.time + 1f;
        }
#endif

        if (PlayerHealth <= 0f) Lose();
    }

    // heartbeat, red vignette and music that react to danger
    void UpdateTension()
    {
        float lowHealth = State == GameState.Playing ? 1f - PlayerHealth / (maxPlayerHealth * 0.35f) : 0f;
        if (hud != null)
        {
            hud.SetLowHealth(lowHealth);
            if (State == GameState.Playing) hud.UpdateThreats(aliveZombies, mainCamera);
            else hud.UpdateThreats(noZombies, mainCamera);
        }

        if (lowHealth > 0f && Time.time >= nextHeartbeatTime)
        {
            GameAudio.Play(GameAudio.Heartbeat, 0.9f);
            nextHeartbeatTime = Time.time + Mathf.Lerp(1.1f, 0.5f, lowHealth);
        }

        if (musicSource != null)
        {
            bool bossAlive = boss != null && !boss.IsDead;
            float targetPitch = State == GameState.Lost ? 0.6f
                : bossAlive ? 1.12f
                : hordeActive ? 1.06f
                : Time.time < cryoUntil ? 0.85f
                : 1f;
            float targetVolume = State == GameState.Lost ? baseMusicVolume * 0.5f
                : bossAlive || hordeActive ? Mathf.Min(baseMusicVolume * 1.3f, 1f)
                : baseMusicVolume;
            musicSource.pitch = Mathf.MoveTowards(musicSource.pitch, targetPitch, Time.deltaTime * 0.3f);
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVolume, Time.deltaTime * 0.5f);
        }
    }

    // ---------- UI ----------

    public void OnCOntinueButtonClicked()
    {
        State = GameState.Scanning;
        SetActiveSafe(greetingPrompt, false);
        SetActiveSafe(scanPrompt, true);
    }

    public void OnContinueButtonClicked() => OnCOntinueButtonClicked();

    public void OnStartButtonClicked()
    {
        if (State != GameState.Intro && State != GameState.Scanning) return;

        SetActiveSafe(gun, true);
        SetActiveSafe(scanPrompt, false);
        SetActiveSafe(shootButton, true);
        StartCoroutine(BeginLevel(firstLevel, false));
    }

    public void OnRestartButtonClicked()
    {
        if (State == GameState.Lost)
        {
            RetryLevel();
            return;
        }

        // after beating the game, start the campaign over
        carriedScore = 0;
        carriedAmmo = 0;
        SetActiveSafe(youWonPrompt, false);
        StartCoroutine(LoadSceneWithFade(0));
    }

    void RefreshHud()
    {
        if (ammoCountText != null)
        {
            bool rapidFire = Time.time < rapidFireUntil;
            ammoCountText.text = rapidFire ? "Ammo: RAPID" : "Ammo: " + AmmoCount;
            ammoCountText.color = rapidFire ? new Color(1f, 0.85f, 0.1f)
                : AmmoCount <= lowAmmoThreshold ? new Color(1f, 0.6f, 0.1f)
                : Color.white;
        }

        if (playerHealthText != null)
        {
            playerHealthText.text = "Health: " + Mathf.CeilToInt(PlayerHealth);
            playerHealthText.color = Color.Lerp(Color.red, Color.white, PlayerHealth / (maxPlayerHealth * 0.5f));
        }

        if (infoText != null && Time.time >= infoMessageUntil)
        {
            infoText.text = string.Empty;
        }

        if (hud != null && (Score != shownScore || combo != shownCombo))
        {
            shownScore = Score;
            shownCombo = combo;
            hud.SetScore(Score, combo);
        }
    }

    void ShowMessage(string message, float duration = 1.5f)
    {
        if (infoText == null) return;
        infoText.text = message;
        infoMessageUntil = Time.time + duration;
    }

    void CreateInfoText()
    {
        if (playerHealthText == null) return;

        // clone the health label so the new line matches the existing HUD style
        infoText = Instantiate(playerHealthText, playerHealthText.transform.parent);
        infoText.name = "InfoText";
        RectTransform rect = infoText.rectTransform;
        rect.anchoredPosition -= new Vector2(0f, rect.rect.height + 10f);
        infoText.text = string.Empty;
    }

    void UpdateGunRecoil()
    {
        if (gunTransform == null) return;
        gunTransform.localRotation = Quaternion.Slerp(gunTransform.localRotation, gunRestRotation, Time.deltaTime * 15f);
    }

    // ---------- Scene flow ----------

    IEnumerator LoadSceneWithFade(int buildIndex)
    {
        yield return Fade(0f, 1f);
        SceneManager.LoadScene(buildIndex);
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeRawImage == null) yield break;

        fadeImage.SetActive(true);
        // block taps while the screen is fading
        fadeRawImage.raycastTarget = true;

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            fadeRawImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        fadeRawImage.color = new Color(0f, 0f, 0f, to);

        if (to <= 0f) fadeImage.SetActive(false);
    }

    static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}
