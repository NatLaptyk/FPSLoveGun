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
    public float runSpeed = 4f;
    [Tooltip("Multiplier applied to love received while stunned.")]
    public int stunnedLoveMultiplier = 2;
    public float stunDuration = 3f;

    [Header("Flight")]
    [Tooltip("Height the Watcher hovers above the ground.")]
    public float hoverHeight = 3f;
    [Tooltip("How quickly the Watcher adjusts its hover height.")]
    public float hoverSpeed = 3f;

    [Header("Detection")]
    public float aggroRange = 30f;

    [Header("Attack Ranges")]
    public float biteAttackRange = 4f;
    public float projectileAttackRange = 20f;
    public float attackCooldown = 3f;
    public float attackAnimationLength = 1.5f;

    [Header("Projectile Settings")]
    public GameObject bossProjectilePrefab;
    public Transform eyeFirePoint;
    public float projectileForce = 20f;
    [Tooltip("How high above the player's pivot to aim.")]
    public float playerAimOffsetY = 1.5f;

    [Header("Defeat / Saved NPCs")]
    [Tooltip("NPC prefabs and counts to burst out of the boss on defeat.")]
    public SavedNPCConfig[] savedNPCsToEject;
    public float ejectForce = 500f;

    [Header("References")]
    public Transform player;

    [Header("Events")]
    [Tooltip("Fired after the boss is defeated and NPCs are ejected. " +
             "Wire to music change, HUD message, win condition, etc.")]
    public UnityEngine.Events.UnityEvent onDefeated;

    [Tooltip("Optional — assign a SectionTracker to auto-register the ejected NPCs " +
             "so the section completes once they are all converted.")]
    public SectionTracker bossPhaseTracker;

    private Animator animator;
    private Collider bossCollider;
    private float stateEndTime = 0f;
    private float nextAttackTime = 0f;
    private bool isDoingBiteAttack = false;

    private void Start()
    {
        CurrentLove = 0;
        animator = GetComponent<Animator>();
        bossCollider = GetComponent<Collider>();

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
                Debug.Log("[WatcherAI] Bite attack!");
            }
            else
            {
                if (animator != null) animator.SetTrigger("Attack1");
                FireProjectile();
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
        Debug.Log($"[WatcherAI] Received {loveAmount} love (total: {CurrentLove}/{loveNeededToConvert})");

        if (CurrentLove >= loveNeededToConvert)
        {
            BecomeHappy();
            return;
        }

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
        if (animator != null) animator.SetTrigger("Die");
        if (bossCollider != null) bossCollider.enabled = false;

        Debug.Log("[WatcherAI] Converted! Ejecting NPCs.");

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