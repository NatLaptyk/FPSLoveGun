using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Final Boss — a ground-based humanoid who spreads sadness and commands the crowd.
/// Defeated by love projectiles; bursts into NPCs on death.
///
/// ANIMATOR PARAMETERS (set these up in the Animator Controller):
///   Speed       float   — 0 = idle, > 0 = moving (drives walk/run blend)
///   InCombat    bool    — true while the boss has spotted the player
///   Attack1     trigger — quick attack  (attack00)
///   Attack2     trigger — heavy attack  (attack01)
///   Attack3     trigger — sadness pulse (attack02)
///   Hit         trigger — hit reaction  (hit00 / hit01, randomised by script)
///   Stun        trigger — fall and rise (fall&riseup00)
///   Die         trigger — death         (dle00)
///
/// SETUP:
///   1. Add NavMeshAgent, this script, a Collider, and an Animator to the boss.
///   2. Bake a NavMesh that covers the field area.
///   3. Assign all Inspector references.
///   4. Fill savedNPCsToEject with the NPC prefabs that burst out on defeat.
///   5. Activate the boss GameObject from Section2Spawner.onAllWavesComplete.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class FinalBossAI : MonoBehaviour, ILovable<bool>
{
    // ── State machine ─────────────────────────────────────────────────────────
    public enum BossState { Idle, Chasing, Attacking, Jumping, Stunned, Defeated }
    public BossState CurrentState { get; private set; } = BossState.Idle;
    public int CurrentLove { get; private set; }

    // ── Stats ─────────────────────────────────────────────────────────────────
    [Header("Boss Stats")]
    public int loveNeededToDefeat = 15;
    [Tooltip("Multiplier applied to love received while stunned.")]
    public int stunnedLoveMultiplier = 3;
    public float stunDuration = 4f;

    // ── Movement ──────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float walkSpeed  = 2f;
    public float runSpeed   = 8f;    // set higher than player speed so boss can catch up
    public float aggroRange = 20f;
    [Tooltip("Distance at which the boss breaks into a run instead of walking.")]
    public float runThreshold = 4f;
    [Tooltip("Distance at which the boss jumps toward the player to close the gap.")]
    public float jumpTriggerDistance = 10f;
    public float jumpCooldown = 5f;
    [Tooltip("How many units the boss rises at the peak of the jump arc. " +
             "Increase this to clear taller obstacles.")]
    public float jumpArcHeight = 3f;

    // ── Attacks ───────────────────────────────────────────────────────────────
    [Header("Quick Attack (Attack1)")]
    [Tooltip("Melee range for the quick jab.")]
    public float quickAttackRange = 2.5f;
    public int   quickAttackDamage = 10;
    public float quickAttackCooldown = 2f;
    public float quickAttackAnimLength = 0.8f;

    [Header("Heavy Attack (Attack2)")]
    [Tooltip("Melee range for the heavy swing — slightly wider than quick.")]
    public float heavyAttackRange = 3.5f;
    public int   heavyAttackDamage = 25;
    public float heavyAttackCooldown = 4f;
    public float heavyAttackAnimLength = 1.2f;

    [Header("Sadness Pulse (Attack3)")]
    [Tooltip("Radius of the AoE sadness pulse centred on the boss.")]
    public float pulseRadius = 8f;
    public int   pulseDamage = 15;
    public float pulseCooldown = 6f;
    public float pulseAnimLength = 1.5f;
    [Tooltip("Optional particle/effect spawned at the boss position during the pulse.")]
    public GameObject pulseEffectPrefab;

    [Header("Ring Shot (Attack4)")]
    [Tooltip("Number of projectiles fired evenly spread around the full 360°.")]
    public int   ringProjectileCount = 12;
    [Tooltip("Projectile prefab for the ring shot. Reuses bossProjectilePrefab if left empty.")]
    public GameObject ringProjectilePrefab;
    [Tooltip("Fallback single-shot projectile prefab (used by ring shot if ringProjectilePrefab is empty).")]
    public GameObject bossProjectilePrefab;
    [Tooltip("How fast each ring projectile travels.")]
    public float ringProjectileForce = 14f;
    [Tooltip("Distance at which the ring shot triggers (medium range).")]
    public float ringShotRange = 14f;
    public float ringShotCooldown = 7f;
    public float ringShotAnimLength = 1.0f;
    [Tooltip("Radius around the boss body from which each projectile spawns.")]
    public float ringShotSpawnRadius = 0.8f;
    [Tooltip("Y offset from the boss pivot where projectiles spawn (waist / chest height).")]
    public float ringShotHeightOffset = 1.2f;

    // ── Watcher Summons ───────────────────────────────────────────────────────
    [Header("Watcher Summons")]
    [Tooltip("The WatcherAI prefab to spawn during combat.")]
    public GameObject watcherPrefab;

    [Tooltip("Maximum number of Watchers alive at the same time. " +
             "Boss won't summon more until one is defeated.")]
    public int maxWatchersAlive = 2;

    [Tooltip("Seconds between summons (when below the max).")]
    public float watcherSpawnCooldown = 18f;

    [Tooltip("Radius around the boss where Watchers appear.")]
    public float watcherSpawnRadius = 6f;

    [Tooltip("How high above the boss the Watcher spawns (they hover, so start them elevated).")]
    public float watcherSpawnHeight = 4f;

    private float nextWatcherSpawnTime;
    private List<GameObject> activeWatchers = new List<GameObject>();

    // ── NPC ejection ──────────────────────────────────────────────────────────
    [Header("Defeat — Explosion")]
    [Tooltip("Particle / VFX prefab instantiated at the boss position on defeat, " +
             "before NPCs are ejected. Leave empty to skip.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Seconds to wait after the explosion spawns before ejecting NPCs. " +
             "Match this to the peak of your explosion effect.")]
    public float explosionDuration = 1.2f;

    [Tooltip("MinimapMarker (Objective type) for the boss location. " +
             "Hidden automatically when the boss is defeated.")]
    public MinimapMarker bossObjectiveMarker;

    [Header("Defeat — NPC Ejection")]
    [Tooltip("NPC prefabs and counts to burst out of the boss on defeat.")]
    public SavedNPCConfig[] savedNPCsToEject;
    public float ejectForce = 400f;

    // ── References ────────────────────────────────────────────────────────────
    [Header("References")]
    public Transform player;
    [Tooltip("Assign a SectionTracker — the ejected NPCs are auto-registered with it " +
             "so the section completes once they are all converted.")]
    public SectionTracker bossPhaseTracker;

    // ── Music ─────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("MusicController for the boss fight track. " +
             "Played the moment the boss first aggros onto the player.")]
    public MusicController bossFightMusic;

    // ── Events ────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fires the moment the boss is defeated (before the die animation finishes). " +
             "Wire to music, HUD message, etc.")]
    public UnityEngine.Events.UnityEvent onDefeated;

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioClip attackSound;
    public AudioClip pulseSound;
    public AudioClip hitSound;
    public AudioClip defeatSound;

    [Range(0f, 1f)]
    [Tooltip("Master volume for all boss sound effects.")]
    public float sfxVolume = 1f;

    // ── Privates ──────────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Animator     animator;
    private Collider     bossCollider;
    private AudioSource  audioSource;

    private bool        isAggroed     = false;   // once true, boss never returns to Idle
    private bool        musicStarted  = false;   // guard against double-play
    private GameObject  activePulseEffect;       // tracked so it can be destroyed after the attack
    private float nextQuickAttackTime;
    private float nextHeavyAttackTime;
    private float nextPulseTime;
    private float nextRingShotTime;
    private float nextJumpTime;
    private float stateEndTime;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        agent        = GetComponent<NavMeshAgent>();
        animator     = GetComponent<Animator>();
        bossCollider = GetComponent<Collider>();
        audioSource  = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        agent.speed = walkSpeed;

        // Prevent the music from auto-starting if its GO is a child of this prefab.
        // We'll enable it manually the moment the boss first aggros the player.
        if (bossFightMusic != null)
            bossFightMusic.gameObject.SetActive(false);

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (CurrentState == BossState.Defeated || player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Once aggroed, skip Idle and go straight to Chasing
        if (isAggroed && CurrentState == BossState.Idle)
            CurrentState = BossState.Chasing;

        switch (CurrentState)
        {
            case BossState.Idle:      HandleIdle(distToPlayer);      break;
            case BossState.Chasing:   HandleChasing(distToPlayer);   break;
            case BossState.Attacking: HandleAttacking();              break;
            case BossState.Jumping:   /* driven by JumpDash coroutine */ break;
            case BossState.Stunned:   HandleStunned();                break;
        }

        // Watcher summon — runs independently of the attack state machine
        if (isAggroed) TrySummonWatcher();
    }

    // ── State handlers ────────────────────────────────────────────────────────

    void HandleIdle(float distToPlayer)
    {
        agent.isStopped = true;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            animator.SetBool("InCombat", false);
        }

        if (distToPlayer <= aggroRange)
        {
            isAggroed = true;
            CurrentState = BossState.Chasing;
            agent.isStopped = false;

            if (bossFightMusic != null && !musicStarted)
            {
                musicStarted = true;
                bossFightMusic.gameObject.SetActive(true); // triggers OnEnable → StartPlayback
            }
            Debug.Log("[FinalBoss] Player spotted — now permanently aggroed.");
        }
    }

    void HandleChasing(float distToPlayer)
    {
        if (animator != null) animator.SetBool("InCombat", true);

        // Priority order: pulse > ring shot > heavy > quick > jump > walk/run
        if (Time.time >= nextPulseTime && distToPlayer <= pulseRadius)
        {
            StartAttack(BossAttackType.Pulse);
            return;
        }
        if (Time.time >= nextRingShotTime && distToPlayer <= ringShotRange)
        {
            StartAttack(BossAttackType.RingShot);
            return;
        }
        if (Time.time >= nextHeavyAttackTime && distToPlayer <= heavyAttackRange)
        {
            StartAttack(BossAttackType.Heavy);
            return;
        }
        if (Time.time >= nextQuickAttackTime && distToPlayer <= quickAttackRange)
        {
            StartAttack(BossAttackType.Quick);
            return;
        }

        // Jump to close a large gap — speed burst, not a warp
        if (Time.time >= nextJumpTime && distToPlayer >= jumpTriggerDistance)
        {
            StartCoroutine(JumpDash());
            return;
        }

        // Move toward player
        agent.isStopped = false;
        agent.SetDestination(player.position);

        bool shouldRun = distToPlayer > runThreshold;
        agent.speed = shouldRun ? runSpeed : walkSpeed;

        // IsRunning bool is simpler to wire in the Animator than a Speed threshold
        if (animator != null)
        {
            animator.SetBool("IsRunning", shouldRun);
            animator.SetFloat("Speed", agent.speed);
        }
    }

    void HandleAttacking()
    {
        agent.isStopped = true;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
        }
        FacePlayer();

        if (Time.time >= stateEndTime)
            CurrentState = BossState.Chasing;
    }

    void HandleStunned()
    {
        agent.isStopped = true;
        if (animator != null) animator.SetFloat("Speed", 0f);

        if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
            Debug.Log("[FinalBoss] Stun over — chasing again.");
        }
    }

    // ── Attack logic ──────────────────────────────────────────────────────────

    enum BossAttackType { Quick, Heavy, Pulse, RingShot }

    void StartAttack(BossAttackType type)
    {
        CurrentState = BossState.Attacking;
        agent.isStopped = true;
        FacePlayer();

        switch (type)
        {
            case BossAttackType.Quick:
                if (animator != null) animator.SetTrigger("Attack1");
                DealMeleeDamage(quickAttackDamage, quickAttackRange);
                stateEndTime = Time.time + quickAttackAnimLength;
                nextQuickAttackTime = Time.time + quickAttackCooldown;
                PlaySound(attackSound);
                break;

            case BossAttackType.Heavy:
                if (animator != null) animator.SetTrigger("Attack2");
                DealMeleeDamage(heavyAttackDamage, heavyAttackRange);
                stateEndTime = Time.time + heavyAttackAnimLength;
                nextHeavyAttackTime = Time.time + heavyAttackCooldown;
                PlaySound(attackSound);
                break;

            case BossAttackType.Pulse:
                if (animator != null) animator.SetTrigger("Attack3");
                StartCoroutine(DelaySadnessPulse(pulseAnimLength * 0.5f));
                stateEndTime  = Time.time + pulseAnimLength;
                nextPulseTime = Time.time + pulseCooldown;
                PlaySound(pulseSound);
                break;

            case BossAttackType.RingShot:
                if (animator != null) animator.SetTrigger("Attack3"); // reuse pulse anim or add Attack4
                StartCoroutine(DelayRingShot(ringShotAnimLength * 0.4f));
                stateEndTime      = Time.time + ringShotAnimLength;
                nextRingShotTime  = Time.time + ringShotCooldown;
                PlaySound(attackSound);
                break;
        }
    }

    void DealMeleeDamage(int damage, float range)
    {
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= range)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeSadness(damage);
            Debug.Log($"[FinalBoss] Melee hit — {damage} sadness.");
        }
    }

    IEnumerator DelaySadnessPulse(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Damage the player if in range
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= pulseRadius)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeSadness(pulseDamage);
        }

        // Spawn visual effect — destroy any leftover from a previous pulse first,
        // then schedule destruction after the attack animation finishes.
        if (pulseEffectPrefab != null)
        {
            if (activePulseEffect != null) Destroy(activePulseEffect);
            activePulseEffect = Instantiate(pulseEffectPrefab, transform.position, Quaternion.identity);
            Destroy(activePulseEffect, pulseAnimLength);
        }

        Debug.Log($"[FinalBoss] Sadness pulse — radius {pulseRadius}.");
    }

    IEnumerator DelayRingShot(float delay)
    {
        yield return new WaitForSeconds(delay);
        FireRingShot();
    }

    void FireRingShot()
    {
        // Pick the prefab — fall back to the single-shot projectile if ring one not set
        GameObject prefab = ringProjectilePrefab != null ? ringProjectilePrefab : bossProjectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[FinalBoss] Ring shot has no projectile prefab assigned!");
            return;
        }

        int count       = Mathf.Max(1, ringProjectileCount);
        float angleStep = 360f / count;
        // Spawn height — waist/chest level so projectiles fly horizontally
        float spawnY    = transform.position.y + ringShotHeightOffset;

        for (int i = 0; i < count; i++)
        {
            float   angle     = i * angleStep;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Spawn position: on the ring around the boss body
            Vector3 spawnPos  = new Vector3(
                transform.position.x + direction.x * ringShotSpawnRadius,
                spawnY,
                transform.position.z + direction.z * ringShotSpawnRadius);

            GameObject proj = Instantiate(prefab, spawnPos,
                                          Quaternion.LookRotation(direction));

            // Ignore the boss's own collider
            SadnessProjectile sp = proj.GetComponent<SadnessProjectile>();
            if (sp != null) sp.owner = transform;

            if (proj.TryGetComponent(out Rigidbody rb))
            {
                rb.useGravity    = false;
                rb.linearVelocity = direction * ringProjectileForce;
            }
        }

        Debug.Log($"[FinalBoss] Ring shot — {count} projectiles fired.");
    }

    // ── ILovable ──────────────────────────────────────────────────────────────

    public void ReceiveLove(int loveAmount, bool isFromBomb)
    {
        if (CurrentState == BossState.Defeated) return;

        int effective = CurrentState == BossState.Stunned
            ? loveAmount * stunnedLoveMultiplier
            : loveAmount;

        CurrentLove += effective;
        Debug.Log($"[FinalBoss] Received {effective} love (total {CurrentLove}/{loveNeededToDefeat}).");

        PlaySound(hitSound);

        if (CurrentLove >= loveNeededToDefeat)
        {
            StartCoroutine(DefeatSequence());
            return;
        }

        if (isFromBomb && CurrentState != BossState.Stunned)
        {
            CurrentState = BossState.Stunned;
            stateEndTime = Time.time + stunDuration;
            if (animator != null) animator.SetTrigger("Stun");
            Debug.Log("[FinalBoss] Stunned by Love Bomb!");
        }
        else if (CurrentState != BossState.Stunned)
        {
            // 30 % chance to cover/dodge instead of flinching
            if (Random.value < 0.3f)
            {
                if (animator != null) animator.SetTrigger("Cover");
                Debug.Log("[FinalBoss] Dodged!");
            }
            else
            {
                if (animator != null) animator.SetTrigger("Hit");
                Debug.Log("[FinalBoss] Hit reaction.");
            }
        }
    }

    // ── Defeat ────────────────────────────────────────────────────────────────

    IEnumerator DefeatSequence()
    {
        CurrentState = BossState.Defeated;

        // Stop all movement and clear animation bools immediately
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            animator.SetBool("InCombat", false);
            animator.SetTrigger("Die");
        }
        if (bossCollider != null) bossCollider.enabled = false;
        PlaySound(defeatSound);

        // Hide the boss objective marker from the minimap
        if (bossObjectiveMarker != null) bossObjectiveMarker.Hide();

        // Fade out boss fight music
        if (bossFightMusic != null) bossFightMusic.FadeOut();

        // Kill any active pulse VFX immediately
        if (activePulseEffect != null) { Destroy(activePulseEffect); activePulseEffect = null; }

        // Kill all Watchers the boss summoned — they fall with their master
        activeWatchers.RemoveAll(w => w == null);
        foreach (GameObject watcher in activeWatchers)
            Destroy(watcher);
        activeWatchers.Clear();
        Debug.Log("[FinalBoss] Summoned Watchers destroyed.");

        Debug.Log("[FinalBoss] Defeated — playing death animation.");

        // Wait for the death animation to finish before ejecting NPCs.
        // Increase this if the dle00 clip is longer than 2.5 seconds.
        yield return new WaitForSeconds(2.5f);

        // ── Explosion effect — plays exactly once at the boss position
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab,
                                               transform.position, Quaternion.identity);

            // Force loop off so the effect plays exactly once regardless of prefab settings
            ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main  = ps.main;
                main.loop = false;
                ps.Play();
            }

            // Auto-destroy after the effect has finished
            Destroy(explosion, explosionDuration + 2f);
            yield return new WaitForSeconds(explosionDuration);
        }

        UnhappyPerson[] ejected = EjectNPCs();

        // Register with GameManager BEFORE converting so the total is correct first
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && ejected.Length > 0)
            gm.RegisterAdditionalPeople(ejected.Length);

        // Auto-convert every ejected NPC to happy — they burst out already saved,
        // so the HUD and minimap should reflect them as happy immediately.
        foreach (UnhappyPerson npc in ejected)
        {
            if (npc != null) npc.ReceiveLove(999);
        }

        if (bossPhaseTracker != null && ejected.Length > 0)
        {
            bossPhaseTracker.sectionPeople = ejected;
            Debug.Log($"[FinalBoss] Registered {ejected.Length} NPCs with SectionTracker.");
        }

        // Fire AFTER count is updated so any win-condition listener sees the
        // correct happy/total numbers (e.g. GameManager.TriggerWin).
        onDefeated?.Invoke();

        // Destroy after a further delay so ejected NPC coroutines can start
        Destroy(gameObject, 0.5f);
    }

    UnhappyPerson[] EjectNPCs()
    {
        if (savedNPCsToEject == null || savedNPCsToEject.Length == 0)
        {
            Debug.LogWarning("[FinalBoss] savedNPCsToEject is empty — assign NPC prefabs in the Inspector!");
            return new UnhappyPerson[0];
        }

        // Count total so we can assign unique ring slots
        int total = 0;
        foreach (var cfg in savedNPCsToEject) total += cfg.spawnCount;

        var result = new List<UnhappyPerson>();
        float spreadRadius = 3f;
        int   spawnIndex   = 0;

        foreach (var cfg in savedNPCsToEject)
        {
            if (cfg.npcPrefab == null) continue;

            for (int i = 0; i < cfg.spawnCount; i++)
            {
                // Place each NPC at a unique angle in a ring around the boss.
                // NavMeshAgent.Warp() snaps it to the nearest valid NavMesh point —
                // no Rigidbody, no gravity, no coroutines that die with the boss.
                float   angle     = spawnIndex * (360f / Mathf.Max(1, total));
                Vector3 offset    = Quaternion.Euler(0, angle, 0) * Vector3.forward * spreadRadius;
                Vector3 spawnPos  = transform.position + offset;

                // Preserve the prefab's own root rotation so models that need
                // a baked X-offset (e.g. FBX exports lying flat) stand upright.
                GameObject obj = Instantiate(cfg.npcPrefab, spawnPos, cfg.npcPrefab.transform.rotation);

                NavMeshAgent npcAgent = obj.GetComponent<NavMeshAgent>();
                if (npcAgent != null)
                {
                    npcAgent.enabled = true;
                    npcAgent.Warp(spawnPos);   // snaps to NavMesh surface
                }

                if (obj.TryGetComponent(out UnhappyPerson npc))
                    result.Add(npc);

                spawnIndex++;
            }
        }

        Debug.Log($"[FinalBoss] Ejected {result.Count} NPCs in a ring.");
        return result.ToArray();
    }

    // ── Jump ─────────────────────────────────────────────────────────────────

    IEnumerator JumpDash()
    {
        nextJumpTime = Time.time + jumpCooldown;
        CurrentState = BossState.Jumping;   // own state — HandleAttacking won't interfere

        if (animator != null)
        {
            animator.SetTrigger("Jump");
            animator.SetBool("IsRunning", false);
            animator.SetFloat("Speed", 0f);
        }

        // Sprint toward the player while arcing upward then back down.
        // agent.baseOffset lifts the visual mesh above the NavMesh surface,
        // letting the boss clear obstacles with raised edges without any
        // physics or NavMesh rebaking needed.
        float elapsed      = 0f;
        float dashDuration = 0.7f;
        float dashSpeed    = runSpeed * 2f;
        float baseOffset   = agent.baseOffset;  // remember original (usually 0)

        while (elapsed < dashDuration)
        {
            if (player != null)
            {
                agent.isStopped = false;
                agent.speed = dashSpeed;
                agent.SetDestination(player.position);
            }

            // Sin arc: 0 → peak at mid-dash → 0
            // Mathf.Sin(t * PI) gives a perfect single hump between 0 and 1
            float t = elapsed / dashDuration;
            agent.baseOffset = baseOffset + Mathf.Sin(t * Mathf.PI) * jumpArcHeight;

            elapsed += Time.deltaTime;
            yield return null;
        }

        agent.baseOffset = baseOffset;  // restore so normal movement isn't affected
        agent.speed = runSpeed;
        CurrentState = BossState.Chasing;
        Debug.Log("[FinalBoss] Jump complete.");
    }

    // ── Watcher Summon ────────────────────────────────────────────────────────

    void TrySummonWatcher()
    {
        if (watcherPrefab == null) return;
        if (Time.time < nextWatcherSpawnTime) return;

        // Remove destroyed / converted Watchers from the tracking list
        activeWatchers.RemoveAll(w => w == null);

        if (activeWatchers.Count >= maxWatchersAlive) return;

        nextWatcherSpawnTime = Time.time + watcherSpawnCooldown;

        // Pick a random horizontal angle around the boss
        float   angle    = Random.Range(0f, 360f);
        Vector3 offset   = Quaternion.Euler(0, angle, 0) * Vector3.forward * watcherSpawnRadius;
        offset.y         = watcherSpawnHeight;
        Vector3 spawnPos = transform.position + offset;

        GameObject watcher = Instantiate(watcherPrefab, spawnPos, Quaternion.identity);
        activeWatchers.Add(watcher);

        // Point the new Watcher straight at the player so it aggros immediately
        WatcherAI ai = watcher.GetComponent<WatcherAI>();
        if (ai != null && player != null)
            ai.player = player;

        Debug.Log($"[FinalBoss] Summoned Watcher at {spawnPos}. Active: {activeWatchers.Count}/{maxWatchersAlive}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, sfxVolume);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, quickAttackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, heavyAttackRange);
        Gizmos.color = new Color(0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, pulseRadius);
    }
}
