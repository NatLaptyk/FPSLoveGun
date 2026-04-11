using UnityEngine;

/// <summary>
/// Defines a strict contract for entities that can receive love within the game ecosystem.
/// </summary>
/// <typeparam name="T">The data type used to identify the source or type of love (e.g., is from bomb).</typeparam>
public interface ILovable<T>
{
    /// <summary>
    /// Applies love to the implementing entity.
    /// </summary>
    /// <param name="loveAmount">The raw integer love to apply.</param>
    /// <param name="sourceModifier">An identifier representing the attack type, matching <typeparamref name="T"/>.</param>
    void ReceiveLove(int loveAmount, T sourceModifier);
}

/// <include file='ExternalDocs.xml' path='docs/members[@name="BossAI"]/*' />
/// <summary>
/// Controls the behavior, state machine, and combat logic for the giant flying eye boss.
/// </summary>
/// <remarks>
/// <para>
/// This component utilizes internal <c>Time.time</c> trackers to ensure zero garbage collection.
/// The boss now calculates distance to determine whether to use a projectile or bite attack, 
/// takes extra love while stunned, and spawns saved NPCs upon conversion.
/// </para>
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

    [Header("Boss Stats (Happiness)")]
    public int loveNeededToConvert = 500;
    public float runSpeed = 6f;
    [Tooltip("Multiplier applied to love received while the boss is stunned.")]
    public int stunnedLoveMultiplier = 2;
    public float stunDuration = 3f;

    [Header("Attack Ranges")]
    [Tooltip("If the player is within this range, the boss will use the physical bite attack.")]
    public float biteAttackRange = 4f;
    [Tooltip("If the player is outside bite range but inside this range, the boss fires projectiles.")]
    public float projectileAttackRange = 20f;
    public float attackCooldown = 3f;
    public float attackAnimationLength = 1.5f;

    [Header("Projectile Settings")]
    public GameObject bossProjectilePrefab;
    public Transform eyeFirePoint;
    public float projectileForce = 20f;
    [Tooltip("How high above the player's pivot to aim. Increase this if it hits the floor.")]
    public float playerAimOffsetY = 1.5f;

    [Header("Defeat / Saved NPCs")]
    public GameObject npcPrefab;
    public int npcsToEject = 5;
    public float ejectForce = 500f;

    [Header("References")]
    public Transform player;

    private Animator animator;
    private Collider bossCollider;

    // Internal GC-Free Timers
    private float stateEndTime = 0f;
    private float nextAttackTime = 0f;
    private bool isDoingBiteAttack = false;

    /// <summary>
    /// Initializes required component references and caches necessary data.
    /// </summary>
    private void Start()
    {
        CurrentLove = 0;
        animator = GetRequiredComponent<Animator>(true);
        bossCollider = GetRequiredComponent<Collider>(false);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    /// <summary>
    /// Executes the AI logic on a frame-by-frame basis based on the <see cref="CurrentState"/>.
    /// </summary>
    private void Update()
    {
        if (CurrentState == BossState.Converted) return;

        switch (CurrentState)
        {
            case BossState.Idle:
                HandleIdle();
                break;
            case BossState.Chasing:
                HandleChasing();
                break;
            case BossState.Attacking:
                HandleAttacking();
                break;
            case BossState.Stunned:
                HandleStunned();
                break;
        }
    }

    /// <summary>
    /// Manages the resting state until the player enters the aggro radius.
    /// </summary>
    private void HandleIdle()
    {
        animator.SetFloat("Speed", 0f);
        if ((transform.position - player.position).sqrMagnitude < 900f) // 30 units squared
        {
            CurrentState = BossState.Chasing;
        }
    }

    /// <summary>
    /// Updates the boss's position and determines which attack to use based on distance.
    /// </summary>
    private void HandleChasing()
    {
        float sqrDistance = (transform.position - player.position).sqrMagnitude;
        float sqrBiteRange = biteAttackRange * biteAttackRange;
        float sqrProjRange = projectileAttackRange * projectileAttackRange;

        Vector3 direction = (player.position - transform.position).normalized;

        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (sqrDistance <= sqrBiteRange)
        {
            CurrentState = BossState.Attacking;
            isDoingBiteAttack = true;
            animator.SetFloat("Speed", 0f);
        }
        else if (sqrDistance <= sqrProjRange)
        {
            CurrentState = BossState.Attacking;
            isDoingBiteAttack = false;
            animator.SetFloat("Speed", 0f);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, player.position, runSpeed * Time.deltaTime);
            animator.SetFloat("Speed", runSpeed);
        }
    }

    /// <summary>
    /// Triggers the appropriate attack logic based on the distance calculated in <see cref="HandleChasing"/>.
    /// </summary>
    private void HandleAttacking()
    {
        if (Time.time >= nextAttackTime)
        {
            if (isDoingBiteAttack)
            {
                animator.SetTrigger("Attack2"); // Assuming Attack2 is the Bite
            }
            else
            {
                animator.SetTrigger("Attack1"); // Assuming Attack1 is the Eye Projectile
                FireProjectile();
            }

            stateEndTime = Time.time + attackAnimationLength;
            nextAttackTime = Time.time + attackCooldown;
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

        // Now using playerAimOffsetY to aim at the chest/head!
        Vector3 targetPos = player.position + (Vector3.up * playerAimOffsetY);
        Vector3 dirToPlayer = (targetPos - eyeFirePoint.position).normalized;

        GameObject proj = Instantiate(bossProjectilePrefab, eyeFirePoint.position, Quaternion.LookRotation(dirToPlayer));

        if (proj.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = dirToPlayer * projectileForce;
        }
    }

    /// <summary>
    /// Handles the internal timer for recovering from the stunned state.
    /// </summary>
    private void HandleStunned()
    {
        if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
        }
    }

    /// <inheritdoc/>
    public void ReceiveLove(int loveAmount, bool isFromBomb)
    {
        if (CurrentState == BossState.Converted) return;

        // Apply stun vulnerability multiplier if it's a regular shot while stunned
        if (CurrentState == BossState.Stunned && !isFromBomb)
        {
            loveAmount *= stunnedLoveMultiplier;
        }

        CurrentLove += loveAmount;

        if (CurrentLove >= loveNeededToConvert)
        {
            BecomeHappy();
            return;
        }

        if (isFromBomb)
        {
            CurrentState = BossState.Stunned;
            stateEndTime = Time.time + stunDuration;
            animator.SetTrigger("Stun");
        }
        else if (CurrentState != BossState.Stunned)
        {
            animator.SetTrigger("Hit");
        }
    }

    /// <summary>
    /// Processes the boss's conversion sequence and explodes saved NPCs outward.
    /// </summary>
    private void BecomeHappy()
    {
        CurrentState = BossState.Converted;
        animator.SetTrigger("Die"); // Using your death animation as the defeat/collapse

        if (bossCollider != null)
            bossCollider.enabled = false;

        // Eject the saved NPCs
        if (npcPrefab != null)
        {
            for (int i = 0; i < npcsToEject; i++)
            {
                GameObject npc = Instantiate(npcPrefab, transform.position + (Vector3.up * 2f), Random.rotation);

                // Add physical explosion force
                if (npc.TryGetComponent(out Rigidbody rb))
                {
                    rb.AddExplosionForce(ejectForce, transform.position, 10f, 3f);
                }

                // Immediately convert them so they spawn happy
                if (npc.TryGetComponent(out UnhappyPerson person))
                {
                    person.ReceiveLove(999);
                }
            }
        }

        Destroy(gameObject, 8f);
    }

    /// <summary>
    /// Retrieves a component securely using <c>TryGetComponent</c>, with an option to force an error.
    /// </summary>
    private TComponent GetRequiredComponent<TComponent>(bool throwOnFail) where TComponent : Component
    {
        if (TryGetComponent(out TComponent comp)) return comp;
        if (throwOnFail) throw new MissingComponentException($"Missing {typeof(TComponent).Name} on Boss.");
        return null;
    }
}