using System.Collections.Generic;
using UnityEngine;

// Supply drops during a fight, power-ups dropped by zombies, and the timed power-up effects.
public class PickupManager : GameSystem
{
    readonly List<GameObject> spawnedPickups = new List<GameObject>();
    GameObject activeAmmoPickup;
    float nextPickupTime;

    float rapidFireUntil;
    float hollowPointsUntil;
    float cryoUntil;

    // rapid fire rounds are free and twice as fast
    public bool RapidFireActive => Time.time < rapidFireUntil;
    public bool CryoActive => Time.time < cryoUntil;
    // hollow points double all damage
    public float DamageBoost => Time.time < hollowPointsUntil ? 2f : 1f;
    // zombies move and attack slower while the cryo pulse is active
    public float ZombieTimeScale => CryoActive ? 0.3f : 1f;

    protected override void OnBind()
    {
        Flow.LevelPrepared += _ => rapidFireUntil = hollowPointsUntil = cryoUntil = 0f;
        Flow.LevelStarted += () => nextPickupTime = Time.time + Game.pickupSpawnInterval;
        Spawner.ZombieKilled += DropPowerUp;
    }

    public void Tick()
    {
        // never leave the player stuck without ammo
        bool outOfAmmo = Weapons.AmmoCount <= 0 && activeAmmoPickup == null;
        if (!outOfAmmo && Time.time < nextPickupTime) return;
        nextPickupTime = Time.time + Game.pickupSpawnInterval;

        GameObject ammoPrefab = Game.ammoPickupPrefab;
        GameObject healthPrefab = Game.healthPickupPrefab;
        bool needsAmmo = outOfAmmo || Weapons.AmmoCount <= Game.lowAmmoThreshold;
        bool needsHealth = Player.Current < Player.Max * 0.5f;
        GameObject prefab = needsAmmo ? ammoPrefab
            : needsHealth ? healthPrefab
            : (Random.value < 0.5f ? ammoPrefab : healthPrefab);
        if (prefab == null) return;

        GameObject pickup = SpawnPickup(prefab);
        if (prefab == ammoPrefab) activeAmmoPickup = pickup;
    }

    GameObject SpawnPickup(GameObject prefab)
    {
        // place it in front of the player, roughly at floor level, so it's easy to find
        Vector3 forward = MainCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * forward.normalized;

        Vector3 position = MainCamera.transform.position + forward * Random.Range(1.5f, 2.5f);
        position.y = ARWorld.FloorY;

        GameObject pickup = Instantiate(prefab, position, Quaternion.LookRotation(-forward));
        TimedObjectDestructor destructor = pickup.AddComponent<TimedObjectDestructor>();
        destructor.time = Game.pickupLifetime;
        spawnedPickups.RemoveAll(p => p == null);
        spawnedPickups.Add(pickup);

        GameFX.Sparkle(position + Vector3.up * 0.2f, Color.white);
        ShowMessage(prefab == Game.ammoPickupPrefab ? "Supply drop: ammo crate!" : "Supply drop: med kit!");
        return pickup;
    }

    void DropPowerUp(ZombieController zombie)
    {
        if (!Flow.IsPlaying) return;

        float dropChance = zombie.Type == ZombieType.Brute ? Game.bruteDropChance : Game.powerUpDropChance;
        if (!zombie.IsBoss && Random.value < dropChance)
        {
            PowerUpType type = (PowerUpType)Random.Range(0, 4);
            PowerUpPickup powerUp = PowerUpPickup.Spawn(type, zombie.transform.position + Vector3.up * 0.9f);
            spawnedPickups.Add(powerUp.gameObject);
            ShowMessage(PowerUpPickup.DisplayName(type) + " dropped!");
        }
    }

    public void ActivatePowerUp(PowerUpType type)
    {
        GameAudio.Play(GameAudio.PowerUp, 0.8f);
        ShowMessage(PowerUpPickup.DisplayName(type) + "!", 2f);
        MissionSystem.Report(MissionType.CollectPickups);

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
                if (Hud != null) Hud.Shake(12f);
                foreach (ZombieController zombie in Spawner.Alive.ToArray())
                {
                    if (zombie == null) continue;
                    Vector3 offset = zombie.transform.position - center;
                    offset.y = 0f;
                    if (offset.magnitude <= 5f) zombie.TakeDamage(zombie.IsBoss ? 200f : 250f, offset.normalized);
                }
                break;
        }
    }

    // the active power-ups and their seconds left, for under the objective
    public string PowerUpStatus()
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

    public void ClearAll()
    {
        foreach (GameObject pickup in spawnedPickups)
        {
            if (pickup != null) Destroy(pickup);
        }
        spawnedPickups.Clear();
    }

    // a shot or tap that lands on a pickup collects it
    public static bool TryCollect(Collider other)
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
}
