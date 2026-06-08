using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using Random = UnityEngine.Random;

/// <summary>
/// Defines a strict contract for entities that can receive love within the game ecosystem.
/// </summary>
public interface ILovable<T>
{
    void ReceiveLove(int loveAmount, T sourceModifier);
}

/// <summary>
/// A configuration container that defines an NPC prefab and how many of them to spawn.
/// </summary>
[Serializable]
public struct SavedNPCConfig
{
    [Tooltip("The specific NPC Prefab to instantiate.")]
    public GameObject npcPrefab;

    [Tooltip("The exact number of this specific prefab to spawn when the boss is defeated.")]
    public int spawnCount;
}

/// <summary>
/// Controls the behavior, state machine, and combat logic for the Watcher flying boss.
/// Does NOT use NavMeshAgent — flying enemy, moves freely in 3D space.
/// </summary>
public class WatcherAI : MonoBehaviour, ILovable<bool>
{
    public enum BossState { Idle, Chasing, Attacking, Stunned, Converted }

    public BossState CurrentState { get; private set; } = BossState.Idle;
    public int CurrentLove { get; private set; }

    [Header("Boss Stats")]
    public int loveNeededToConvert = 10;
    [Tooltip("Chase speed. Should be higher than the Final Boss's runSpeed so Watchers " +
             "can flank the player independently rather than trailing behind the boss.")]
    [SerializeField] private float runSpeed = 12f;
    [Tooltip("Multiplier applied to love received while stunned.")]
    [SerializeField] private int stunnedLoveMultiplier = 2;
    [SerializeField] private float stunDuration = 3f;

    [Header("Flight")]
    [Tooltip("Height the Watcher hovers above the ground.")]
    [SerializeField] private float hoverHeight = 3f;
    [Tooltip("How quickly the Watcher adjusts its hover height.")]
    [SerializeField] private float hoverSpeed = 3f;

    [Header("Detection")]
    [SerializeField] private float aggroRange = 30f;

    [Header("Attack Ranges")]
    [SerializeField] private float biteAttackRange = 4f;
    [SerializeField] private float projectileAttackRange = 20f;
    [SerializeField] private float attackCooldown = 3f;
    [SerializeField] private float attackAnimationLength = 1.5f;

    [Header("Projectile Settings")]
    [SerializeField] private GameObject bossProjectilePrefab;
    [SerializeField] private Transform eyeFirePoint;
    [SerializeField] private float projectileForce = 20f;
    [Tooltip("How high above the player's pivot to aim.")]
    [SerializeField] private float playerAimOffsetY = 1.5f;

    [Header("Defeat / Saved NPCs")]
    [Tooltip("NPC prefabs and counts to burst out of the boss on defeat.")]
    [SerializeField] private SavedNPCConfig[] savedNPCsToEject;
    [SerializeField] private float ejectForce = 500f;

    [Header("Defeat — Pickup Drop")]
    [Tooltip("Ammo pickup prefab. Leave empty to exclude from the drop table.")]
    [SerializeField] private GameObject ammoDropPrefab;
    [Tooltip("Health pickup prefab. Leave empty to exclude from the drop table.")]
    [SerializeField] private GameObject healthDropPrefab;
    [Tooltip("Love Bomb pickup prefab. Leave empty to exclude from the drop table.")]
    [SerializeField] private GameObject loveBombDropPrefab;
    [Tooltip("Relative weight for each drop type (Ammo / Health / LoveBomb). " +
             "Higher = more likely. Values are normalised automatically.")]
    [SerializeField] private float ammoDropWeight    = 2f;
    [SerializeField] private float healthDropWeight  = 1f;
    [SerializeField] private float loveBombDropWeight = 1f;
    [Tooltip("Height above the Watcher's position to spawn the drop.")]
    [SerializeField] private float dropHeightOffset = 0.5f;

    [Header("References")]
    public Transform player;

    [Header("Audio")]
    [Tooltip("Sound played when the Watcher fires a projectile.")]
    [SerializeField] private AudioClip attackSound;
    [Tooltip("Sound played when the Watcher performs a bite attack.")]
    [SerializeField] private AudioClip biteAttackSound;
    [Tooltip("Sound played when the Watcher is hit by love.")]
    [SerializeField] private AudioClip hitSound;

    [Range(0f, 1f)]
    [Tooltip("Master volume for all Watcher sound effects.")]
    [SerializeField] private float sfxVolume = 1f;

    private AudioSource audioSource;

    [Header("Events")]
    [Tooltip("Fired after the boss is defeated and NPCs are ejected. " +
             "Wire to music change, HUD message, win condition, etc.")]
    public UnityEngine.Events.UnityEvent onDefeated;

    [Tooltip("Optional — assign a SectionTracker to auto-register the ejected NPCs " +
             "so the section completes once they are all converted.")]
    [SerializeField] private SectionTracker bossPhaseTracker;

    private Animator animator;
    private Collider bossCollider;
    private float stateEndTime = 0f;
    private float nextAttackTime = 0f;
    private bool isDoingBiteAttack = false;

    [Header("UI — Love Bar")]
    [SerializeField] private WatcherLoveBar loveBar; // drag the CHILD bar here

    /// <summary>
    /// Called by FinalBossAI when it summons this Watcher mid-fight.
    /// Skips the Idle aggro check and begins chasing immediately.
    /// </summary>
    public void ActivateFromBoss(Transform playerTransform)
    {
        player       = playerTransform;
        CurrentState = BossState.Chasing;
        Debug.Log("[WatcherAI] Activated by boss — chasing player immediately.");
    }

    private void Start()
    {
        CurrentLove = 0;
        TryGetComponent(out animator);
        TryGetComponent(out bossCollider);
        TryGetComponent(out audioSource);
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

    if (loveBar != null)
    {
        loveBar.Init(transform);
        UpdateLoveBar(); // full at start
    }
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.LogWarning("[WatcherAI] Rigidbody found — consider removing it. Flying enemies don't need one.");
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player == null)
            Debug.LogError("[WatcherAI] No player found! Tag your player as 'Player'.");
    }

    private void Update()
    {
        if (CurrentState == BossState.Converted || player == null) return;

        float targetY = GetHoverTargetY();
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * hoverSpeed);
        transform.position = pos;

        switch (CurrentState)
        {
            case BossState.Idle:      HandleIdle();      break;
            case BossState.Chasing:   HandleChasing();   break;
            case BossState.Attacking: HandleAttacking(); break;
            case BossState.Stunned:   HandleStunned();   break;
        }
    }

    private void UpdateLoveBar()
    {
        if (loveBar != null)
            loveBar.SetValues(CurrentLove, loveNeededToConvert);
    }

    private float GetHoverTargetY()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                            out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y + hoverHeight;
        return hoverHeight;
    }

    private void HandleIdle()
    {
        if (animator != null) animator.SetFloat("Speed", 0f);

        float sqrDist = (transform.position - player.position).sqrMagnitude;
        if (sqrDist < aggroRange * aggroRange)
        {
            CurrentState = BossState.Chasing;
            Debug.Log("[WatcherAI] Player detected! Chasing.");
        }
    }

    private void HandleChasing()
    {
        float sqrDistance = (transform.position - player.position).sqrMagnitude;
        FacePlayer();

        if (sqrDistance <= biteAttackRange * biteAttackRange && Time.time >= nextAttackTime)
        {
            CurrentState = BossState.Attacking;
            isDoingBiteAttack = true;
        }
        else if (sqrDistance <= projectileAttackRange * projectileAttackRange && Time.time >= nextAttackTime)
        {
            CurrentState = BossState.Attacking;
            isDoingBiteAttack = false;
        }
        else
        {
            Vector3 targetPos = new Vector3(player.position.x, transform.position.y, player.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, runSpeed * Time.deltaTime);
            if (animator != null) animator.SetFloat("Speed", runSpeed);
        }
    }

    private void HandleAttacking()
    {
        FacePlayer();

        if (Time.time >= nextAttackTime)
        {
            if (isDoingBiteAttack)
            {
                if (animator != null) animator.SetTrigger("Attack2");
                PlaySound(biteAttackSound);
                Debug.Log("[WatcherAI] Bite attack!");
            }
            else
            {
                if (animator != null) animator.SetTrigger("Attack1");
                FireProjectile();
                PlaySound(attackSound);
                Debug.Log("[WatcherAI] Projectile attack!");
            }

            stateEndTime   = Time.time + attackAnimationLength;
            nextAttackTime = Time.time + attackCooldown;
            if (animator != null) animator.SetFloat("Speed", 0f);
        }
        else if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
        }
    }

    private void FireProjectile()
    {
        if (bossProjectilePrefab == null || eyeFirePoint == null) return;

        Vector3 targetPos  = player.position + Vector3.up * playerAimOffsetY;
        Vector3 dirToPlayer = (targetPos - eyeFirePoint.position).normalized;

        GameObject proj = Instantiate(bossProjectilePrefab, eyeFirePoint.position,
                                      Quaternion.LookRotation(dirToPlayer));

        SadnessProjectile sp = proj.GetComponent<SadnessProjectile>();
        if (sp != null) sp.owner = transform;

        if (proj.TryGetComponent(out Rigidbody rb))
        {
            rb.useGravity = false;
            rb.linearVelocity = dirToPlayer * projectileForce;
        }
    }

    private void HandleStunned()
    {
        if (animator != null) animator.SetFloat("Speed", 0f);
        if (Time.time >= stateEndTime)
            CurrentState = BossState.Chasing;
    }

    private void FacePlayer()
    {
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
    }

    public void ReceiveLove(int loveAmount, bool isFromBomb)
    {
        if (CurrentState == BossState.Converted) return;

        if (CurrentState == BossState.Stunned && !isFromBomb)
            loveAmount *= stunnedLoveMultiplier;

        CurrentLove += loveAmount;
        UpdateLoveBar();
        Debug.Log($"[WatcherAI] Received {loveAmount} love (total: {CurrentLove}/{loveNeededToConvert})");

        if (CurrentLove >= loveNeededToConvert)
        {
            BecomeHappy();
            return;
        }

        PlaySound(hitSound);

        if (isFromBomb)
        {
            CurrentState = BossState.Stunned;
            stateEndTime = Time.time + stunDuration;
            if (animator != null) animator.SetTrigger("Stun");
        }
        else if (CurrentState != BossState.Stunned)
        {
            if (animator != null) animator.SetTrigger("Hit");
        }
    }

    private void BecomeHappy()
    {
        CurrentState = BossState.Converted;

        if (loveBar != null)
        loveBar.gameObject.SetActive(false);
        
        if (animator != null) animator.SetTrigger("Die");
        if (bossCollider != null) bossCollider.enabled = false;

        Debug.Log("[WatcherAI] Converted! Ejecting NPCs.");

        // Random weighted drop — build table from whichever prefabs are assigned
        SpawnRandomDrop();

        UnhappyPerson[] ejected = EjectNPCs();

        // Hand ejected NPCs to an optional SectionTracker so the section
        // completes once they are all converted by the player.
        if (bossPhaseTracker != null && ejected != null && ejected.Length > 0)
        {
            bossPhaseTracker.sectionPeople = ejected;
            Debug.Log($"[WatcherAI] Registered {ejected.Length} ejected NPCs with SectionTracker.");
        }

        // Tell GameManager about the new NPCs so the global count stays correct.
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && ejected != null)
            gm.RegisterAdditionalPeople(ejected.Length);

        onDefeated?.Invoke();
        Destroy(gameObject, 8f);
    }

    /// <summary>
    /// Builds a weighted drop table from whichever pickup prefabs are assigned,
    /// rolls a random pick, and spawns the winner above the Watcher's position.
    /// Any prefab left null is simply excluded from the table.
    /// </summary>
    private void SpawnRandomDrop()
    {
        // Build table: (prefab, weight) pairs — skip any null prefabs
        var table = new System.Collections.Generic.List<(GameObject prefab, float weight)>();
        if (ammoDropPrefab    != null) table.Add((ammoDropPrefab,    ammoDropWeight));
        if (healthDropPrefab  != null) table.Add((healthDropPrefab,  healthDropWeight));
        if (loveBombDropPrefab != null) table.Add((loveBombDropPrefab, loveBombDropWeight));

        if (table.Count == 0) return; // nothing assigned — no drop

        // Sum weights
        float total = 0f;
        foreach (var entry in table) total += entry.weight;

        // Roll
        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        GameObject chosen = null;
        foreach (var entry in table)
        {
            cumulative += entry.weight;
            if (roll <= cumulative)
            {
                chosen = entry.prefab;
                break;
            }
        }
        if (chosen == null) chosen = table[table.Count - 1].prefab; // safety fallback

        Vector3 dropPos = transform.position + Vector3.up * dropHeightOffset;
        Instantiate(chosen, dropPos, Quaternion.identity);
        Debug.Log($"[WatcherAI] Dropped: {chosen.name}");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Instantiates every configured NPC and blasts them outward with explosion force.
    /// Returns only the NPCs that have an UnhappyPerson component (for tracker registration).
    /// </summary>
    private UnhappyPerson[] EjectNPCs()
    {
        if (savedNPCsToEject == null || savedNPCsToEject.Length == 0)
            return new UnhappyPerson[0];

        // Pre-count total NPCs so we can assign unique ring slots
        int total = 0;
        foreach (var cfg in savedNPCsToEject) total += cfg.spawnCount;

        var result = new System.Collections.Generic.List<UnhappyPerson>(total);

        // Spread radius — NPCs are placed in a circle around the boss position
        float spreadRadius = 3f;
        int spawnIndex = 0;

        foreach (var cfg in savedNPCsToEject)
        {
            if (cfg.npcPrefab == null) continue;

            for (int j = 0; j < cfg.spawnCount; j++)
            {
                // Place each NPC at a unique angle in the ring so none overlap
                float angle = spawnIndex * (360f / Mathf.Max(1, total));
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * spreadRadius;
                Vector3 spawnPos = transform.position + offset;
                spawnPos.y = transform.position.y; // keep at boss ground level

                GameObject npc = Instantiate(cfg.npcPrefab, spawnPos, Quaternion.identity);

                // Use NavMeshAgent.Warp() to snap the NPC to the nearest NavMesh
                // surface at its ring position — no Rigidbody or gravity needed.
                NavMeshAgent npcAgent = npc.GetComponent<NavMeshAgent>();
                if (npcAgent != null)
                {
                    npcAgent.enabled = true;
                    npcAgent.Warp(spawnPos);
                }

                if (npc.TryGetComponent(out UnhappyPerson person))
                    result.Add(person);

                spawnIndex++;
            }
        }

        Debug.Log($"[WatcherAI] Ejected {result.Count} NPCs in a ring.");
        return result.ToArray();
    }
}