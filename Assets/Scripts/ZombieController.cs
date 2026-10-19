using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ZombieType { Walker, Runner, Brute, Boss, Spitter, Crawler }

public class ZombieController : MonoBehaviour
{
    // zombies walk on an imaginary floor this far below the AR camera
    public const float CameraHeightAboveFloor = 1.25f;

    const string IdleState = "Idle";
    const string ScreamState = "Scream";
    const string HitReactState = "HitReact";

    static RuntimeAnimatorController mixamoController;
    static bool mixamoLoaded;

    // crawlers need the Mixamo crawl animation
    public static bool HasMixamoAnimations
    {
        get
        {
            if (!mixamoLoaded)
            {
                mixamoController = Resources.Load<RuntimeAnimatorController>("ZombieMixamo");
                mixamoLoaded = true;
            }
            return mixamoController != null;
        }
    }

    // claw or bite, picked per zombie for variety
    string attackState = "Attack";
    const string DieBackState = "DieBack";
    const string DieForwardState = "DieForward";

    // meters per second for a basic walker, other types scale from this
    public float zombieSpeed = 0.6f;
    public float zombieHealth = 100f;
    public float attackRange = 0.9f;
    public float damagePerSecond = 10f;
    public float knockbackDistance = 0.15f;
    // portion of the collider height (from the top) that counts as the head
    public float headZone = 0.15f;
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float corpseLifetime = 2.5f;
    public float riseDuration = 1.2f;

    // audio source for zombie
    public AudioSource zombieAudioSource;

    public ZombieType Type { get; private set; } = ZombieType.Walker;
    public bool IsBoss => Type == ZombieType.Boss;
    public bool IsDead => isDead;
    public float MaxHealth { get; private set; } = 100f;
    public int ScoreValue { get; private set; } = 100;
    public string DisplayName { get; private set; } = "Walker";
    public bool IsEnraged { get; private set; }
    // rare glowing variant: tougher, worth more, and a Zombie-dex prize
    public bool IsMutant { get; private set; }
    public bool IsBeingCaptured { get; private set; }
    public float HealthFraction => MaxHealth > 0f ? Mathf.Clamp01(zombieHealth / MaxHealth) : 0f;
    float rageUntil;

    Camera mainCamera;
    Animator animator;
    Renderer[] renderers;
    Transform[] bones;
    Transform head;
    bool headless;
    float staggerUntil;
    // degrees the body leans back after a hit, easing back to upright
    float flinch;
    Quaternion modelRestRotation = Quaternion.identity;
    int woundCount;
    const int MaxWounds = 6;
    MaterialPropertyBlock propertyBlock;
    BoxCollider bodyCollider;
    string moveState = "Z_Run_InPlace";
    string currentAnimState;
    float baseAnimSpeed = 1f;
    Color? baseTint;
    bool isDead;
    bool isRising;
    bool isCelebrating;
    float nextSummonTime;
    float nextGroanTime;

    // approach: each zombie curves in from its own side instead of walking a straight line
    float flankAngle;
    float weavePhase;
    // attack: a short roar and wind-up before the first claw lands
    float attackReadyTime = -1f;
    // spitter: keeps its distance and lobs acid
    float nextSpitTime;
    const float SpitterRange = 3.6f;

    AudioSource stepSource;
    float nextStepTime;
    float stepInterval = 0.55f;
    float stepPitch = 1f;

    void Awake()
    {
        mainCamera = Camera.main;
        bodyCollider = GetComponent<BoxCollider>();
        propertyBlock = new MaterialPropertyBlock();
        if (zombieAudioSource == null) zombieAudioSource = GetComponent<AudioSource>();
        // positional groans: the player can hear which side a zombie is on
        if (zombieAudioSource != null) GameAudio.Make3D(zombieAudioSource);

        stepSource = gameObject.AddComponent<AudioSource>();
        stepSource.playOnAwake = false;
        GameAudio.Make3D(stepSource);
        stepSource.maxDistance = 10f;

        flankAngle = Random.Range(25f, 50f) * (Random.value < 0.5f ? -1f : 1f);
        attackState = Random.value < 0.5f ? "Attack" : "Bite";
        weavePhase = Random.value * Mathf.PI * 2f;
        CacheModel();
    }

    void CacheModel()
    {
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
        bones = skin != null ? skin.bones : null;
        head = FindHead();
        if (animator != null) modelRestRotation = animator.transform.localRotation;
    }

    Transform FindHead()
    {
        if (animator != null && animator.isHuman)
        {
            Transform humanHead = animator.GetBoneTransform(HumanBodyBones.Head);
            if (humanHead != null) return humanHead;
        }
        if (bones == null) return null;
        foreach (Transform bone in bones)
        {
            if (bone != null && bone.name.ToLower().Contains("head") && !bone.name.ToLower().Contains("end")) return bone;
        }
        return null;
    }

    // headshot kill: the head is gone, left to the blood burst
    public void Decapitate()
    {
        if (head == null || headless) return;
        headless = true;
        GameFX.HeadBurst(head.position, -transform.forward);
        head.localScale = Vector3.zero;
    }

    void LateUpdate()
    {
        // animation clips can carry scale keys; keep the head gone
        if (headless && head != null) head.localScale = Vector3.zero;

        // lean back after a hit, on top of the model's own rest rotation
        if (flinch > 0f && animator != null)
        {
            flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 60f);
            animator.transform.localRotation = modelRestRotation * Quaternion.Euler(-flinch, 0f, 0f);
        }
    }

    // leave a blood mark on the body part that was hit
    public void AddWound(Vector3 point, Vector3 shotDirection)
    {
        if (woundCount >= MaxWounds) return;
        woundCount++;
        GameFX.Wound(NearestBone(point), point, shotDirection);
    }

    Transform NearestBone(Vector3 point)
    {
        Transform nearest = transform;
        if (bones == null) return nearest;
        float best = float.MaxValue;
        foreach (Transform bone in bones)
        {
            if (bone == null) continue;
            float distance = (bone.position - point).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = bone;
            }
        }
        return nearest;
    }

    // set up the zombie's stats and look; call right after spawning
    public void Configure(ZombieType type, float speedMultiplier, GameObject modelPrefab, bool finalBoss)
    {
        Type = type;
        float scale = 1f;
        float animSpeed = 1f;
        float speedFactor = 1f;

        switch (type)
        {
            case ZombieType.Walker:
                DisplayName = "Walker";
                moveState = "Z_Walk_InPlace";
                speedFactor = 0.85f;
                MaxHealth = 100f;
                damagePerSecond = 10f;
                ScoreValue = 100;
                break;
            case ZombieType.Runner:
                DisplayName = "Runner";
                moveState = "Z_Run_InPlace";
                speedFactor = 2.1f;
                animSpeed = 1.25f;
                MaxHealth = 60f;
                damagePerSecond = 8f;
                knockbackDistance = 0.25f;
                ScoreValue = 150;
                stepInterval = 0.3f;
                stepPitch = 1.15f;
                // pale, bloodless skin sets runners apart from walkers
                baseTint = new Color(0.82f, 0.86f, 0.8f, 1f);
                break;
            case ZombieType.Brute:
                DisplayName = "Brute";
                moveState = "Z_Walk1_InPlace 0";
                scale = 1.3f;
                speedFactor = 0.75f;
                animSpeed = 0.8f;
                MaxHealth = 350f;
                damagePerSecond = 22f;
                knockbackDistance = 0.03f;
                attackRange = 1.1f;
                ScoreValue = 300;
                // bruised, darker flesh for the heavy brutes
                baseTint = new Color(0.72f, 0.62f, 0.58f, 1f);
                stepInterval = 0.75f;
                stepPitch = 0.7f;
                break;
            case ZombieType.Crawler:
                DisplayName = "Crawler";
                moveState = "Crawl";
                speedFactor = 1.4f;
                MaxHealth = 70f;
                damagePerSecond = 9f;
                attackRange = 0.8f;
                ScoreValue = 175;
                stepInterval = 0.25f;
                stepPitch = 1.25f;
                baseTint = new Color(0.78f, 0.74f, 0.72f, 1f);
                // low to the ground: shrink the hit box so shots have to aim down
                if (bodyCollider != null)
                {
                    bodyCollider.center = new Vector3(bodyCollider.center.x, bodyCollider.center.y * 0.4f, bodyCollider.center.z + 0.2f);
                    bodyCollider.size = new Vector3(bodyCollider.size.x, bodyCollider.size.y * 0.4f, bodyCollider.size.z * 1.8f);
                }
                break;
            case ZombieType.Spitter:
                DisplayName = "Spitter";
                moveState = "Z_Walk_InPlace";
                speedFactor = 0.9f;
                MaxHealth = 80f;
                damagePerSecond = 6f;
                ScoreValue = 200;
                // sickly green skin; it hangs back and lobs acid instead of rushing in
                baseTint = new Color(0.62f, 0.95f, 0.5f, 1f);
                nextSpitTime = Time.time + Random.Range(2.5f, 4f);
                break;
            case ZombieType.Boss:
                DisplayName = finalBoss ? "THE ABOMINATION" : "THE BRUTE KING";
                moveState = "Z_Walk1_InPlace 0";
                scale = finalBoss ? 2.3f : 1.8f;
                speedFactor = 0.6f;
                animSpeed = 0.65f;
                MaxHealth = finalBoss ? 1800f : 900f;
                damagePerSecond = finalBoss ? 35f : 25f;
                knockbackDistance = 0f;
                attackRange = finalBoss ? 1.6f : 1.35f;
                ScoreValue = finalBoss ? 5000 : 2000;
                riseDuration = 2.5f;
                baseTint = finalBoss ? new Color(0.75f, 0.35f, 0.35f, 1f) : new Color(0.6f, 0.8f, 0.55f, 1f);
                nextSummonTime = Time.time + 10f;
                stepInterval = 0.9f;
                stepPitch = 0.55f;
                break;
        }

        if (modelPrefab != null) SwapModel(modelPrefab);

        // the realistic models have proper skin textures; only hint at the type's colour on top
        if (baseTint.HasValue && realisticModels != null && System.Array.IndexOf(realisticModels, modelPrefab) >= 0)
        {
            baseTint = Color.Lerp(Color.white, baseTint.Value, 0.35f);
        }

        zombieSpeed *= speedFactor * speedMultiplier;
        zombieHealth = MaxHealth;
        transform.localScale = Vector3.one * scale;
        baseAnimSpeed = animSpeed;
        if (animator != null) animator.speed = animSpeed;
        SetTint(baseTint);
    }

    // realistic Mixamo characters in Resources/ZombieModels, when they've been added
    static GameObject[] realisticModels;

    public static GameObject RealisticModelFor(ZombieType type)
    {
        if (realisticModels == null) realisticModels = Resources.LoadAll<GameObject>("ZombieModels");
        if (realisticModels.Length == 0) return null;

        GameObject warzombie = System.Array.Find(realisticModels, m => m.name.ToLower().Contains("warzombie"));
        GameObject girl = System.Array.Find(realisticModels, m => m.name.ToLower().Contains("girl"));
        if (warzombie == null) warzombie = realisticModels[0];
        if (girl == null) girl = realisticModels[realisticModels.Length - 1];

        switch (type)
        {
            case ZombieType.Runner:
            case ZombieType.Spitter:
                return girl;
            case ZombieType.Walker:
                return Random.value < 0.5f ? warzombie : girl;
            default:
                return warzombie;
        }
    }

    // replace the default model with another rigged zombie, keeping the animator setup
    void SwapModel(GameObject modelPrefab)
    {
        if (animator == null) return;

        Transform oldModel = animator.transform;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        Bounds oldBounds = ModelBounds(oldModel.gameObject);

        GameObject newModel = Instantiate(modelPrefab, oldModel.parent);
        newModel.transform.localPosition = oldModel.localPosition;
        newModel.transform.localRotation = oldModel.localRotation;

        // models come in different sizes and pivots: match the old one's height, then its feet
        Bounds newBounds = ModelBounds(newModel);
        if (oldBounds.size.y > 0.01f && newBounds.size.y > 0.01f)
        {
            newModel.transform.localScale *= oldBounds.size.y / newBounds.size.y;
            newBounds = ModelBounds(newModel);
            newModel.transform.position += Vector3.up * (oldBounds.min.y - newBounds.min.y);
        }

        // Destroy is deferred, so detach and hide the old model before re-caching
        oldModel.gameObject.SetActive(false);
        oldModel.SetParent(null);
        Destroy(oldModel.gameObject);

        CacheModel();
        if (animator != null && controller != null) animator.runtimeAnimatorController = controller;
    }

    static Bounds ModelBounds(GameObject model)
    {
        Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>();
        if (modelRenderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);
        Bounds bounds = modelRenderers[0].bounds;
        foreach (Renderer r in modelRenderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    void Start()
    {
        if (animator != null)
        {
            // the Mixamo animation set replaces the pack's own clips when it has been built
            if (HasMixamoAnimations && animator.isHuman) animator.runtimeAnimatorController = mixamoController;

            // movement is driven from code, root motion would make zombies drift
            animator.applyRootMotion = false;
            // desync the walk cycles so a group of zombies doesn't move in lockstep
            int hash = Animator.StringToHash(moveState);
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0, Random.value);
                currentAnimState = moveState;
            }
        }

        if (IsDormant)
        {
            // a hunted zombie is already standing there, idling with its back half-turned
            transform.position = FloorPosition(transform.position);
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            PlayAnim(IdleState);
            if (animator != null) animator.speed = 0.6f;
            return;
        }
        StartCoroutine(RiseFromGround());
    }

    // ---------- Hunt ----------

    // hiding and idle until found; set before Start
    public bool IsDormant { get; private set; }

    public void MakeDormant()
    {
        IsDormant = true;
    }

    // found: it turns, screams and comes for the player, briefly enraged
    public void Wake()
    {
        if (!IsDormant || isDead) return;
        IsDormant = false;
        if (animator != null) animator.speed = baseAnimSpeed;
        rageUntil = Time.time + 2.5f;
        StartCoroutine(WakeScream());
    }

    IEnumerator WakeScream()
    {
        isRising = true;
        if (animator != null && animator.HasState(0, Animator.StringToHash(ScreamState))) PlayAnim(ScreamState);
        Groan(Random.Range(0.75f, 0.9f));
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            if (isDead) yield break;
            FaceCamera();
            yield return null;
        }
        isRising = false;
    }

    // claw up out of the floor instead of popping into existence
    IEnumerator RiseFromGround()
    {
        isRising = true;
        Vector3 end = FloorPosition(transform.position);
        Vector3 start = end + Vector3.down * (1.9f * transform.localScale.y);
        GameFX.Dust(end, IsBoss ? 3f : transform.localScale.y);

        if (IsBoss)
        {
            Groan(0.45f);
            GameAudio.Play(GameAudio.Impact, 1f, 0.8f);
        }
        else if (Random.value < 0.6f)
        {
            Groan(Random.Range(0.85f, 1.15f));
        }

        for (float t = 0f; t < riseDuration; t += Time.deltaTime)
        {
            if (isDead) yield break;
            float eased = 1f - Mathf.Pow(1f - t / riseDuration, 3f);
            transform.position = Vector3.Lerp(start, end, eased);
            FaceCamera();
            yield return null;
        }
        transform.position = end;

        // bosses always, and some others, scream at the player before coming
        bool scream = IsBoss || (Type != ZombieType.Runner && Type != ZombieType.Crawler && Random.value < 0.3f);
        if (scream && animator != null && animator.HasState(0, Animator.StringToHash(ScreamState)))
        {
            PlayAnim(ScreamState);
            Groan(IsBoss ? 0.4f : Random.Range(0.75f, 0.9f));
            for (float t = 0f; t < (IsBoss ? 2f : 1.4f); t += Time.deltaTime)
            {
                if (isDead) yield break;
                FaceCamera();
                yield return null;
            }
        }
        isRising = false;
    }

    // ---------- Mutants and capture ----------

    public void MakeMutant()
    {
        if (IsBoss || IsMutant) return;
        IsMutant = true;
        DisplayName = "Mutant " + DisplayName;
        MaxHealth *= 1.5f;
        zombieHealth = MaxHealth;
        ScoreValue *= 2;
        baseTint = new Color(0.82f, 0.6f, 1f, 1f);
        SetTint(baseTint);
        // a violet shimmer so mutants stand out across the room
        ParticleSystem glow = GameFX.Aura(transform, ZombieDex.MutantColor);
        glow.transform.localPosition = Vector3.up * 1.1f;
        ParticleSystem.ShapeModule shape = glow.shape;
        shape.radius = 0.35f;
    }

    // pinned by a trap: stop moving and attacking until the trap decides
    public void BeginCapture()
    {
        IsBeingCaptured = true;
        attackReadyTime = -1f;
        currentAnimState = null;
        PlayAnim(HitReactState);
        SetTint(new Color(0.55f, 0.95f, 1f, 1f));
    }

    // broke out: furious for a few seconds
    public void BreakFree()
    {
        IsBeingCaptured = false;
        rageUntil = Time.time + 3f;
        SetTint(baseTint);
        Groan(Random.Range(1.1f, 1.3f));
    }

    // caught: shrink into the trap and vanish
    public void CompleteCapture(Vector3 trapPosition)
    {
        if (isDead) return;
        isDead = true;
        IsBeingCaptured = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (zombieAudioSource != null) zombieAudioSource.Stop();
        StartCoroutine(ShrinkInto(trapPosition));
    }

    IEnumerator ShrinkInto(Vector3 point)
    {
        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.position;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k * k);
            transform.position = Vector3.Lerp(startPosition, point, k);
            yield return null;
        }
        Destroy(gameObject);
    }

    void Update()
    {
        if (isDead || isRising || IsBeingCaptured || IsDormant) return;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        GameControllerScript game = GameControllerScript.Instance;
        if (isCelebrating || (game != null && !game.IsPlaying))
        {
            PlayAnim(IdleState);
            return;
        }

        float slow = game != null ? game.ZombieTimeScale : 1f;
        if (animator != null) animator.speed = baseAnimSpeed * slow * (IsEnraged ? 1.4f : 1f);

        if (IsBoss) UpdateBoss(game);
        MoveZombieTowardsCamera(slow);

        // random groans keep the player on edge
        if (Time.time >= nextGroanTime)
        {
            nextGroanTime = Time.time + Random.Range(4f, 9f);
            if (Random.value < 0.5f) Groan(IsBoss ? 0.5f : Random.Range(0.85f, 1.15f));
        }
    }

    void UpdateBoss(GameControllerScript game)
    {
        if (game == null) return;

        if (Time.time >= nextSummonTime)
        {
            nextSummonTime = Time.time + (IsEnraged ? 8f : 12f);
            Groan(0.45f);
            game.SummonMinions(transform.position, IsEnraged ? 3 : 2);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // legacy physical bullets; the game controller normally uses hitscan
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject);
            TakeDamage(50f, -transform.forward);
        }
    }

    // move the zombie towards the camera until it is in attack range, then hurt the player
    public void MoveZombieTowardsCamera(float timeScale = 1f)
    {
        Vector3 target = FloorPosition(mainCamera.transform.position);
        Vector3 position = transform.position;
        position.y = target.y;

        Vector3 flatOffset = target - position;
        float distance = flatOffset.magnitude;

        // spitters stop at range and lob acid instead of closing in
        if (Type == ZombieType.Spitter && distance <= SpitterRange && distance > attackRange)
        {
            PlayAnim(IdleState);
            if (Time.time >= nextSpitTime && Time.time >= staggerUntil) StartCoroutine(SpitAcid());
            transform.position = position;
            FaceCamera();
            return;
        }

        if (distance <= attackRange)
        {
            PlayAnim(attackState);

            // a roar and a beat before the first claw lands, so the player can react
            if (attackReadyTime < 0f)
            {
                attackReadyTime = Time.time + (IsBoss ? 0.35f : 0.5f);
                Groan(IsBoss ? 0.45f : Random.Range(1.1f, 1.25f));
            }

            if (Time.time >= attackReadyTime && GameControllerScript.Instance != null)
            {
                GameControllerScript.Instance.DamagePlayer(damagePerSecond * timeScale * Time.deltaTime);
            }

            if (zombieAudioSource != null && !zombieAudioSource.isPlaying)
            {
                Groan(IsBoss ? 0.5f : Random.Range(0.9f, 1.1f));
            }
        }
        else
        {
            attackReadyTime = -1f;
            if (Time.time >= staggerUntil)
            {
                PlayAnim(moveState);
                bool raging = IsEnraged || Time.time < rageUntil;
                float speed = zombieSpeed * timeScale * (raging ? 1.6f : 1f);
                position += ApproachDirection(flatOffset, distance) * speed * Time.deltaTime;
                UpdateFootsteps(timeScale);
            }
        }

        transform.position = position;
        FaceCamera();
    }

    // curve in from the side while far away, straighten out for the final lunge; runners weave
    Vector3 ApproachDirection(Vector3 toPlayer, float distance)
    {
        Vector3 direct = toPlayer / Mathf.Max(distance, 0.001f);
        if (IsBoss) return direct;

        float sideways = flankAngle * Mathf.Clamp01((distance - 1.5f) / 3f);
        if (Type == ZombieType.Runner) sideways += Mathf.Sin(Time.time * 3f + weavePhase) * 25f;
        return Quaternion.Euler(0f, sideways, 0f) * direct;
    }

    void UpdateFootsteps(float timeScale)
    {
        if (Time.time < nextStepTime) return;
        nextStepTime = Time.time + stepInterval / Mathf.Max(timeScale, 0.2f) * (IsEnraged ? 0.7f : 1f);
        stepSource.pitch = stepPitch * Random.Range(0.9f, 1.1f);
        stepSource.PlayOneShot(GameAudio.Footstep, Type == ZombieType.Runner ? 0.45f : 0.6f);
    }

    // rear back, then throw a glob of acid at where the player is standing now; they dodge by moving
    IEnumerator SpitAcid()
    {
        nextSpitTime = Time.time + Random.Range(3f, 4.5f);
        PlayAnim("Attack");
        Groan(1.4f);
        yield return new WaitForSeconds(0.45f);
        if (isDead || mainCamera == null) yield break;

        Vector3 mouth = transform.position + Vector3.up * 1.5f * transform.localScale.y + transform.forward * 0.3f;
        AcidGlob.Launch(mouth, mainCamera.transform.position);
        GameAudio.PlayAt(GameAudio.Spit, mouth, 0.9f);
    }

    void FaceCamera()
    {
        if (mainCamera == null) return;
        Vector3 flat = mainCamera.transform.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
    }

    static Vector3 FloorPosition(Vector3 position)
    {
        Camera camera = Camera.main;
        // the real detected floor when there is one, otherwise a guess below the phone
        float floor = ARWorld.Instance != null ? ARWorld.FloorY
            : camera != null ? camera.transform.position.y - CameraHeightAboveFloor : position.y;
        return new Vector3(position.x, floor, position.z);
    }

    public bool IsHeadshot(Vector3 worldPoint)
    {
        if (bodyCollider == null) return false;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float top = bodyCollider.center.y + bodyCollider.size.y * 0.5f;
        return local.y >= top - bodyCollider.size.y * headZone;
    }

    // returns true when this hit killed the zombie
    public bool TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead) return false;

        zombieHealth -= damage;
        StopCoroutine(nameof(HitFlash));
        StartCoroutine(nameof(HitFlash));

        if (zombieHealth <= 0f)
        {
            Die(hitDirection);
            return true;
        }

        // a hidden zombie that gets shot is very much awake
        if (IsDormant) Wake();

        if (IsBoss && !IsEnraged && zombieHealth <= MaxHealth * 0.5f)
        {
            Enrage();
        }

        Vector3 push = hitDirection;
        push.y = 0f;
        transform.position += push.normalized * knockbackDistance;

        // stagger: small zombies stop and rock back, the big ones barely notice
        float stagger = IsBoss ? 0.04f : Type == ZombieType.Brute ? 0.1f : 0.35f;
        staggerUntil = Mathf.Max(staggerUntil, Time.time + stagger);
        flinch = Mathf.Max(flinch, IsBoss ? 3f : Type == ZombieType.Brute ? 6f : 10f);
        // a real hit reaction for the smaller types (crawlers are already on the floor)
        if (!IsBoss && Type != ZombieType.Brute && Type != ZombieType.Crawler)
        {
            currentAnimState = null;
            PlayAnim(HitReactState);
        }
        return false;
    }

    void Enrage()
    {
        IsEnraged = true;
        baseTint = new Color(1f, 0.25f, 0.2f, 1f);
        SetTint(baseTint);
        Groan(0.4f);
        GameAudio.Play(GameAudio.Impact, 1f, 1.2f);
        if (GameControllerScript.Instance != null) GameControllerScript.Instance.OnBossEnraged(this);
    }

    // stop attacking and idle when the player has lost
    public void Celebrate()
    {
        isCelebrating = true;
    }

    // killed by a level event rather than by the player
    public void Collapse()
    {
        if (isDead) return;
        isDead = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        PlayAnim(Random.value < 0.5f ? DieBackState : DieForwardState);
        StartCoroutine(SinkAndDestroy());
    }

    void Die(Vector3 hitDirection)
    {
        isDead = true;

        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (zombieAudioSource != null) zombieAudioSource.Stop();
        if (animator != null) animator.speed = 1f;

        // fall away from the shot when hit from the front
        bool hitFromFront = Vector3.Dot(transform.forward, hitDirection) < 0f;
        PlayAnim(hitFromFront ? DieBackState : DieForwardState);

        if (IsBoss) GameFX.Dust(transform.position, 3f);
        GameFX.BloodPool(transform.position, transform.localScale.x);

        if (GameControllerScript.Instance != null)
        {
            GameControllerScript.Instance.OnZombieKilled(this);
        }

        StartCoroutine(SinkAndDestroy());
    }

    void Groan(float pitch)
    {
        if (zombieAudioSource == null || zombieAudioSource.clip == null) return;
        zombieAudioSource.pitch = pitch;
        zombieAudioSource.Play();
    }

    void PlayAnim(string state)
    {
        if (animator == null || currentAnimState == state) return;

        int hash = Animator.StringToHash(state);
        // the original animator only has a claw attack
        if (!animator.HasState(0, hash) && state == "Bite") hash = Animator.StringToHash("Attack");
        if (!animator.HasState(0, hash)) return;

        animator.CrossFadeInFixedTime(hash, 0.2f);
        currentAnimState = state;
    }

    IEnumerator HitFlash()
    {
        SetTint(hitFlashColor);
        yield return new WaitForSeconds(0.1f);
        SetTint(baseTint);
    }

    void SetTint(Color? color)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            propertyBlock.Clear();
            if (color.HasValue)
            {
                propertyBlock.SetColor("_BaseColor", color.Value);
                propertyBlock.SetColor("_Color", color.Value);
            }
            r.SetPropertyBlock(propertyBlock);
        }
    }

    IEnumerator SinkAndDestroy()
    {
        yield return new WaitForSeconds(corpseLifetime);

        // sink into the floor instead of popping out of existence
        Vector3 start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            transform.position = start + Vector3.down * (t * 1.5f * transform.localScale.y);
            yield return null;
        }
        Destroy(gameObject);
    }
}
