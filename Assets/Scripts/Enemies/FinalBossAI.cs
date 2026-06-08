using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// Final Boss — a ground-based humanoid who spreads sadness and commands the crowd.
// Defeated by love projectiles; bursts into NPCs on death.
// ANIMATOR PARAMETERS (set these up in the Animator Controller):
// Speed       float   — 0 = idle, > 0 = moving (drives walk/run blend)
// InCombat    bool    — true while the boss has spotted the player
// Attack1     trigger — quick attack  (attack00)
// Attack2     trigger — heavy attack  (attack01)
// Attack3     trigger — sadness pulse (attack02)
// Hit         trigger — hit reaction  (hit00 / hit01, randomised by script)
// Stun        trigger — fall and rise (fall&riseup00)
// Die         trigger — death         (dle00)

[RequireComponent(typeof(NavMeshAgent))]
public class FinalBossAI : MonoBehaviour, ILovable<bool>
{
    // ── State machine ─────────────────────────────────────────────────────────
    public enum BossState { Idle, Chasing, Attacking, Jumping, Stunned, PhaseTransition, Defeated }
    public BossState CurrentState { get; private set; } = BossState.Idle;
    public int CurrentLove { get; private set; }

    // ── Stats ─────────────────────────────────────────────────────────────────
    [Header("Boss Stats")]
    public int loveNeededToDefeat = 15;
    [Tooltip("Multiplier applied to love received while stunned.")]
    [SerializeField] private int stunnedLoveMultiplier = 3;
    [SerializeField] private float stunDuration = 4f;

    // ── Movement ──────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float walkSpeed  = 2f;
    [SerializeField] private float runSpeed   = 8f;    // set higher than player speed so boss can catch up
    [SerializeField] private float aggroRange = 20f;
    [Tooltip("Distance at which the boss breaks into a run instead of walking.")]
    [SerializeField] private float runThreshold = 4f;
    [Tooltip("Distance at which the boss jumps toward the player to close the gap.")]
    [SerializeField] private float jumpTriggerDistance = 10f;
    [SerializeField] private float jumpCooldown = 5f;
    [Tooltip("How many units the boss rises at the peak of the jump arc. " +
             "Increase this to clear taller obstacles.")]
    [SerializeField] private float jumpArcHeight = 3f;

    // ── Attacks ───────────────────────────────────────────────────────────────
    [Header("Quick Attack (Attack1)")]
    [Tooltip("Melee range for the quick jab.")]
    [SerializeField] private float quickAttackRange = 2.5f;
    [SerializeField] private int   quickAttackDamage = 10;
    [SerializeField] private float quickAttackCooldown = 2f;
    [SerializeField] private float quickAttackAnimLength = 0.8f;

    [Header("Heavy Attack (Attack2)")]
    [Tooltip("Melee range for the heavy swing — slightly wider than quick.")]
    [SerializeField] private float heavyAttackRange = 3.5f;
    [SerializeField] private int   heavyAttackDamage = 25;
    [SerializeField] private float heavyAttackCooldown = 4f;
    [SerializeField] private float heavyAttackAnimLength = 1.2f;

    [Header("Sadness Pulse (Attack3)")]
    [Tooltip("Radius of the AoE sadness pulse centred on the boss.")]
    [SerializeField] private float pulseRadius = 8f;
    [SerializeField] private int   pulseDamage = 15;
    [SerializeField] private float pulseCooldown = 6f;
    [SerializeField] private float pulseAnimLength = 1.5f;
    [Tooltip("Optional particle/effect spawned at the boss position during the pulse.")]
    [SerializeField] private GameObject pulseEffectPrefab;

    [Header("Teleport Dash (Attack5)")]
    [Tooltip("The boss vanishes and reappears directly behind the player, then immediately swings. " +
             "Triggers at any distance once the cooldown is ready.")]
    [SerializeField] private float teleportDashCooldown = 12f;
    [Tooltip("How far behind the player the boss lands (metres).")]
    [SerializeField] private float teleportBehindDistance = 1.8f;
    [Tooltip("Damage dealt by the guaranteed strike right after the teleport.")]
    [SerializeField] private int   teleportStrikeDamage = 20;
    [Tooltip("Optional VFX prefab played at the DISAPPEAR position.")]
    [SerializeField] private GameObject teleportDisappearVFX;
    [Tooltip("Optional VFX prefab played at the APPEAR position.")]
    [SerializeField] private GameObject teleportAppearVFX;
    [Tooltip("Duration of the VFX / brief pause before the boss reappears.")]
    [SerializeField] private float teleportVFXDuration = 0.25f;
    [Tooltip("How long the strike animation lasts after the boss reappears.")]
    [SerializeField] private float teleportStrikeAnimLength = 0.8f;

    [Header("Ring Shot (Attack4)")]
    [Tooltip("Number of projectiles fired evenly spread around the full 360°.")]
    [SerializeField] private int   ringProjectileCount = 12;
    [Tooltip("Projectile prefab for the ring shot. Reuses bossProjectilePrefab if left empty.")]
    [SerializeField] private GameObject ringProjectilePrefab;
    [Tooltip("Fallback single-shot projectile prefab (used by ring shot if ringProjectilePrefab is empty).")]
    [SerializeField] private GameObject bossProjectilePrefab;
    [Tooltip("How fast each ring projectile travels.")]
    [SerializeField] private float ringProjectileForce = 14f;
    [Tooltip("Distance at which the ring shot triggers (medium range).")]
    [SerializeField] private float ringShotRange = 14f;
    [SerializeField] private float ringShotCooldown = 7f;
    [SerializeField] private float ringShotAnimLength = 1.0f;
    [Tooltip("Radius around the boss body from which each projectile spawns.")]
    [SerializeField] private float ringShotSpawnRadius = 0.8f;
    [Tooltip("Y offset from the boss pivot where projectiles spawn (waist / chest height).")]
    [SerializeField] private float ringShotHeightOffset = 1.2f;

    // ── Phase Relocation ──────────────────────────────────────────────────────
    [Header("Phase — Boss Relocation")]
    [Tooltip("Two Transform markers placed in the city.\n" +
             "  [0] = where the boss warps when Phase 2 starts (≥ phase2Threshold).\n" +
             "  [1] = where the boss warps when Phase 3 starts (≥ phase3Threshold).\n" +
             "Leave empty to disable phase teleportation entirely.")]
    [SerializeField] private Transform[] phaseSpawnPoints;

    [Range(0f, 1f)]
    [Tooltip("Fraction of loveNeededToDefeat at which the boss escapes to Phase 2 location. " +
             "Default 0.30 = 30 %.")]
    [SerializeField] private float phase2Threshold = 0.30f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of loveNeededToDefeat at which the boss escapes to Phase 3 location. " +
             "Default 0.80 = 80 %.")]
    [SerializeField] private float phase3Threshold = 0.80f;

    [Tooltip("VFX prefab played at the boss position when it vanishes (used for both departure " +
             "and arrival). Leave empty to skip.")]
    [SerializeField] private GameObject phaseTeleportVFX;

    [Tooltip("Seconds the boss stays invisible while warping to the next phase location.")]
    [SerializeField] private float phaseTransitionDuration = 1.5f;

    [Tooltip("Optional HintMessage component to call ShowWithText() on during each transition. " +
             "Wire to a trigger zone or a screen-space canvas HintMessage in the scene.")]
    [SerializeField] private HintMessage phaseHintMessage;

    [Tooltip("Hint shown to the player when the boss escapes to Phase 2.")]
    [SerializeField] private string phase2Message = "The boss has fled! Find him across town!";

    [Tooltip("Hint shown to the player when the boss escapes to Phase 3.")]
    [SerializeField] private string phase3Message = "He's running again — track him down and finish this!";

    // ── Watcher Summons ───────────────────────────────────────────────────────
    [Header("Watcher Summons")]
    [Tooltip("The WatcherAI prefab to spawn during combat.")]
    [SerializeField] private GameObject watcherPrefab;

    [Tooltip("Maximum number of Watchers alive at the same time. " +
             "Boss won't summon more until one is defeated.")]
    [SerializeField] private int maxWatchersAlive = 2;

    [Tooltip("Seconds between summons (when below the max).")]
    [SerializeField] private float watcherSpawnCooldown = 18f;

    [Tooltip("Radius around the boss where Watchers appear.")]
    [SerializeField] private float watcherSpawnRadius = 6f;

    [Tooltip("How high above the boss the Watcher spawns (they hover, so start them elevated).")]
    [SerializeField] private float watcherSpawnHeight = 4f;

    private float nextWatcherSpawnTime;
    private List<GameObject> activeWatchers = new List<GameObject>();

    // ── NPC ejection ──────────────────────────────────────────────────────────
    [Header("Defeat — Explosion")]
    [Tooltip("Particle / VFX prefab instantiated at the boss position on defeat, " +
             "before NPCs are ejected. Leave empty to skip.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [Tooltip("Seconds to wait after the explosion spawns before ejecting NPCs. " +
             "Match this to the peak of your explosion effect.")]
    [SerializeField] private float explosionDuration = 1.2f;

    [Tooltip("MinimapMarker (Objective type) for the boss location. " +
             "Hidden automatically when the boss is defeated.")]
    public MinimapMarker bossObjectiveMarker;

    [Header("Defeat — NPC Ejection")]
    [Tooltip("NPC prefabs and counts to burst out of the boss on defeat.")]
    [SerializeField] private SavedNPCConfig[] savedNPCsToEject;
    [SerializeField] private float ejectForce = 400f;

    // ── References ────────────────────────────────────────────────────────────
    [Header("References")]
    public Transform player;
    [Tooltip("Assign a SectionTracker — the ejected NPCs are auto-registered with it " +
             "so the section completes once they are all converted.")]
    [SerializeField] private SectionTracker bossPhaseTracker;

    // ── Music ─────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("MusicController for the boss fight track. " +
             "Played the moment the boss first aggros onto the player.")]
    [SerializeField] private MusicController bossFightMusic;

    // ── Events ────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Fires the moment the boss is defeated (before the die animation finishes). " +
             "Wire to music, HUD message, etc.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onDefeated;

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip pulseSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip defeatSound;

    [Range(0f, 1f)]
    [Tooltip("Master volume for all boss sound effects.")]
    [SerializeField] private float sfxVolume = 1f;

    // ── Privates ──────────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Animator     animator;
    private Collider     bossCollider;
    private AudioSource  audioSource;

    private bool        isAggroed     = false;   // once true, boss never returns to Idle
    private bool        musicStarted  = false;   // guard against double-play
    private int         currentPhase  = 1;       // 1 / 2 / 3 — controls phase-teleport thresholds
    private GameObject  activePulseEffect;       // tracked so it can be destroyed after the attack
    private float nextQuickAttackTime;
    private float nextHeavyAttackTime;
    private float nextPulseTime;
    private float nextRingShotTime;
    private float nextTeleportDashTime;
    private float nextJumpTime;
    private float stateEndTime;

    [Header("UI — Boss Love Bar")]
    [SerializeField] private BossLoveBar bossLoveBar; // drag the child bar here in Inspector

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        TryGetComponent(out agent);
        TryGetComponent(out animator);
        TryGetComponent(out bossCollider);
        TryGetComponent(out audioSource);
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        agent.speed = walkSpeed;

        // Give the teleport-dash a warm-up delay so it doesn't fire on the
        // very first frame the boss enters Chasing state.
        nextTeleportDashTime = Time.time + teleportDashCooldown;

        // Prevent the music from auto-starting if its GO is a child of this prefab.
        // We'll enable it manually the moment the boss first aggros the player.
        if (bossFightMusic != null)
            bossFightMusic.gameObject.SetActive(false);

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (bossLoveBar != null)
    {
        bossLoveBar.Init(transform);
        UpdateBossLoveBar(); // shows full at start
    }
    }

    void Update()
    {
        if (CurrentState == BossState.Defeated || player == null) return;

        // Keep the objective marker in sync regardless of whether it is a
        // child of this object or a standalone scene object.
        if (bossObjectiveMarker != null)
            bossObjectiveMarker.transform.position = transform.position;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Once aggroed, skip Idle and go straight to Chasing
        if (isAggroed && CurrentState == BossState.Idle)
            CurrentState = BossState.Chasing;

        switch (CurrentState)
        {
            case BossState.Idle:             HandleIdle(distToPlayer);      break;
            case BossState.Chasing:          HandleChasing(distToPlayer);   break;
            case BossState.Attacking:        HandleAttacking();              break;
            case BossState.Jumping:          /* driven by JumpDash coroutine */     break;
            case BossState.Stunned:          HandleStunned();                break;
            case BossState.PhaseTransition:  /* driven by PhaseTransition coroutine */ break;
        }

        // Watcher summon — runs independently of the attack state machine,
        // but not during a phase teleport
        if (isAggroed && CurrentState != BossState.PhaseTransition)
            TrySummonWatcher();
    }

    // ── State handlers ────────────────────────────────────────────────────────
    private void UpdateBossLoveBar()
{
    if (bossLoveBar != null)
        bossLoveBar.SetValues(CurrentLove, loveNeededToDefeat);
}
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

        // Teleport behind the player — fires at any distance once cooldown is ready
        if (Time.time >= nextTeleportDashTime)
        {
            StartCoroutine(TeleportDash());
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

    enum BossAttackType { Quick, Heavy, Pulse, RingShot, TeleportDash }

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

            // Set owner so SadnessProjectile's root check can identify us
            SadnessProjectile sp = proj.GetComponent<SadnessProjectile>();
            if (sp != null) sp.owner = transform;

            // Immediately ignore all boss colliders — don't wait for
            // SadnessProjectile.Start() next frame, which would be too late
            // if a FixedUpdate runs first and detects the overlap.
            Collider projCol = proj.GetComponent<Collider>();
            if (projCol != null)
            {
                foreach (Collider bossCol in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(projCol, bossCol, true);
            }

            if (proj.TryGetComponent(out Rigidbody rb))
            {
                rb.useGravity     = false;
                rb.linearVelocity = direction * ringProjectileForce;
            }
        }

        Debug.Log($"[FinalBoss] Ring shot — {count} projectiles fired.");
    }

    // ── ILovable ──────────────────────────────────────────────────────────────

    public void ReceiveLove(int loveAmount, bool isFromBomb)
    {
        // Ignore love during defeat sequence or while teleporting between phases
        if (CurrentState == BossState.Defeated ||
            CurrentState == BossState.PhaseTransition) return;

        int effective = CurrentState == BossState.Stunned
            ? loveAmount * stunnedLoveMultiplier
            : loveAmount;

        CurrentLove += effective;
        UpdateBossLoveBar();
        Debug.Log($"[FinalBoss] Received {effective} love (total {CurrentLove}/{loveNeededToDefeat}).");

        PlaySound(hitSound);

        // ── Phase-transition checks (only when spawn points are configured) ────
        bool phasesEnabled = phaseSpawnPoints != null && phaseSpawnPoints.Length >= 2;

        if (phasesEnabled)
        {
            float ratio = (float)CurrentLove / loveNeededToDefeat;

            if (currentPhase == 1 && ratio >= phase2Threshold)
            {
                // Cap love so the bar stops at the threshold — the remaining
                // portion must be filled during Phase 2 / 3.
                CurrentLove = Mathf.RoundToInt(phase2Threshold * loveNeededToDefeat);
                UpdateBossLoveBar();
                // Kill any in-flight attack coroutines (TeleportDash, JumpDash,
                // DelayRingShot, etc.) so their agent.Warp() can't overwrite ours.
                StopAllCoroutines();
                // Restore renderers in case an attack coroutine hid them mid-animation.
                foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
                StartCoroutine(DoPhaseTransition(2));
                return;
            }

            if (currentPhase == 2 && ratio >= phase3Threshold)
            {
                CurrentLove = Mathf.RoundToInt(phase3Threshold * loveNeededToDefeat);
                UpdateBossLoveBar();
                StopAllCoroutines();
                foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
                StartCoroutine(DoPhaseTransition(3));
                return;
            }

            // Final defeat only allowed once in Phase 3
            if (currentPhase == 3 && CurrentLove >= loveNeededToDefeat)
            {
                StartCoroutine(DefeatSequence());
                return;
            }
        }
        else
        {
            // No phases configured — original single-phase behaviour
            if (CurrentLove >= loveNeededToDefeat)
            {
                StartCoroutine(DefeatSequence());
                return;
            }
        }

        // ── Hit reactions ─────────────────────────────────────────────────────
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
        if (bossLoveBar != null)
    bossLoveBar.gameObject.SetActive(false);
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

        // Kill all Watchers the boss summoned — they fall with their master.
        // Also catches any watchers that survived from earlier phases.
        activeWatchers.RemoveAll(w => w == null);
        foreach (GameObject watcher in activeWatchers)
        {
            if (watcher != null) Destroy(watcher);
        }
        activeWatchers.Clear();
        Debug.Log("[FinalBoss] All summoned Watchers destroyed.");

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

    // ── Teleport Dash ─────────────────────────────────────────────────────────

    IEnumerator TeleportDash()
    {
        // Enter a locked state so no other attack interrupts the sequence
        CurrentState = BossState.Attacking;
        agent.isStopped = true;
        nextTeleportDashTime = Time.time + teleportDashCooldown;

        // ── 1. Disappear VFX at current position ──────────────────────────────
        if (teleportDisappearVFX != null)
        {
            GameObject vfx = Instantiate(teleportDisappearVFX, transform.position, transform.rotation);
            Destroy(vfx, teleportVFXDuration + 1f);
        }

        // Briefly hide the boss mesh so it looks like it vanished
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        yield return new WaitForSeconds(teleportVFXDuration);

        // ── 2. Calculate position directly behind the player ──────────────────
        // Use the player's forward so "behind" is relative to where they face.
        Vector3 behindOffset  = -player.forward * teleportBehindDistance;
        Vector3 targetPos     = player.position + behindOffset;
        targetPos.y           = transform.position.y; // keep boss on the ground

        // Snap to NavMesh so the boss doesn't land in geometry
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            targetPos = navHit.position;

        agent.Warp(targetPos);

        // Face the player from behind
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // ── 3. Reappear ───────────────────────────────────────────────────────
        foreach (var r in renderers) r.enabled = true;

        if (teleportAppearVFX != null)
        {
            GameObject vfx = Instantiate(teleportAppearVFX, transform.position, transform.rotation);
            Destroy(vfx, teleportStrikeAnimLength + 1f);
        }

        // ── 4. Immediate strike ───────────────────────────────────────────────
        if (animator != null) animator.SetTrigger("Attack1");
        PlaySound(attackSound);
        DealMeleeDamage(teleportStrikeDamage, teleportBehindDistance + 1.5f);

        stateEndTime = Time.time + teleportStrikeAnimLength;

        Debug.Log("[FinalBoss] Teleport Dash — appeared behind player and struck!");
    }

    // ── Phase Relocation ──────────────────────────────────────────────────────

    /// <summary>
    /// Hides the boss, warps it to the next phase spawn point, shows a hint, then
    /// resumes combat at the new location.  The minimap marker follows automatically
    /// because it lives on the boss GameObject.
    /// </summary>
    IEnumerator DoPhaseTransition(int newPhase)
    {
        currentPhase = newPhase;
        CurrentState = BossState.PhaseTransition;

        // Stop movement immediately
        agent.isStopped = true;
        agent.velocity  = Vector3.zero;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsRunning", false);
            animator.SetBool("InCombat", false);
        }

        Debug.Log($"[FinalBoss] Phase {newPhase} transition — vanishing…");

        // ── Departure VFX + vanish ────────────────────────────────────────────
        if (phaseTeleportVFX != null)
        {
            GameObject vfx = Instantiate(phaseTeleportVFX, transform.position, transform.rotation);
            Destroy(vfx, phaseTransitionDuration + 2f);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        // Boss is invisible and invulnerable while warping
        yield return new WaitForSeconds(phaseTransitionDuration);

        // ── Destroy any active Watchers before moving to the new location ────
        // This prevents them lingering in the old area while the player searches
        // for the boss across town.
        activeWatchers.RemoveAll(w => w == null);
        foreach (GameObject watcher in activeWatchers)
            Destroy(watcher);
        activeWatchers.Clear();

        // ── Teleport to the phase spawn point ────────────────────────────────
        // phaseSpawnPoints[0] = Phase-2 destination
        // phaseSpawnPoints[1] = Phase-3 destination
        int idx = newPhase - 2;   // phase 2 → 0,  phase 3 → 1
        if (idx >= 0 && idx < phaseSpawnPoints.Length && phaseSpawnPoints[idx] != null)
        {
            Vector3 dest = phaseSpawnPoints[idx].position;

            // Pre-sample so we know the exact on-mesh landing point BEFORE
            // touching the agent.  Use a generous 15 m radius in case the
            // designer placed the spawn marker slightly above the NavMesh.
            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                dest = hit.position;
                Debug.Log($"[FinalBoss] Phase {newPhase} — NavMesh snap: {dest}.");
            }
            else
            {
                Debug.LogWarning($"[FinalBoss] Phase {newPhase} — no NavMesh found within 15 m of spawn point {dest}! Check that your NavMesh is baked there.");
            }

            // Full agent reset: stop → disable → move transform → wait one frame
            // so Unity processes the position before the agent re-initialises.
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity  = Vector3.zero;
            agent.enabled   = false;
            transform.position = dest;

            yield return null;   // one frame gap — critical for agent re-init

            agent.enabled = true;

            Debug.Log($"[FinalBoss] Phase {newPhase} — agent re-enabled at {transform.position}.");
        }
        else
        {
            Debug.LogWarning($"[FinalBoss] Phase {newPhase} spawn point not assigned in Inspector!");
            yield return null;
        }

        // ── Arrival VFX + reappear ────────────────────────────────────────────
        if (phaseTeleportVFX != null)
        {
            GameObject vfx = Instantiate(phaseTeleportVFX, transform.position, transform.rotation);
            Destroy(vfx, 2f);
        }

        foreach (var r in renderers) r.enabled = true;

        // ── Hint message to guide the player ─────────────────────────────────
        if (phaseHintMessage != null)
        {
            string msg = newPhase == 2 ? phase2Message : phase3Message;
            phaseHintMessage.ShowWithText(msg);
        }

        // ── Resume combat ─────────────────────────────────────────────────────
        agent.isStopped = false;
        CurrentState    = BossState.Chasing;
        if (animator != null) animator.SetBool("InCombat", true);

        // Reset teleport-dash timer so it doesn't fire the instant we reappear
        nextTeleportDashTime = Time.time + teleportDashCooldown;

        Debug.Log($"[FinalBoss] Phase {newPhase} — now chasing player at new location.");
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

        // Skip the Watcher's Idle state and begin chasing the player immediately
        WatcherAI ai = watcher.GetComponent<WatcherAI>();
        if (ai != null && player != null)
            ai.ActivateFromBoss(player);

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
