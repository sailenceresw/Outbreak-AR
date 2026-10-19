using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;
using GameState = GameControllerScript.GameState;

// Everything the player sees and hears around the fight: the HUD, the runtime buttons, the home,
// pause and menu screens, callouts, the floor grid, and the tension (heartbeat, danger grade, music).
public class UIController : GameSystem
{
    public new GameHUD Hud { get; private set; }

    HomeScreen homeScreen;
    GameObject swapButton;
    GameObject grenadeButton;
    TMP_Text grenadeButtonLabel;
    GameObject trapButton;
    TMP_Text trapButtonLabel;
    GameObject missionsButton;
    TMP_Text missionsButtonLabel;
    // pause during fights, home after a run
    GameObject pauseButton;
    GameObject homeButton;

    // detected floor planes are drawn while scanning and hidden during fights
    ARPlaneManager planeManager;
    bool planesVisible = true;
    readonly List<Renderer> planeRenderers = new List<Renderer>();

    // feedback
    float infoMessageUntil;
    float nextHeartbeatTime;
    float baseMusicVolume = 0.7f;
    static readonly List<ZombieController> noZombies = new List<ZombieController>();
    int shownScore = -1;
    int shownCombo = -1;

    protected override void OnBind()
    {
        if (Game.musicSource != null)
        {
            baseMusicVolume = Game.musicSource.volume;
        }
        planeManager = FindObjectOfType<ARPlaneManager>();

        Progression.XPChanged += () =>
        {
            if (Hud != null) Hud.SetXP(PlayerProfile.Level, PlayerProfile.LevelProgress);
        };
        Flow.StateChanged += OnStateChanged;
    }

    // the fight's buttons go away when a run ends
    void OnStateChanged(GameState state)
    {
        if (state != GameState.Won && state != GameState.Lost) return;
        SetActiveSafe(Game.shootButton, false);
        SetActiveSafe(swapButton, false);
        SetActiveSafe(grenadeButton, false);
        SetActiveSafe(trapButton, false);
        if (state == GameState.Lost) SetActiveSafe(Game.gun, false);
    }

    // ---------- Setup ----------

    // the runtime buttons, the scene's prompts and the HUD; called once when the scene starts
    public void CreateHud(bool skipGreeting)
    {
        CreateSwapButton();
        CreateGrenadeButton();
        CreateTrapButton();
        CreateMissionsButton();
        SetActiveSafe(Game.shootButton, false);
        // the home screen replaces the template's greeting panel
        SetActiveSafe(Game.greetingPrompt, false);
        SetActiveSafe(Game.scanPrompt, skipGreeting);
        Weapons.AddTriggerHold(Game.shootButton);
        SetActiveSafe(Game.youLostPrompt, false);
        SetActiveSafe(Game.youWonPrompt, false);
        SetActiveSafe(Game.gun, false);

        if (Game.infoText == null) CreateInfoText();
        TMP_Text playerHealthText = Game.playerHealthText;
        if (playerHealthText != null && playerHealthText.canvas != null)
        {
            Hud = GameHUD.Create(playerHealthText.canvas.rootCanvas, playerHealthText);
            Hud.SetCrosshairVisible(false);

            // the HUD's health and ammo panels replace the scene's plain labels; the labels stay
            // in place because the HUD and level select clone them for their font
            playerHealthText.text = string.Empty;
            if (Game.ammoCountText != null) Game.ammoCountText.text = string.Empty;
        }
    }

    // styling and the screens on top of the HUD; called once after the AR look is attached
    public void FinishSetup(bool skipGreeting)
    {
        UnifyFonts();
        StyleSceneButtons();
        CreatePauseButton();
        CreateHomeButton();
        if (!skipGreeting) ShowHomeScreen();
    }

    // a constant low drone and wind under the music; horror lives in the quiet moments
    public void StartAmbience()
    {
        AudioSource ambience = gameObject.AddComponent<AudioSource>();
        ambience.clip = GameAudio.Ambience;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.35f;
        ambience.Play();
    }

    public void ShowCombatButtons(bool heavyUnlocked)
    {
        SetActiveSafe(swapButton, heavyUnlocked);
        SetActiveSafe(grenadeButton, true);
        SetActiveSafe(trapButton, true);
    }

    // ---------- Per frame ----------

    // which screen's buttons are showing, and the back button during fights
    public void Tick()
    {
        GameState state = Flow.State;
        bool missionBoardOpen = Progression.MissionBoardOpen;

        // the HUD belongs to fights; the home and scan screens stay clean
        if (Hud != null) Hud.SetVisible(state != GameState.Intro && state != GameState.Scanning);

        // the home screen has its own missions button; this one is for the end-of-run screens
        bool runOver = state == GameState.Lost || state == GameState.Won;
        SetActiveSafe(missionsButton, runOver && !missionBoardOpen);

        // pause during fights, home once the run is over
        bool inFight = state == GameState.Playing || state == GameState.LevelIntro;
        SetActiveSafe(pauseButton, inFight && !PauseMenu.IsOpen);
        SetActiveSafe(homeButton, (state == GameState.Lost || state == GameState.Won || state == GameState.Scanning) && !missionBoardOpen);
        if (inFight && !PauseMenu.IsOpen && !missionBoardOpen && PauseMenu.ClosedFrame != Time.frameCount
            && PauseMenu.BackPressed()) OpenPauseMenu();
    }

    public void LateTick()
    {
        // new planes keep being detected mid-fight, so re-hide every frame while hidden
        if (!planesVisible) ApplyPlaneVisibility();
    }

    // heartbeat, red vignette and music that react to danger
    public void UpdateTension()
    {
        GameState state = Flow.State;
        bool playing = state == GameState.Playing;
        float health = Player.Current;
        float maxHealth = Player.Max;
        float lowHealth = playing ? 1f - health / (maxHealth * 0.35f) : 0f;
        if (PostFX.Instance != null) PostFX.Instance.SetDanger(Mathf.Clamp01(1f - health / (maxHealth * 0.5f)) * (playing ? 1f : 0f));
        if (Hud != null)
        {
            Hud.SetLowHealth(lowHealth);
            if (playing) Hud.UpdateThreats(Spawner.Alive, MainCamera);
            else Hud.UpdateThreats(noZombies, MainCamera);
        }

        if (lowHealth > 0f && Time.time >= nextHeartbeatTime)
        {
            GameAudio.Play(GameAudio.Heartbeat, 0.9f);
            nextHeartbeatTime = Time.time + Mathf.Lerp(1.1f, 0.5f, lowHealth);
        }

        AudioSource musicSource = Game.musicSource;
        if (musicSource != null)
        {
            bool bossAlive = Spawner.BossAlive;
            bool hordeActive = Spawner.HordeActive;
            float targetPitch = state == GameState.Lost ? 0.6f
                : bossAlive ? 1.12f
                : hordeActive ? 1.06f
                : Pickups.CryoActive ? 0.85f
                : 1f;
            float targetVolume = state == GameState.Lost ? baseMusicVolume * 0.5f
                : bossAlive || hordeActive ? Mathf.Min(baseMusicVolume * 1.3f, 1f)
                : baseMusicVolume;
            musicSource.pitch = Mathf.MoveTowards(musicSource.pitch, targetPitch, Time.deltaTime * 0.3f);
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVolume, Time.deltaTime * 0.5f);
        }
    }

    public void RefreshHud()
    {
        WeaponDefinition weapon = Weapons.ActiveWeapon;
        bool rapidFire = Pickups.RapidFireActive;
        bool heavyShot = Weapons.HoldingHeavy;
        int rounds = heavyShot ? Weapons.ShellCount : Weapons.AmmoCount;
        // "low" scales with how much the gun usually carries
        int lowThreshold = heavyShot ? Mathf.Max(2, Weapons.Heavy.startAmmo / 4) : Game.lowAmmoThreshold;
        Color roundsColor = rapidFire ? new Color(1f, 0.85f, 0.1f)
            : rounds <= 0 ? new Color(1f, 0.25f, 0.2f)
            : rounds <= lowThreshold ? new Color(1f, 0.6f, 0.1f)
            : Color.white;
        string roundsLabel = rapidFire ? "RAPID" : rounds.ToString();

        float health = Player.Current;
        float maxHealth = Player.Max;
        if (Hud != null)
        {
            Hud.SetHealth(health, maxHealth);
            Hud.SetAmmo(weapon.name.ToUpper() + "  " + weapon.ammoName, roundsLabel, roundsColor, Weapons.GrenadeCount);
        }
        else
        {
            // no HUD: fall back to the scene's plain labels
            if (Game.ammoCountText != null)
            {
                Game.ammoCountText.text = weapon.ammoName + ": " + roundsLabel;
                Game.ammoCountText.color = roundsColor;
            }
            if (Game.playerHealthText != null)
            {
                Game.playerHealthText.text = "Health: " + Mathf.CeilToInt(health);
                Game.playerHealthText.color = Color.Lerp(Color.red, Color.white, health / (maxHealth * 0.5f));
            }
        }

        if (missionsButtonLabel != null && missionsButton != null && missionsButton.activeSelf)
        {
            int ready = MissionSystem.UnclaimedCount;
            missionsButtonLabel.text = ready > 0 ? "MISSIONS  (" + ready + ")" : "MISSIONS";
        }

        if (grenadeButtonLabel != null)
        {
            int grenades = Weapons.GrenadeCount;
            grenadeButtonLabel.text = "GRENADE  " + grenades;
            grenadeButtonLabel.alpha = grenades > 0 ? 1f : 0.45f;
        }
        if (trapButtonLabel != null && trapButton != null && trapButton.activeSelf)
        {
            int traps = Inventory.Count(ItemId.Trap);
            trapButtonLabel.text = "TRAP  " + traps;
            trapButtonLabel.alpha = traps > 0 ? 1f : 0.45f;
        }

        if (Game.infoText != null && Time.time >= infoMessageUntil)
        {
            Game.infoText.text = string.Empty;
        }

        int score = Flow.Score;
        int combo = Flow.Combo;
        if (Hud != null && (score != shownScore || combo != shownCombo))
        {
            shownScore = score;
            shownCombo = combo;
            Hud.SetScore(score, combo);
        }
    }

    // ---------- Callouts ----------

    public new void ShowMessage(string message, float duration = 1.5f)
    {
        ShowMessage(message, duration, Color.white);
    }

    public new void ShowMessage(string message, float duration, Color color)
    {
        if (Hud != null)
        {
            Hud.Callout(message, color, duration);
            return;
        }
        TMP_Text infoText = Game.infoText;
        if (infoText == null) return;
        infoText.text = message;
        infoMessageUntil = Time.time + duration;
    }

    void CreateInfoText()
    {
        TMP_Text playerHealthText = Game.playerHealthText;
        if (playerHealthText == null) return;

        // clone the health label so the new line matches the existing HUD style
        TMP_Text infoText = Instantiate(playerHealthText, playerHealthText.transform.parent);
        infoText.name = "InfoText";
        RectTransform rect = infoText.rectTransform;
        rect.anchoredPosition -= new Vector2(0f, rect.rect.height + 10f);
        infoText.text = string.Empty;
        Game.infoText = infoText;
    }

    // ---------- Floor grid ----------

    public void SetPlanesVisible(bool visible)
    {
        planesVisible = visible;
        ApplyPlaneVisibility();
    }

    void ApplyPlaneVisibility()
    {
        if (planeManager == null) return;
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.GetComponentsInChildren(true, planeRenderers);
            foreach (Renderer r in planeRenderers) r.enabled = planesVisible;
        }
    }

    // ---------- Home screen and menus ----------

    Canvas SceneCanvas => Game.playerHealthText != null ? Game.playerHealthText.canvas : null;

    void ShowHomeScreen()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null) return;

        Flow.SetState(GameState.Intro);
        SetActiveSafe(Game.scanPrompt, false);
        // the floor grid belongs to the scan step, not the menu
        SetPlanesVisible(false);
        if (homeScreen == null)
        {
            homeScreen = HomeScreen.Show(canvas.rootCanvas, Game.playerHealthText,
                () => Flow.ChooseLevel(GameProgress.UnlockedLevel), OpenMap, ShowLevelPicker,
                Progression.OpenMissionBoard, OpenShop, OpenDex);
        }
        homeScreen.SetVisible(true);
    }

    public void HideHomeScreen()
    {
        if (homeScreen != null) homeScreen.SetVisible(false);
    }

    public void RefreshHomeScreen()
    {
        if (homeScreen != null) homeScreen.Refresh();
    }

    void ShowLevelPicker()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null) return;
        LevelSelectMenu.Show(canvas.rootCanvas, Game.playerHealthText, Flow.ChooseLevel);
    }

    void OpenMap()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null) return;
        MapScreen.Show(canvas.rootCanvas, Game.playerHealthText, Scenes.StartEncounter, RefreshHomeScreen);
    }

    void OpenDex()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null) return;
        DexMenu.Show(canvas.rootCanvas, Game.playerHealthText, null);
    }

    void OpenShop()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null) return;
        ShopMenu.Show(canvas.rootCanvas, Game.playerHealthText, () =>
        {
            Weapons.LoadEquipped();
            RefreshHomeScreen();
        });
    }

    void OpenPauseMenu()
    {
        Canvas canvas = SceneCanvas;
        if (canvas == null || PauseMenu.IsOpen) return;

        GameAudio.Play(GameAudio.DryFire, 0.6f, 0.9f);
        PauseMenu.Show(canvas.rootCanvas, Game.playerHealthText, "Level " + (Flow.CurrentLevelIndex + 1) + ": " + Flow.Level.title,
            null, Flow.RetryLevel, Progression.OpenMissionBoard, Scenes.GoHome);
    }

    // the scene's fonts didn't survive the Unity 6 upgrade (garbled glyphs); use the HUD's font everywhere
    void UnifyFonts()
    {
        TMP_Text playerHealthText = Game.playerHealthText;
        if (playerHealthText == null || playerHealthText.font == null) return;
        TMP_FontAsset font = playerHealthText.font;
        foreach (TMP_Text text in playerHealthText.canvas.rootCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != font) text.font = font;
        }
    }

    // ---------- Buttons ----------

    // top-right under the ammo panel during fights
    void CreatePauseButton()
    {
        RectTransform parent = CanvasRoot();
        if (parent == null) return;
        Button button = UIStyle.NewButton(parent, Game.playerHealthText, "II", UIStyle.Steel, 30f, OpenPauseMenu);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(84f, 84f);
        rect.anchoredPosition = new Vector2(-18f, -118f);
        pauseButton = button.gameObject;
        pauseButton.SetActive(false);
    }

    // above the missions button on the death and victory screens
    void CreateHomeButton()
    {
        RectTransform parent = CanvasRoot();
        if (parent == null) return;
        Button button = UIStyle.NewButton(parent, Game.playerHealthText, "HOME", UIStyle.Blood, 28f, Scenes.GoHome);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(300f, 90f);
        rect.anchoredPosition = new Vector2(0f, 170f);
        homeButton = button.gameObject;
        homeButton.SetActive(false);
    }

    RectTransform CanvasRoot()
    {
        Canvas canvas = SceneCanvas;
        return canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
    }

    // replace the template's flat ellipse buttons and black bands with the game's style
    void StyleSceneButtons()
    {
        StyleButtonObject(Game.shootButton, UIStyle.Blood);
        StyleButtonObject(swapButton, UIStyle.Steel);
        StyleButtonObject(grenadeButton, UIStyle.Toxic);
        StyleButtonObject(trapButton, new Color(0.3f, 0.9f, 1f));
        StyleButtonObject(missionsButton, UIStyle.Gold);

        foreach (GameObject prompt in new[] { Game.greetingPrompt, Game.scanPrompt, Game.youLostPrompt, Game.youWonPrompt })
        {
            if (prompt == null) continue;
            foreach (Button button in prompt.GetComponentsInChildren<Button>(true)) UIStyle.StyleButton(button, UIStyle.Toxic);
            foreach (Image image in prompt.GetComponentsInChildren<Image>(true))
            {
                // dark backing panels, not the buttons we just styled
                if (image.GetComponentInParent<Button>() != null) continue;
                if (image.color.a > 0.5f && image.color.maxColorComponent < 0.2f)
                {
                    image.color = new Color(0.04f, 0.05f, 0.04f, 0.85f);
                }
            }
        }
    }

    static void StyleButtonObject(GameObject target, Color accent)
    {
        if (target == null) return;
        UIStyle.StyleButton(target.GetComponent<Button>(), accent);
    }

    // bottom-centre on menu screens; shows how many missions are ready to claim
    void CreateMissionsButton()
    {
        missionsButton = CloneShootButton("MissionsButton", Progression.OpenMissionBoard, new Vector2(0.5f, 0f),
            new Vector2(0f, 30f), new Vector2(260f, 90f), "MISSIONS", out missionsButtonLabel);
        if (missionsButton != null)
        {
            Image background = missionsButton.GetComponent<Image>();
            if (background != null) background.color = new Color(1f, 0.75f, 0.15f, background.color.a);
        }
    }

    // bottom-right, switches between pistol and shotgun
    void CreateSwapButton()
    {
        swapButton = CloneShootButton("SwapWeaponButton", Weapons.SwitchWeapon, new Vector2(1f, 0f),
            new Vector2(-20f, 20f), new Vector2(150f, 90f), "SWAP", out _);
    }

    // bottom-left, mirrors the swap button
    void CreateGrenadeButton()
    {
        grenadeButton = CloneShootButton("GrenadeButton", Weapons.ThrowGrenade, new Vector2(0f, 0f),
            new Vector2(20f, 20f), new Vector2(190f, 90f), "GRENADE", out grenadeButtonLabel);
        if (grenadeButton != null)
        {
            Image background = grenadeButton.GetComponent<Image>();
            if (background != null) background.color = new Color(0.32f, 0.42f, 0.2f, background.color.a);
        }
    }

    // above the grenade button: throws a containment trap at the zombie under the crosshair
    void CreateTrapButton()
    {
        trapButton = CloneShootButton("TrapButton", Capture.ThrowTrap, new Vector2(0f, 0f),
            new Vector2(20f, 145f), new Vector2(160f, 76f), "TRAP", out trapButtonLabel);
    }

    // extra buttons are cloned from the shoot button so they match the scene's style
    GameObject CloneShootButton(string name, UnityEngine.Events.UnityAction onClick, Vector2 corner,
        Vector2 position, Vector2 size, string text, out TMP_Text label)
    {
        label = null;
        GameObject shootButton = Game.shootButton;
        if (shootButton == null) return null;

        GameObject clone = Instantiate(shootButton, shootButton.transform.parent);
        clone.name = name;

        Button button = clone.GetComponent<Button>();
        if (button != null)
        {
            // drop the cloned ShootBullet binding
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
        }

        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = corner;
        rect.pivot = corner;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;

        clone.SetActive(false);
        return clone;
    }
}
