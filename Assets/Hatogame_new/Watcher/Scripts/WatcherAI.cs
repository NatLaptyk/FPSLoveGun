using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Defines a strict contract for entities that can receive love within the game ecosystem.
/// </summary>
/// <typeparam name="T">The data type used to identify the source or type of love modifier.</typeparam>
public interface ILovable<T>
{
    /// <summary>
    /// Applies love to the implementing entity.
    /// </summary>
    /// <param name="loveAmount">The raw integer love to apply.</param>
    /// <param name="sourceModifier">An identifier representing the attack type, matching <typeparamref name="T"/>.</param>
    void ReceiveLove(int loveAmount, T sourceModifier);
}

/// <summary>
/// A configuration container that defines an NPC prefab and how many of them to spawn.
/// </summary>
/// <remarks>
/// <para>Marked as <c>[Serializable]</c> so it can be configured directly as an array in the Unity Inspector.</para>
/// </remarks>
[Serializable]
public struct SavedNPCConfig
{
    /// <summary>
    /// The specific NPC Prefab to instantiate.
    /// </summary>
    [Tooltip("The specific NPC Prefab to instantiate.")]
    public GameObject npcPrefab;

    /// <summary>
    /// The exact number of this specific prefab to spawn when the boss is defeated.
    /// </summary>
    [Tooltip("The exact number of this specific prefab to spawn when the boss is defeated.")]
    public int spawnCount;
}

/// <summary>
/// Controls the behavior, state machine, and combat logic for the Watcher flying boss.
/// </summary>
/// <remarks>
/// <para>Moves freely in 3D space — does NOT use <see cref="UnityEngine.AI.NavMeshAgent"/> (flying enemy, not ground-based).</para>
/// </remarks>
public class WatcherAI : MonoBehaviour, ILovable<bool>
{
    /// <summary>
    /// Represents the various behavioral states of the boss.
    /// </summary>
    public enum BossState { Idle, Chasing, Attacking, Stunned, Converted }

    /// <summary>
    /// Gets the current operational state of the AI.
    /// </summary>
    /// <value>A <see cref="BossState"/> enum representing what the AI is currently executing.</value>
    public BossState CurrentState { get; private set; } = BossState.Idle;

    /// <summary>
    /// Gets the current love received by the boss.
    /// </summary>
    /// <value>An integer representing the current love points. Hits max upon conversion.</value>
    public int CurrentLove { get; private set; }

    [Header("Boss Stats")]
    /// <summary>Total amount of love required to defeat the boss.</summary>
    public int loveNeededToConvert = 10;
    /// <summary>Movement speed while pursuing the player.</summary>
    public float runSpeed = 4f;
    /// <summary>Multiplier applied to love received while the boss is stunned.</summary>
    [Tooltip("Multiplier applied to love received while stunned.")]
    public int stunnedLoveMultiplier = 2;
    /// <summary>Duration in seconds the boss remains stunned after a bomb hit.</summary>
    public float stunDuration = 3f;

    [Header("Flight")]
    /// <summary>Height the Watcher hovers above the ground.</summary>
    [Tooltip("Height the Watcher hovers above the ground.")]
    public float hoverHeight = 3f;
    /// <summary>How quickly the Watcher adjusts its hover height.</summary>
    [Tooltip("How quickly the Watcher adjusts its hover height.")]
    public float hoverSpeed = 3f;

    [Header("Detection")]
    /// <summary>Distance at which the boss will spot the player and begin chasing.</summary>
    public float aggroRange = 30f;

    [Header("Attack Ranges")]
    /// <summary>Distance required to trigger the physical bite attack.</summary>
    public float biteAttackRange = 4f;
    /// <summary>Distance required to trigger the projectile attack.</summary>
    public float projectileAttackRange = 20f;
    /// <summary>Time in seconds between boss attacks.</summary>
    public float attackCooldown = 3f;
    /// <summary>Duration to wait for the attack animation to complete.</summary>
    public float attackAnimationLength = 1.5f;

    [Header("Projectile Settings")]
    /// <summary>The prefab fired during the projectile attack.</summary>
    public GameObject bossProjectilePrefab;
    /// <summary>The transform position where projectiles spawn.</summary>
    public Transform eyeFirePoint;
    /// <summary>The physics velocity applied to the fired projectile.</summary>
    public float projectileForce = 20f;
    /// <summary>Vertical offset applied to the player's position when aiming.</summary>
    [Tooltip("How high above the player's pivot to aim.")]
    public float playerAimOffsetY = 1.5f;

    [Header("Defeat / Saved NPCs")]
    /// <summary>A configurable list of different NPC prefabs and the exact amount to spawn upon defeat.</summary>
    [Tooltip("Add different NPC prefabs here and specify how many of each should explode out of the boss.")]
    public SavedNPCConfig[] savedNPCsToEject;
    /// <summary>The explosive force applied to the NPCs when they are ejected.</summary>
    public float ejectForce = 500f;

    [Header("References")]
    /// <summary>Reference to the player's transform.</summary>
    public Transform player;

    private Animator animator;
    private Collider bossCollider;

    private float stateEndTime = 0f;
    private float nextAttackTime = 0f;
    private bool isDoingBiteAttack = false;

    /// <summary>
    /// Initializes required components and verifies physics setup.
    /// </summary>
    private void Start()
    {
        CurrentLove = 0;
        animator = GetComponent<Animator>();
        bossCollider = GetComponent<Collider>();

        // Disable the Rigidbody if one exists — flying enemies don't need physics.
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

    /// <summary>
    /// Executes the AI state machine and manages hover logic frame-by-frame.
    /// </summary>
    private void Update()
    {
        if (CurrentState == BossState.Converted || player == null) return;

        // Always correct Y toward hover height, regardless of state
        float targetY = GetHoverTargetY();
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * hoverSpeed);
        transform.position = pos;

        switch (CurrentState)
        {
            case BossState.Idle: HandleIdle(); break;
            case BossState.Chasing: HandleChasing(); break;
            case BossState.Attacking: HandleAttacking(); break;
            case BossState.Stunned: HandleStunned(); break;
        }
    }

    /// <summary>
    /// Calculates the target Y position the Watcher should hover at using a downward raycast.
    /// </summary>
    /// <returns>A float representing the world Y position to hover at.</returns>
    private float GetHoverTargetY()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y + hoverHeight;

        return hoverHeight; // fallback
    }

    /// <summary>
    /// Manages the resting state until the player enters the aggro radius.
    /// </summary>
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

    /// <summary>
    /// Updates position to pursue the player and determines which attack to use based on distance.
    /// </summary>
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

    /// <summary>
    /// Triggers the attack logic and manages the cooldown timer.
    /// </summary>
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

            stateEndTime = Time.time + attackAnimationLength;
            nextAttackTime = Time.time + attackCooldown;

            if (animator != null) animator.SetFloat("Speed", 0f);
        }
        else if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
        }
    }

    /// <summary>
    /// Instantiates and fires the sadness projectile toward the player.
    /// </summary>
    private void FireProjectile()
    {
        if (bossProjectilePrefab == null || eyeFirePoint == null) return;

        Vector3 targetPos = player.position + (Vector3.up * playerAimOffsetY);
        Vector3 dirToPlayer = (targetPos - eyeFirePoint.position).normalized;

        GameObject proj = Instantiate(bossProjectilePrefab, eyeFirePoint.position, Quaternion.LookRotation(dirToPlayer));

        SadnessProjectile sp = proj.GetComponent<SadnessProjectile>();
        if (sp != null) sp.owner = transform;

        if (proj.TryGetComponent(out Rigidbody rb))
        {
            rb.useGravity = false;
            rb.linearVelocity = dirToPlayer * projectileForce;
        }

        Debug.Log($"[WatcherAI] Fired projectile toward {targetPos}, dir={dirToPlayer}");
    }

    /// <summary>
    /// Handles the internal timer for recovering from the stunned state.
    /// </summary>
    private void HandleStunned()
    {
        if (animator != null) animator.SetFloat("Speed", 0f);

        if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
        }
    }

    /// <summary>
    /// Smoothly rotates the boss to face the player along the Y axis.
    /// </summary>
    private void FacePlayer()
    {
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// Processes the boss's conversion sequence and explodes the configurable list of saved NPCs outward.
    /// </summary>
    private void BecomeHappy()
    {
        CurrentState = BossState.Converted;
        if (animator != null) animator.SetTrigger("Die");
        if (bossCollider != null) bossCollider.enabled = false;

        // Loop through the configured list of NPCs to eject
        if (savedNPCsToEject != null)
        {
            for (int i = 0; i < savedNPCsToEject.Length; i++)
            {
                // Loop based on how many of this specific prefab we want
                for (int j = 0; j < savedNPCsToEject[i].spawnCount; j++)
                {
                    if (savedNPCsToEject[i].npcPrefab != null)
                    {
                        GameObject npc = Instantiate(savedNPCsToEject[i].npcPrefab, transform.position + (Vector3.up * 2f), Random.rotation);

                        if (npc.TryGetComponent(out Rigidbody rb))
                            rb.AddExplosionForce(ejectForce, transform.position, 10f, 3f);

                        if (npc.TryGetComponent(out UnhappyPerson person))
                            person.ReceiveLove(999);
                    }
                }
            }
        }

        Debug.Log("[WatcherAI] Converted! Boss defeated.");
        Destroy(gameObject, 8f);
    }
}