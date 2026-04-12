using UnityEngine;

/// <summary>
/// Defines a strict contract for entities that can receive love within the game ecosystem.
/// </summary>
public interface ILovable<T>
{
    void ReceiveLove(int loveAmount, T sourceModifier);
}

/// <summary>
/// Controls the behavior, state machine, and combat logic for the Watcher flying boss.
/// Moves freely in 3D space — does NOT use NavMeshAgent (flying enemy, not ground-based).
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
    public GameObject npcPrefab;
    public int npcsToEject = 5;
    public float ejectForce = 500f;

    [Header("References")]
    public Transform player;

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

        // Disable the Rigidbody if one exists — flying enemies don't need physics.
        // The projectile's Rigidbody is enough for OnTriggerEnter to work.
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

        // Always correct Y toward hover height, regardless of state
        float targetY = GetHoverTargetY();
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * hoverSpeed);
        transform.position = pos;

        switch (CurrentState)
        {
            case BossState.Idle:     HandleIdle();      break;
            case BossState.Chasing:  HandleChasing();   break;
            case BossState.Attacking:HandleAttacking(); break;
            case BossState.Stunned:  HandleStunned();   break;
        }
    }

    /// <summary>
    /// Returns the target Y position the Watcher should hover at.
    /// Raycasts down to find the ground surface.
    /// </summary>
    private float GetHoverTargetY()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y + hoverHeight;

        return hoverHeight; // fallback
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

        // Face the player
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
            // Fly toward player horizontally — Y is handled by hover above
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

            stateEndTime = Time.time + attackAnimationLength;
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

    private void HandleStunned()
    {
        if (animator != null) animator.SetFloat("Speed", 0f);

        if (Time.time >= stateEndTime)
        {
            CurrentState = BossState.Chasing;
        }
    }

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

        if (npcPrefab != null)
        {
            for (int i = 0; i < npcsToEject; i++)
            {
                GameObject npc = Instantiate(npcPrefab, transform.position + Vector3.up * 2f, Random.rotation);
                if (npc.TryGetComponent(out Rigidbody rb))
                    rb.AddExplosionForce(ejectForce, transform.position, 10f, 3f);
                if (npc.TryGetComponent(out UnhappyPerson person))
                    person.ReceiveLove(999);
            }
        }

        Debug.Log("[WatcherAI] Converted! Boss defeated.");
        Destroy(gameObject, 8f);
    }
}
