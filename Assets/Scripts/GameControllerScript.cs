using UnityEngine;
using TMPro;

// The game's entry point in each level scene. It holds the inspector settings and scene references,
// adds the parts that run the game (Assets/Scripts/Game), ticks them in a fixed order each frame,
// and keeps the methods that scene buttons and other scripts call, forwarding them to the right part.
public class GameControllerScript : MonoBehaviour
{
    public enum GameState { Intro, Scanning, LevelIntro, Playing, LevelComplete, Won, Lost }

    public static GameControllerScript Instance { get; private set; }

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
    public float maxPlayerHealth = 100f;
    public float healBetweenLevels = 25f;
    public Vector2 spawnDistanceRange = new Vector2(4f, 5f);
    // the first levels only spawn within this many degrees of where the player faces
    public int frontSpawnLevels = 2;
    public float frontSpawnHalfAngle = 60f;

    // gun stats live in Arsenal; these only shape the shot itself
    [Header("Shooting")]
    public float bulletSpeed = 40f;
    public float shotRadius = 0.08f;

    [Header("Grenades")]
    // topped up to this many at the start of each level
    public int grenadesPerLevel = 2;
    public int maxGrenades = 5;
    public float grenadeRadius = 2.6f;
    // full damage at the centre, falling to 35% at the edge of the blast
    public float grenadeDamage = 320f;
    public float grenadeFlightTime = 0.75f;
    public Vector2 grenadeThrowRange = new Vector2(1.5f, 9f);

    [Header("Catching")]
    // chance a catchable zombie spawns as a glowing mutant (map sites raise it)
    public float mutantChance = 0.04f;

    [Header("Pickups")]
    public float pickupSpawnInterval = 20f;
    public float pickupLifetime = 20f;
    public int lowAmmoThreshold = 5;
    // below this many pistol rounds, a reserve round trickles in so the player is never defenceless
    public int reserveAmmoCap = 3;
    public float reserveAmmoInterval = 2f;
    public float powerUpDropChance = 0.08f;
    public float bruteDropChance = 0.35f;

    [Header("Feedback")]
    public float fadeDuration = 1f;

    public Camera MainCamera { get; private set; }

    public SceneFlow Scenes { get; private set; }
    public ProgressionManager Progression { get; private set; }
    public LevelFlowManager Flow { get; private set; }
    public SpawnManager Spawner { get; private set; }
    public PickupManager Pickups { get; private set; }
    public PlayerHealth Player { get; private set; }
    public WeaponSystem Weapons { get; private set; }
    public CaptureSystem Capture { get; private set; }
    public UIController UI { get; private set; }
    public TutorialCoach Coach { get; private set; }
    public HuntSystem Hunt { get; private set; }

    public GameState State => Flow.State;
    public bool IsPlaying => Flow.IsPlaying;
    public int AmmoCount => Weapons.AmmoCount;
    public int ShellCount => Weapons.ShellCount;
    public int GrenadeCount => Weapons.GrenadeCount;
    public WeaponSlot CurrentSlot => Weapons.CurrentSlot;
    public int ZombiesKilled => Flow.ZombiesKilled;
    public int Score => Flow.Score;
    // zombies move and attack slower while the cryo pulse is active
    public float ZombieTimeScale => Pickups.ZombieTimeScale;

    void Awake()
    {
        Instance = this;
        MainCamera = Camera.main;

        Scenes = gameObject.AddComponent<SceneFlow>();
        Progression = gameObject.AddComponent<ProgressionManager>();
        Flow = gameObject.AddComponent<LevelFlowManager>();
        Spawner = gameObject.AddComponent<SpawnManager>();
        Pickups = gameObject.AddComponent<PickupManager>();
        Player = gameObject.AddComponent<PlayerHealth>();
        Weapons = gameObject.AddComponent<WeaponSystem>();
        Capture = gameObject.AddComponent<CaptureSystem>();
        UI = gameObject.AddComponent<UIController>();
        Coach = gameObject.AddComponent<TutorialCoach>();
        Hunt = gameObject.AddComponent<HuntSystem>();

        // bound in this order, which is also the order their event handlers run in
        // (a kill updates the score and combo before any power-up drops)
        foreach (GameSystem system in new GameSystem[] { Scenes, Progression, Flow, Spawner, Pickups, Player, Weapons, Capture, UI, Coach, Hunt })
        {
            system.Bind(this);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        DisableTemplateSpawner();

        // survivor level rewards
        Player.Setup();
        int streakBonus = Progression.BeginSession();
        GameSettings.Apply();
        Time.timeScale = 1f;
        Weapons.LoadEquipped();
        UI.StartAmbience();

        bool skipGreeting = Flow.Setup();

        UI.CreateHud(skipGreeting);
        // realistic look: room lighting, floor shadows and the horror grade
        // real floor height and depth occlusion first, so everything placed on the floor uses it
        ARWorld.Attach(MainCamera);
        ARLighting.Attach(MainCamera, ZombieController.CameraHeightAboveFloor);
        PostFX.Create(MainCamera);
        UI.FinishSetup(skipGreeting);

        Progression.RefreshXP();
        UI.RefreshHud();
        Progression.ShowStreakBonus(streakBonus);

        StartCoroutine(Scenes.Fade(1f, 0f));
    }

    // the scenes came from Unity's AR template, whose tap-to-place spawner drops its demo objects
    // (a big first-aid cube) wherever a tap lands on a detected surface; the game never uses it
    static void DisableTemplateSpawner()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string type = behaviour.GetType().Name;
            if (type == "ARInteractorSpawnTrigger" || type == "ObjectSpawner") behaviour.enabled = false;
        }
    }

    void Update()
    {
        if (MainCamera == null) MainCamera = Camera.main;
        Spawner.PruneDead();

        if (Flow.IsPlaying)
        {
            LifetimeStats.AddPlayTime(Time.unscaledDeltaTime);
            Spawner.Tick();
            Pickups.Tick();
            Weapons.TickReserveAmmo();
            Hunt.Tick();
            Flow.TickObjective();
        }
        Weapons.UpdateAim();
        Capture.UpdateHint();

        Flow.TickCombo();
        Weapons.TickAutoFire();
        UI.Tick();

        Weapons.UpdateGunSway();
        UI.UpdateTension();
        UI.RefreshHud();
    }

    void LateUpdate()
    {
        UI.LateTick();
    }

    // ---------- Called by scene buttons (bound by name; don't rename) ----------

    public void ShootBullet() => Weapons.Shoot();

    public void OnCOntinueButtonClicked() => Flow.EnterScanning();

    public void OnContinueButtonClicked() => Flow.EnterScanning();

    public void OnStartButtonClicked() => Flow.OnStartPressed();

    public void OnRestartButtonClicked()
    {
        if (Flow.State == GameState.Lost)
        {
            Flow.RetryLevel();
            return;
        }
        Scenes.RestartCampaign();
    }

    // ---------- Called by zombies, pickups and acid ----------

    public void OnZombieKilled(ZombieController zombie) => Spawner.OnZombieKilled(zombie);

    public void OnBossEnraged(ZombieController boss) => Spawner.OnBossEnraged(boss);

    public void SummonMinions(Vector3 center, int count) => Spawner.SummonMinions(center, count);

    public void DamagePlayer(float amount) => Player.Damage(amount);

    public void AcidHit(float damage) => Player.AcidHit(damage);

    public void AcidDodged() => Player.AcidDodged();

    public void AddAmmo(int amount) => Weapons.AddAmmo(amount);

    public void AddHealth(float amount) => Player.AddHealth(amount);

    public void ActivatePowerUp(PowerUpType type) => Pickups.ActivatePowerUp(type);

    // ---------- Other entry points ----------

    public void SwitchWeapon() => Weapons.SwitchWeapon();

    public void ThrowGrenade() => Weapons.ThrowGrenade();

    public void ThrowTrap() => Capture.ThrowTrap();

    public void OpenMissionBoard() => Progression.OpenMissionBoard();

    public void StartEncounter(SiteEncounter siteEncounter) => Scenes.StartEncounter(siteEncounter);
}
