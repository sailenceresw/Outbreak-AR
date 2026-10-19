using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// what dealt the damage that's being applied right now, so kills can count toward missions
public enum DamageSource { Other, Gun, Grenade }

// The two equipped guns and grenades: ammo, firing, hit detection, aim and the gun model's recoil and sway.
public class WeaponSystem : GameSystem
{
    public int AmmoCount { get; private set; }
    public int ShellCount { get; private set; }
    public int GrenadeCount { get; private set; }
    // which of the two equipped guns is in the player's hands
    public WeaponSlot CurrentSlot { get; private set; } = WeaponSlot.Sidearm;
    // the heavy slot opens on the shotgun's campaign level
    public bool HeavyUnlocked { get; private set; }
    public DamageSource CurrentDamageSource { get; private set; }
    // the zombie under the crosshair, which a trap is thrown at
    public ZombieController AimedZombie { get; private set; }

    WeaponDefinition sidearm;
    WeaponDefinition heavy;
    public WeaponDefinition Sidearm => sidearm;
    public WeaponDefinition Heavy => heavy;
    public WeaponDefinition ActiveWeapon => CurrentSlot == WeaponSlot.Heavy ? heavy : sidearm;
    public bool HoldingHeavy => CurrentSlot == WeaponSlot.Heavy;

    AudioSource gunAudioSource;
    Transform gunTransform;
    Quaternion gunRestRotation;
    bool triggerHeld;
    float nextFireTime;
    float nextGrenadeTime;
    float nextReserveRoundTime;

    // ammo when the level began, restored on a retry
    int levelStartAmmo;
    int levelStartShells;

    // gun sway
    Vector3 lastCameraEuler;
    Vector2 swayAngles;

    // the scene's pistol model, hidden while another gun's model is held
    Renderer[] sceneGunRenderers = new Renderer[0];
    bool[] sceneGunRendererWasOn = new bool[0];
    readonly Dictionary<WeaponId, GameObject> heldModels = new Dictionary<WeaponId, GameObject>();
    readonly Dictionary<WeaponId, Vector3> heldMuzzles = new Dictionary<WeaponId, Vector3>();
    WeaponId? shownWeapon;

    protected override void OnBind()
    {
        if (Game.gun != null)
        {
            gunAudioSource = Game.gun.GetComponent<AudioSource>();
            gunTransform = Game.gun.transform;
            gunRestRotation = gunTransform.localRotation;
            sceneGunRenderers = Game.gun.GetComponentsInChildren<Renderer>(true);
            sceneGunRendererWasOn = new bool[sceneGunRenderers.Length];
            for (int i = 0; i < sceneGunRenderers.Length; i++) sceneGunRendererWasOn[i] = sceneGunRenderers[i].enabled;
        }
        Flow.StateChanged += state =>
        {
            if (state == GameControllerScript.GameState.Lost) triggerHeld = false;
        };
    }

    // ---------- Loadout ----------

    public void LoadEquipped()
    {
        sidearm = Arsenal.Equipped(WeaponSlot.Sidearm);
        heavy = Arsenal.Equipped(WeaponSlot.Heavy);
    }

    public static int StartingAmmo(WeaponDefinition weapon)
    {
        return Mathf.RoundToInt(weapon.startAmmo * Arsenal.AmmoMultiplier(weapon.id));
    }

    // starting loadout for a fresh run from the given level
    public void ResetLoadout(int levelIndex)
    {
        AmmoCount = StartingAmmo(sidearm) + 5 * Mathf.Max(levelIndex - Game.firstLevel, 0) + PlayerProfile.BonusStartingAmmo;
        // the heavy weapon is announced on its unlock level, later starts already have it
        HeavyUnlocked = levelIndex > LevelCatalog.ShotgunUnlockLevel;
        ShellCount = HeavyUnlocked ? StartingAmmo(heavy) : 0;
        GrenadeCount = 0;
        CurrentSlot = WeaponSlot.Sidearm;
    }

    // the loadout carried over from the previous chapter's scene
    public void RestoreCarried(int ammo, int shells, int grenades, int startLevel)
    {
        AmmoCount = Mathf.Max(ammo, StartingAmmo(sidearm));
        ShellCount = shells;
        GrenadeCount = grenades;
        HeavyUnlocked = startLevel > LevelCatalog.ShotgunUnlockLevel;
    }

    // ammo and grenades as a level begins; returns true when the heavy weapon unlocks on this level
    public bool PrepareForLevel(int index, bool isRetry, int startLevelIndex, int bonusAmmo)
    {
        if (!isRetry && bonusAmmo > 0 && index != startLevelIndex)
        {
            AmmoCount += bonusAmmo;
        }

        bool newWeapon = false;
        if (!HeavyUnlocked && index >= LevelCatalog.ShotgunUnlockLevel)
        {
            HeavyUnlocked = true;
            ShellCount += StartingAmmo(heavy);
            newWeapon = true;
        }
        GrenadeCount = Mathf.Max(GrenadeCount, Game.grenadesPerLevel + PlayerProfile.BonusGrenades);
        return newWeapon;
    }

    public void SnapshotLevelStart()
    {
        levelStartAmmo = AmmoCount;
        levelStartShells = ShellCount;
    }

    public void RestoreForRetry()
    {
        triggerHeld = false;
        AmmoCount = Mathf.Max(levelStartAmmo, StartingAmmo(sidearm));
        ShellCount = HeavyUnlocked ? Mathf.Max(levelStartShells, StartingAmmo(heavy) / 2) : 0;
    }

    // consumes shop items that act at the start of a level; returns a line for the level banner
    public string UseLevelStartItems()
    {
        string note = string.Empty;
        if (Inventory.Use(ItemId.GrenadePack))
        {
            GrenadeCount = Mathf.Min(GrenadeCount + 2, Game.maxGrenades + 2);
            note += "\n<color=#9BE36A>Grenade Pack: +2 grenades</color>";
        }
        if (Inventory.Use(ItemId.AmmoCache))
        {
            AmmoCount *= 2;
            ShellCount *= 2;
            note += "\n<color=#9BE36A>Ammo Cache: double ammo</color>";
        }
        return note;
    }

    // amount comes from the crate prefab; each gun refills by its own crate size instead
    public void AddAmmo(int amount)
    {
        int rounds = Mathf.RoundToInt(sidearm.ammoPerCrate * Arsenal.AmmoMultiplier(sidearm.id));
        AmmoCount += rounds;
        GameAudio.Play(GameAudio.Pickup, 0.7f);
        MissionSystem.Report(MissionType.CollectPickups);

        string message = "+" + rounds + " ammo";
        if (HeavyUnlocked)
        {
            int heavyRounds = Mathf.RoundToInt(heavy.ammoPerCrate * Arsenal.AmmoMultiplier(heavy.id));
            ShellCount += heavyRounds;
            message += "  +" + heavyRounds + " " + heavy.ammoName.ToLower();
        }
        if (GrenadeCount < Game.maxGrenades)
        {
            GrenadeCount++;
            message += "  +1 grenade";
        }
        ShowMessage(message);
    }

    // below the cap, a reserve round trickles in so the player is never defenceless
    public void TickReserveAmmo()
    {
        if (AmmoCount >= Game.reserveAmmoCap)
        {
            nextReserveRoundTime = Time.time + Game.reserveAmmoInterval;
            return;
        }
        if (Time.time < nextReserveRoundTime) return;

        AmmoCount++;
        nextReserveRoundTime = Time.time + Game.reserveAmmoInterval;
    }

    // ---------- Firing ----------

    // automatic guns keep firing while the trigger is held
    public void TickAutoFire()
    {
        if (triggerHeld && Flow.IsPlaying && ActiveWeapon.automatic && !PauseMenu.IsOpen) Shoot();
    }

    public void Shoot()
    {
        if (!Flow.IsPlaying || MainCamera == null) return;
        if (Time.time < nextFireTime) return;

        WeaponDefinition weapon = ActiveWeapon;
        bool heavyShot = HoldingHeavy;
        bool rapidFire = Pickups.RapidFireActive;
        float interval = weapon.fireInterval * Arsenal.FireIntervalMultiplier(weapon.id);
        nextFireTime = Time.time + (rapidFire ? interval * 0.5f : interval);

        Transform cameraTransform = MainCamera.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if ((heavyShot ? ShellCount : AmmoCount) <= 0 && !rapidFire)
        {
            if (heavyShot)
            {
                SwapToSidearm("Out of " + weapon.ammoName.ToLower() + "! " + sidearm.name + " ready");
                return;
            }
            // dry fire can still grab a pickup, otherwise the player could soft-lock with no ammo
            if (CastShot(ray, weapon.range, out RaycastHit pickupHit))
            {
                PickupManager.TryCollect(pickupHit.collider);
            }
            GameAudio.Play(GameAudio.DryFire, 0.8f);
            ShowMessage("Out of ammo! Reserve rounds incoming");
            triggerHeld = false;
            return;
        }

        // rapid fire rounds are free
        if (!rapidFire)
        {
            if (heavyShot) ShellCount--;
            else AmmoCount--;
        }

        if (gunAudioSource != null && gunAudioSource.clip != null)
        {
            // one gunshot clip, pitched and levelled per gun
            gunAudioSource.pitch = weapon.soundPitch * Random.Range(0.95f, 1.05f) * (rapidFire ? 1.1f : 1f);
            gunAudioSource.PlayOneShot(gunAudioSource.clip, weapon.soundVolume);
        }
        if (gunTransform != null)
        {
            gunTransform.localRotation = gunRestRotation * Quaternion.Euler(-weapon.recoilAngle, 0f, 0f);
        }

        bool big = weapon.pellets > 1 || weapon.damage >= 100f;
        Vector3 muzzle = gunTransform == null ? cameraTransform.position + cameraTransform.forward * 0.4f
            : heldMuzzles.TryGetValue(weapon.id, out Vector3 muzzleLocal) ? gunTransform.TransformPoint(muzzleLocal)
            : gunTransform.position + cameraTransform.forward * 0.25f;
        GameFX.MuzzleFlash(muzzle, cameraTransform.forward, big);
        GameFX.FlashLight(muzzle, new Color(1f, 0.75f, 0.4f), big ? 5f : 3f, big ? 4f : 3f, 0.06f);
        GameFX.ShellCasing(muzzle - cameraTransform.forward * 0.15f + cameraTransform.right * 0.03f,
            cameraTransform.right, cameraTransform.up, weapon.id == WeaponId.Shotgun);

        bool hitZombie = FireWeapon(weapon, cameraTransform, ray);
        if (weapon.shake > 0f && Hud != null) Hud.Shake(weapon.shake);

        Flow.RecordShot(hitZombie);

        // don't leave the player holding an empty heavy weapon
        if (heavyShot && !rapidFire && ShellCount <= 0) SwapToSidearm("Out of " + weapon.ammoName.ToLower() + "! " + sidearm.name + " ready");
    }

    void SwapToSidearm(string message)
    {
        CurrentSlot = WeaponSlot.Sidearm;
        nextFireTime = Mathf.Max(nextFireTime, Time.time + 0.25f);
        GameAudio.Play(GameAudio.DryFire, 0.9f, 1.2f);
        RefreshCrosshair();
        ShowMessage(message, 1.5f, new Color(1f, 0.6f, 0.1f));
    }

    // hitscan so fast bullets can't tunnel through zombies; the bullet prefab is only a tracer.
    // Every gun is a cone of pellets (one pellet for most); damage per zombie is added up and applied once.
    bool FireWeapon(WeaponDefinition weapon, Transform cameraTransform, Ray ray)
    {
        Dictionary<ZombieController, float> damageByZombie = new Dictionary<ZombieController, float>();
        Dictionary<ZombieController, Vector3> hitPoints = new Dictionary<ZombieController, Vector3>();
        HashSet<ZombieController> headshots = new HashSet<ZombieController>();
        float spread = Mathf.Tan(weapon.spreadAngle * Mathf.Deg2Rad);
        float damagePerPellet = weapon.damage * Arsenal.DamageMultiplier(weapon.id) * Pickups.DamageBoost;

        for (int i = 0; i < weapon.pellets; i++)
        {
            Vector3 direction = spread > 0f ? (ray.direction + Random.insideUnitSphere * spread).normalized : ray.direction;
            Ray pellet = new Ray(ray.origin, direction);
            Vector3 endPoint = pellet.GetPoint(weapon.range);

            if (CastShot(pellet, weapon.range, out RaycastHit hit))
            {
                endPoint = hit.point;
                ZombieController zombie = hit.collider.GetComponentInParent<ZombieController>();
                if (zombie != null && !zombie.IsDead)
                {
                    bool headshot = zombie.IsHeadshot(hit.point);
                    float falloff = Mathf.Lerp(1f, 0.4f, Mathf.InverseLerp(weapon.falloffStart, weapon.range, hit.distance));
                    float damage = damagePerPellet * falloff * (headshot ? weapon.headshotMultiplier : 1f);

                    damageByZombie.TryGetValue(zombie, out float total);
                    damageByZombie[zombie] = total + damage;
                    hitPoints[zombie] = hit.point;
                    if (headshot) headshots.Add(zombie);
                }
                else if (zombie == null)
                {
                    AcidGlob glob = hit.collider.GetComponent<AcidGlob>();
                    if (glob != null)
                    {
                        glob.Pop();
                        Flow.AddScore(50);
                        ShowMessage("ACID SHOT DOWN!", 1f, new Color(0.55f, 1f, 0.2f));
                    }
                    else
                    {
                        PickupManager.TryCollect(hit.collider);
                    }
                }
            }

            // a few tracers sell a spread without spawning one per pellet
            if (weapon.pellets == 1 || i % 3 == 0) SpawnTracer(cameraTransform, endPoint);
        }

        foreach (KeyValuePair<ZombieController, float> entry in damageByZombie)
        {
            if (entry.Key == null) continue;
            ApplyHit(entry.Key, entry.Value, hitPoints[entry.Key], ray.direction, headshots.Contains(entry.Key));
        }
        return damageByZombie.Count > 0;
    }

    void ApplyHit(ZombieController zombie, float damage, Vector3 point, Vector3 direction, bool headshot)
    {
        CurrentDamageSource = DamageSource.Gun;
        zombie.AddWound(point, direction);
        bool killed = zombie.TakeDamage(damage, direction);
        CurrentDamageSource = DamageSource.Other;
        if (headshot && Flow.IsPlaying)
        {
            MissionSystem.Report(MissionType.Headshots);
            if (killed) Progression.AwardXP(5);
        }

        GameFX.Blood(point, -direction + Vector3.up * 0.3f, headshot);
        // splashes reach the floor under the zombie
        if (Random.value < 0.5f)
        {
            Vector2 offset = Random.insideUnitCircle * 0.3f;
            GameFX.BloodPool(zombie.transform.position + new Vector3(offset.x, 0f, offset.y), 0.35f);
        }
        if (killed && headshot)
        {
            zombie.Decapitate();
            if (Hud != null) Hud.Shake(6f);
            if (PostFX.Instance != null) PostFX.Instance.Jolt(0.35f);
            Flow.PlayTimeEffect(0.05f, 0.09f);
        }
        GameAudio.Play(killed ? GameAudio.KillMarker : GameAudio.HitMarker, 0.5f);
        if (Hud != null)
        {
            Hud.HitMarker(killed, headshot);
            Hud.ShowDamageNumber(point, damage, headshot, killed);
        }

        if (headshot)
        {
            if (killed) Flow.AddScore(50 * Mathf.Max(Flow.Combo, 1));
            ShowMessage(killed ? "HEADSHOT!" : "Headshot!", 1.2f, new Color(1f, 0.85f, 0.15f));
        }
    }

    public void SwitchWeapon()
    {
        if (!HeavyUnlocked) return;
        GameControllerScript.GameState state = Flow.State;
        if (state != GameControllerScript.GameState.Playing && state != GameControllerScript.GameState.LevelIntro) return;

        CurrentSlot = HoldingHeavy ? WeaponSlot.Sidearm : WeaponSlot.Heavy;
        nextFireTime = Time.time + 0.25f;

        // a heavy clack for the big gun, a light click for the sidearm
        GameAudio.Play(GameAudio.DryFire, 0.9f, HoldingHeavy ? 0.6f : 1.2f);
        RefreshCrosshair();
        if (gunTransform != null) gunTransform.localRotation = gunRestRotation * Quaternion.Euler(25f, 0f, 0f);
        ShowMessage(ActiveWeapon.name.ToUpper(), 1f);
    }

    // a wide ring for guns that spray
    public void RefreshCrosshair()
    {
        if (Hud != null) Hud.SetCrosshairWide(ActiveWeapon.pellets > 1 || ActiveWeapon.spreadAngle >= 2f);
    }

    // hold-to-fire for automatic guns: the FIRE button reports press and release
    public void AddTriggerHold(GameObject shootButton)
    {
        if (shootButton == null) return;
        EventTrigger trigger = shootButton.GetComponent<EventTrigger>();
        if (trigger == null) trigger = shootButton.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, () => triggerHeld = true);
        AddTrigger(trigger, EventTriggerType.PointerUp, () => triggerHeld = false);
        AddTrigger(trigger, EventTriggerType.PointerExit, () => triggerHeld = false);
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    // ---------- Grenades ----------

    public void ThrowGrenade()
    {
        if (!Flow.IsPlaying || MainCamera == null) return;
        if (Time.time < nextGrenadeTime) return;
        if (GrenadeCount <= 0)
        {
            GameAudio.Play(GameAudio.DryFire, 0.8f, 0.8f);
            ShowMessage("No grenades! Ammo crates carry one");
            return;
        }

        GrenadeCount--;
        nextGrenadeTime = Time.time + 1f;

        Transform cameraTransform = MainCamera.transform;
        Vector3 from = cameraTransform.position + cameraTransform.forward * 0.3f - cameraTransform.up * 0.1f;
        GameAudio.Play(GameAudio.Throw, 0.8f);
        Grenade.Throw(from, GrenadeTarget(cameraTransform), Game.grenadeFlightTime, ExplodeGrenade);
    }

    // where the view ray meets the floor, kept within throwing range
    Vector3 GrenadeTarget(Transform cameraTransform)
    {
        Vector3 floor = FloorUnderPlayer();
        Vector3 flatForward = cameraTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector2 throwRange = Game.grenadeThrowRange;
        float distance = throwRange.y * 0.55f;
        // only aim at the floor when looking down at it; looking level or up lobs it a fixed distance
        if (cameraTransform.forward.y < -0.05f)
        {
            float rayDistance = (floor.y - cameraTransform.position.y) / cameraTransform.forward.y;
            Vector3 hit = cameraTransform.position + cameraTransform.forward * rayDistance;
            Vector3 offset = hit - floor;
            offset.y = 0f;
            distance = offset.magnitude;
        }
        distance = Mathf.Clamp(distance, throwRange.x, throwRange.y);
        return floor + flatForward * distance;
    }

    void ExplodeGrenade(Vector3 center)
    {
        GameFX.Explosion(center);
        GameAudio.Play(GameAudio.Explosion, 1f, Random.Range(0.9f, 1.05f));

        if (MainCamera != null && Hud != null)
        {
            // closer blasts shake harder
            float playerDistance = Vector3.Distance(FloorUnderPlayer(), center);
            Hud.Shake(Mathf.Lerp(18f, 6f, Mathf.InverseLerp(1.5f, 8f, playerDistance)));
            if (PostFX.Instance != null) PostFX.Instance.Jolt(Mathf.Lerp(0.9f, 0.35f, Mathf.InverseLerp(1.5f, 8f, playerDistance)));
        }
        if (!Flow.IsPlaying) return;

        float grenadeRadius = Game.grenadeRadius;
        int kills = 0;
        foreach (ZombieController zombie in Spawner.Alive.ToArray())
        {
            if (zombie == null || zombie.IsDead) continue;
            Vector3 offset = zombie.transform.position - center;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > grenadeRadius) continue;

            float falloff = Mathf.Lerp(1f, 0.35f, distance / grenadeRadius);
            float damage = Game.grenadeDamage * falloff * (zombie.IsBoss ? 0.6f : 1f) * Pickups.DamageBoost;
            Vector3 direction = distance > 0.01f ? offset / distance : Vector3.forward;
            CurrentDamageSource = DamageSource.Grenade;
            bool killed = zombie.TakeDamage(damage, direction);
            CurrentDamageSource = DamageSource.Other;
            if (killed) kills++;

            Vector3 chest = zombie.transform.position + Vector3.up * 1.2f * zombie.transform.localScale.y;
            GameFX.Blood(chest, direction + Vector3.up * 0.5f, false);
            if (Hud != null) Hud.ShowDamageNumber(chest, damage, false, killed);
        }

        if (kills >= 2)
        {
            MissionSystem.Report(MissionType.GrenadeMultiKill);
            Flow.AddScore(150 * (kills - 1));
            ShowMessage(kills >= 4 ? "MASSACRE! x" + kills : "MULTI KILL x" + kills, 1.8f, new Color(1f, 0.55f, 0.15f));
            GameAudio.Play(GameAudio.KillMarker, 0.8f, 0.8f);
        }
    }

    // ---------- Aim ----------

    // turn the crosshair red when it's over a zombie
    public void UpdateAim()
    {
        if (Hud == null) return;
        bool onTarget = false;
        AimedZombie = null;
        if (Flow.IsPlaying && MainCamera != null)
        {
            // traps reach further than a shotgun, so look the full distance and check gun range separately
            Ray ray = new Ray(MainCamera.transform.position, MainCamera.transform.forward);
            if (CastShot(ray, 30f, out RaycastHit hit))
            {
                ZombieController zombie = hit.collider.GetComponentInParent<ZombieController>();
                if (zombie != null && !zombie.IsDead)
                {
                    AimedZombie = zombie;
                    onTarget = hit.distance <= ActiveWeapon.range;
                }
            }
        }
        Hud.SetAimOnTarget(onTarget);
    }

    // nearest hit along the shot, ignoring the gun model and tracers
    bool CastShot(Ray ray, float distance, out RaycastHit closest)
    {
        closest = default;
        float closestDistance = float.MaxValue;
        RaycastHit[] hits = Physics.SphereCastAll(ray, Game.shotRadius, distance, ~0, QueryTriggerInteraction.Collide);
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

    void SpawnTracer(Transform cameraTransform, Vector3 endPoint)
    {
        if (Game.bulletPrefab == null) return;

        // start slightly below the view so the tracer doesn't fill the screen
        Vector3 start = cameraTransform.position + cameraTransform.forward * 0.2f - cameraTransform.up * 0.05f;
        Vector3 direction = endPoint - start;
        if (direction.sqrMagnitude < 0.0001f) return;
        GameObject bullet = Instantiate(Game.bulletPrefab, start, Quaternion.LookRotation(direction));

        // damage is applied by the raycast, so the tracer must not trigger anything
        foreach (Collider c in bullet.GetComponentsInChildren<Collider>()) c.enabled = false;

        float bulletSpeed = Game.bulletSpeed;
        Rigidbody body = bullet.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.linearVelocity = direction.normalized * bulletSpeed;
        }
        Destroy(bullet, Mathf.Min(direction.magnitude / bulletSpeed, 3f));
    }

    // ---------- Gun model ----------

    // the model in the player's hands follows whichever gun is active
    void ShowHeldModel()
    {
        if (sidearm == null) return;
        WeaponId id = ActiveWeapon.id;
        if (shownWeapon == id) return;
        shownWeapon = id;

        if (WeaponModels.HasOwnModel(id) && !heldModels.ContainsKey(id))
        {
            heldModels[id] = WeaponModels.Create(id, gunTransform, out Vector3 muzzleLocal);
            if (heldModels[id] != null) heldMuzzles[id] = muzzleLocal;
        }

        bool ownModel = heldModels.TryGetValue(id, out GameObject shown) && shown != null;
        for (int i = 0; i < sceneGunRenderers.Length; i++)
        {
            if (sceneGunRenderers[i] != null) sceneGunRenderers[i].enabled = !ownModel && sceneGunRendererWasOn[i];
        }
        foreach (KeyValuePair<WeaponId, GameObject> model in heldModels)
        {
            if (model.Value != null) model.Value.SetActive(model.Key == id);
        }
    }

    public void UpdateGunSway()
    {
        if (gunTransform == null) return;
        ShowHeldModel();
        // the gun trails the view a little when turning and breathes at rest
        Quaternion sway = Quaternion.identity;
        if (MainCamera != null && Time.deltaTime > 0f)
        {
            Vector3 euler = MainCamera.transform.eulerAngles;
            float yaw = Mathf.DeltaAngle(lastCameraEuler.y, euler.y);
            float pitch = Mathf.DeltaAngle(lastCameraEuler.x, euler.x);
            lastCameraEuler = euler;
            swayAngles = Vector2.Lerp(swayAngles, new Vector2(Mathf.Clamp(pitch * 0.6f, -5f, 5f), Mathf.Clamp(-yaw * 0.6f, -5f, 5f)), Time.deltaTime * 8f);
            float breath = Mathf.Sin(Time.time * 1.6f) * 0.6f;
            sway = Quaternion.Euler(swayAngles.x + breath, swayAngles.y, swayAngles.y * 0.6f);
        }
        gunTransform.localRotation = Quaternion.Slerp(gunTransform.localRotation, gunRestRotation * sway, Time.deltaTime * 15f);
    }
}
