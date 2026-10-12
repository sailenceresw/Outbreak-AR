using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ZombieType { Walker, Runner, Brute, Boss }

public class ZombieController : MonoBehaviour
{
    // zombies walk on an imaginary floor this far below the AR camera
    public const float CameraHeightAboveFloor = 1.25f;

    const string AttackState = "Attack";
    const string IdleState = "Idle";
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

    Camera mainCamera;
    Animator animator;
    Renderer[] renderers;
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

    void Awake()
    {
        mainCamera = Camera.main;
        bodyCollider = GetComponent<BoxCollider>();
        propertyBlock = new MaterialPropertyBlock();
        if (zombieAudioSource == null) zombieAudioSource = GetComponent<AudioSource>();
        CacheModel();
    }

    void CacheModel()
    {
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
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
                break;
        }

        if (modelPrefab != null) SwapModel(modelPrefab);

        zombieSpeed *= speedFactor * speedMultiplier;
        zombieHealth = MaxHealth;
        transform.localScale = Vector3.one * scale;
        baseAnimSpeed = animSpeed;
        if (animator != null) animator.speed = animSpeed;
        SetTint(baseTint);
    }

    // replace the default model with another rigged zombie, keeping the animator setup
    void SwapModel(GameObject modelPrefab)
    {
        if (animator == null) return;

        Transform oldModel = animator.transform;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;

        GameObject newModel = Instantiate(modelPrefab, oldModel.parent);
        newModel.transform.localPosition = oldModel.localPosition;
        newModel.transform.localRotation = oldModel.localRotation;
        newModel.transform.localScale = oldModel.localScale;

        // Destroy is deferred, so detach and hide the old model before re-caching
        oldModel.gameObject.SetActive(false);
        oldModel.SetParent(null);
        Destroy(oldModel.gameObject);

        CacheModel();
        if (animator != null && controller != null) animator.runtimeAnimatorController = controller;
    }

    void Start()
    {
        if (animator != null)
        {
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

        StartCoroutine(RiseFromGround());
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
        isRising = false;
    }

    void Update()
    {
        if (isDead || isRising) return;
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
        if (flatOffset.magnitude <= attackRange)
        {
            PlayAnim(AttackState);

            if (GameControllerScript.Instance != null)
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
            PlayAnim(moveState);
            float speed = zombieSpeed * timeScale * (IsEnraged ? 1.6f : 1f);
            position = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
        }

        transform.position = position;
        FaceCamera();
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
        float floor = camera != null ? camera.transform.position.y - CameraHeightAboveFloor : position.y;
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

        if (IsBoss && !IsEnraged && zombieHealth <= MaxHealth * 0.5f)
        {
            Enrage();
        }

        Vector3 push = hitDirection;
        push.y = 0f;
        transform.position += push.normalized * knockbackDistance;
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
