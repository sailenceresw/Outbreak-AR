using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Zombies rising around the player: the steady trickle, horde rushes, the boss and its minions.
// Owns the list of zombies still standing.
public class SpawnManager : GameSystem
{
    // a zombie died (raised for every death; handlers check whether a fight is on)
    public event System.Action<ZombieController> ZombieKilled;

    // how long a spawn spot is marked before the zombie rises
    const float SpawnWarningSeconds = 1.5f;

    readonly List<ZombieController> aliveZombies = new List<ZombieController>();
    int zombiesSpawned;
    // marked spots whose zombie hasn't risen yet; they count toward the alive cap
    int pendingSpawns;
    // no spawning until then (the break between Endless waves)
    float pausedUntil;

    // the zombies placed for a hunt, until they're found
    readonly List<ZombieController> hidden = new List<ZombieController>();
    public List<ZombieController> Hidden => hidden;
    ZombieController boss;
    bool bossSpawned;
    float bossSpawnTime;
    float nextSpawnTime;
    float nextHordeTime;
    Coroutine hordeRoutine;

    public List<ZombieController> Alive => aliveZombies;
    public ZombieController Boss => boss;
    public bool BossSpawned => bossSpawned;
    public bool BossAlive => boss != null && !boss.IsDead;
    public bool HordeActive { get; private set; }

    protected override void OnBind()
    {
        Flow.LevelPrepared += _ => ResetForLevel();
        Flow.LevelStarted += StartTimers;
        Flow.StateChanged += state =>
        {
            if (state == GameControllerScript.GameState.Lost) CelebrateAll();
        };
        Capture.ZombieCaptured += zombie => aliveZombies.Remove(zombie);
    }

    void ResetForLevel()
    {
        zombiesSpawned = 0;
        pendingSpawns = 0;
        pausedUntil = 0f;
        hidden.Clear();
        boss = null;
        bossSpawned = false;
        if (hordeRoutine != null) StopCoroutine(hordeRoutine);
        HordeActive = false;
    }

    void StartTimers()
    {
        nextSpawnTime = Time.time + 0.5f;
        nextHordeTime = Time.time + Random.Range(18f, 26f);
        bossSpawnTime = Time.time + 1f;
        if (Flow.Level.objective == LevelObjective.Hunt) PlaceHunt();
    }

    // ---------- Endless waves ----------

    // a fresh wave: the level's numbers have been raised; spawning resumes after the break
    public void StartWave(float breakSeconds)
    {
        zombiesSpawned = 0;
        pausedUntil = Time.time + breakSeconds;
        nextHordeTime = Mathf.Max(nextHordeTime, pausedUntil + Random.Range(10f, 18f));
    }

    // ---------- Hunt ----------

    // the hunted zombies, idle at spots around the room out of view, as far apart as the room allows
    void PlaceHunt()
    {
        hidden.Clear();
        int count = Flow.Level.zombieCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 spot = HidingSpot();
            ZombieController zombie = SpawnZombie(RollZombieType(), spot, true, true);
            if (zombie != null) hidden.Add(zombie);
        }
    }

    Vector3 HidingSpot()
    {
        Vector3 best = ChooseSpawnPoint(false);
        float bestGap = -1f;
        // a few candidates; keep the one furthest from the zombies already hidden
        for (int i = 0; i < 6; i++)
        {
            Vector3 candidate = ChooseSpawnPoint(false);
            float gap = float.MaxValue;
            foreach (ZombieController other in hidden)
            {
                if (other != null) gap = Mathf.Min(gap, Vector3.Distance(other.transform.position, candidate));
            }
            if (gap > bestGap)
            {
                bestGap = gap;
                best = candidate;
            }
        }
        return best;
    }

    public void PruneDead()
    {
        aliveZombies.RemoveAll(z => z == null);
    }

    public void Tick()
    {
        UpdateSpawning();
        UpdateHorde();
    }

    void UpdateSpawning()
    {
        LevelDefinition level = Flow.Level;
        if (level.objective == LevelObjective.Boss && !bossSpawned)
        {
            if (Time.time >= bossSpawnTime) SpawnBoss();
            return;
        }
        // a hunt's zombies are all placed at the start; nothing new rises
        if (level.objective == LevelObjective.Hunt) return;

        if (Time.time < nextSpawnTime || Time.time < pausedUntil) return;
        int maxAlive = level.maxAlive + (boss != null && !boss.IsDead ? 1 : 0);
        if (aliveZombies.Count + pendingSpawns >= maxAlive) return;
        if (level.objective != LevelObjective.Survive && zombiesSpawned >= level.zombieCount) return;

        // early levels spawn in front so new players see what's attacking them; later ones come from
        // out of view and walk in. Either way the spot is marked first, so nothing pops up unannounced.
        bool inFront = Flow.CurrentLevelIndex < Game.frontSpawnLevels;
        StartCoroutine(SpawnWithWarning(RollZombieType(), ChooseSpawnPoint(inFront)));
        zombiesSpawned++;
        nextSpawnTime = Time.time + Random.Range(level.spawnInterval.x, level.spawnInterval.y);
    }

    // where the camera faces on the floor plane, in radians (matching RingPosition's angle)
    float FacingAngle()
    {
        Vector3 forward = MainCamera.transform.forward;
        return Mathf.Atan2(forward.z, forward.x);
    }

    // half the screen's width as an angle, plus a margin so zombies rise just out of sight
    float OutOfViewAngle()
    {
        float horizontalFov = Camera.VerticalToHorizontalFieldOfView(MainCamera.fieldOfView, MainCamera.aspect);
        return Mathf.Min(horizontalFov * 0.5f + 15f, 150f);
    }

    // a spot on real floor: in front (within frontSpawnHalfAngle) or anywhere out of view.
    // Prefers mapped floor and tries closer in if the room is small; falls back to the plain ring.
    Vector3 ChooseSpawnPoint(bool inFront)
    {
        if (MainCamera == null) return RingPosition(Random.Range(0f, Mathf.PI * 2f));

        float facing = FacingAngle();
        float hidden = OutOfViewAngle();
        Vector3 first = Vector3.zero;
        for (int i = 0; i < 24; i++)
        {
            float offset = inFront
                ? Random.Range(-Game.frontSpawnHalfAngle, Game.frontSpawnHalfAngle)
                : (Random.value < 0.5f ? -1f : 1f) * Random.Range(hidden, 180f);
            // later tries come closer, for rooms smaller than the spawn ring
            float near = i < 12 ? Game.spawnDistanceRange.x : 1.8f;
            Vector3 point = RingPosition(facing + offset * Mathf.Deg2Rad, Random.Range(near, Game.spawnDistanceRange.y));
            if (i == 0) first = point;
            if (ARWorld.IsOverFloor(point)) return point;
        }
        return first;
    }

    // blood seeps up and a groan comes from the spot, then the zombie claws out of the floor there
    IEnumerator SpawnWithWarning(ZombieType type, Vector3 position)
    {
        pendingSpawns++;
        Vector3 floor = new Vector3(position.x, ARWorld.FloorY, position.z);
        GameFX.BloodPool(floor, 0.55f);
        GameFX.Dust(floor, 0.6f);
        AudioClip groan = GroanClip();
        if (groan != null) GameAudio.PlayAt(groan, floor, 0.9f, Random.Range(0.7f, 0.85f));

        yield return new WaitForSeconds(SpawnWarningSeconds);
        pendingSpawns = Mathf.Max(pendingSpawns - 1, 0);
        // already counted toward the level when it was queued
        if (Flow.IsPlaying) SpawnZombie(type, position, false);
    }

    public AudioClip GroanClip()
    {
        AudioSource source = Game.zombiePrefab != null ? Game.zombiePrefab.GetComponent<AudioSource>() : null;
        return source != null ? source.clip : null;
    }

    ZombieType RollZombieType()
    {
        LevelDefinition level = Flow.Level;
        // each chance takes its own slice of the roll; whatever's left is a walker
        float roll = Random.value;
        float threshold = level.bruteChance;
        if (roll < threshold) return ZombieType.Brute;
        threshold += level.spitterChance;
        if (roll < threshold) return ZombieType.Spitter;
        threshold += level.crawlerChance;
        if (roll < threshold) return ZombieController.HasMixamoAnimations ? ZombieType.Crawler : ZombieType.Walker;
        threshold += level.runnerChance;
        if (roll < threshold) return ZombieType.Runner;
        return ZombieType.Walker;
    }

    Vector3 RingPosition(float angle)
    {
        return RingPosition(angle, Random.Range(Game.spawnDistanceRange.x, Game.spawnDistanceRange.y));
    }

    Vector3 RingPosition(float angle, float distance)
    {
        Vector3 cameraPosition = MainCamera != null ? MainCamera.transform.position : Vector3.zero;
        return cameraPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    ZombieController SpawnZombie(ZombieType type, Vector3 position, bool countsTowardLevel, bool dormant = false)
    {
        if (Game.zombiePrefab == null || MainCamera == null) return null;

        Vector3 toCamera = MainCamera.transform.position - position;
        toCamera.y = 0f;
        GameObject zombieObject = Instantiate(Game.zombiePrefab, position,
            toCamera.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCamera) : Quaternion.identity);

        ZombieController zombie = zombieObject.GetComponent<ZombieController>();
        if (zombie == null) return null;
        if (dormant) zombie.MakeDormant();

        // the realistic Mixamo characters when present, otherwise the original pack's variants
        GameObject model = ZombieController.RealisticModelFor(type);
        if (model == null)
        {
            if (type == ZombieType.Runner) model = Game.runnerModel;
            else if (type == ZombieType.Brute || type == ZombieType.Boss) model = Game.bruteModel;
        }
        SiteEncounter encounter = Flow.Encounter;
        zombie.Configure(type, Flow.Level.speedMultiplier, model, encounter == null && LevelCatalog.IsFinalBoss(Flow.CurrentLevelIndex));

        // rare glowing mutants of the catchable kinds; map sites are where they gather
        float chance = encounter != null ? encounter.mutantChance : Game.mutantChance;
        if (ZombieDex.Get(type).catchable && Random.value < chance) zombie.MakeMutant();
        ZombieDex.RecordSeen(type);

        aliveZombies.Add(zombie);
        if (countsTowardLevel) zombiesSpawned++;
        return zombie;
    }

    void SpawnBoss()
    {
        bossSpawned = true;

        // rise right in front of the player for maximum drama
        Vector3 forward = MainCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 position = MainCamera.transform.position + forward.normalized * 5f;

        boss = SpawnZombie(ZombieType.Boss, position, false);
        if (Hud != null)
        {
            Hud.Shake(14f);
            Hud.ShowBanner(boss != null ? boss.DisplayName : "BOSS", "It's here.", 2f);
        }
        nextSpawnTime = Time.time + 4f;
    }

    // called by the boss to call in reinforcements
    public void SummonMinions(Vector3 center, int count)
    {
        if (!Flow.IsPlaying) return;

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
        if (Hud != null)
        {
            Hud.Shake(16f);
            Hud.ShowBanner("IT'S ENRAGED!", "Keep firing!", 1.5f);
        }
    }

    void UpdateHorde()
    {
        LevelDefinition level = Flow.Level;
        if (!level.hordeEvents || HordeActive || Time.time < nextHordeTime) return;
        if (CountsDown(level) && zombiesSpawned >= level.zombieCount) return;

        nextHordeTime = Time.time + Random.Range(25f, 35f);
        hordeRoutine = StartCoroutine(HordeRush());
    }

    // levels with a fixed number of zombies to kill (an Endless wave counts like one)
    static bool CountsDown(LevelDefinition level)
    {
        return level.objective == LevelObjective.Eliminate || level.objective == LevelObjective.Endless;
    }

    // siren, then a pack of zombies charges from one direction
    IEnumerator HordeRush()
    {
        HordeActive = true;
        GameAudio.Play(GameAudio.Siren, 0.6f);
        if (Hud != null)
        {
            Hud.ShowBanner("HORDE INCOMING!", "Brace yourself", 1.5f);
            Hud.Shake(6f);
        }

        yield return new WaitForSeconds(2.5f);

        LevelDefinition level = Flow.Level;
        // the pack charges in from out of view, so the siren is the warning and the turn is the scare
        float angle = MainCamera != null
            ? FacingAngle() + (Random.value < 0.5f ? -1f : 1f) * Random.Range(OutOfViewAngle() + 10f, 170f) * Mathf.Deg2Rad
            : Random.Range(0f, Mathf.PI * 2f);
        int count = 4 + Flow.CurrentLevelIndex / 3;
        if (CountsDown(level))
        {
            count = Mathf.Min(count, level.zombieCount - zombiesSpawned);
        }

        for (int i = 0; i < count && Flow.IsPlaying; i++)
        {
            ZombieType type = Random.value < 0.5f ? ZombieType.Runner : ZombieType.Walker;
            SpawnZombie(type, RingPosition(angle + Random.Range(-0.4f, 0.4f)), true);
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(6f);
        HordeActive = false;
    }

    public void OnZombieKilled(ZombieController zombie)
    {
        aliveZombies.Remove(zombie);
        ZombieKilled?.Invoke(zombie);
    }

    // whatever is still standing collapses with the level
    public void CollapseAll()
    {
        foreach (ZombieController zombie in aliveZombies.ToArray())
        {
            if (zombie != null) zombie.Collapse();
        }
        aliveZombies.Clear();
    }

    void CelebrateAll()
    {
        foreach (ZombieController zombie in aliveZombies)
        {
            if (zombie != null) zombie.Celebrate();
        }
    }

    // a retry starts the level over in place: stop the horde and clear the floor
    public void ClearForRetry()
    {
        StopAllCoroutines();
        HordeActive = false;
        pendingSpawns = 0;
        foreach (ZombieController zombie in aliveZombies)
        {
            if (zombie != null) Destroy(zombie.gameObject);
        }
        aliveZombies.Clear();
    }
}
