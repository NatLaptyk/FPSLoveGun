using UnityEngine;

/// <summary>
/// Love Projectile — fired by the Love Gun.
/// On hitting an UnhappyPerson or WatcherAI, it adds love to them.
/// Uses a per-frame raycast to prevent tunneling through fast-moving colliders.
/// </summary>
public class LoveProjectile : MonoBehaviour
{
    [Header("Settings")]
    public int lovePower = 1;           // How much love each shot gives
    public float lifetime = 5f;         // Auto-destroy after this time
    public GameObject hitEffect;        // Pink heart particle burst on impact

    private Vector3 previousPosition;

    void Start()
    {
        // Auto-destroy after lifetime to prevent clutter
        Destroy(gameObject, lifetime);
        previousPosition = transform.position;

        // Point-blank fix: if we spawned overlapping a target,
        // OnTriggerEnter will never fire. Detect it manually here.
        Collider myCol = GetComponent<Collider>();
        float radius = 0.5f;
        if (myCol is SphereCollider sc) radius = sc.radius * transform.lossyScale.x;
        else if (myCol != null) radius = myCol.bounds.extents.magnitude;

        Collider[] overlaps = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider col in overlaps)
        {
            if (TryApplyLove(col))
            {
                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }
        }
    }

    void Update()
    {
        // Raycast from previous position to current position each frame.
        // This catches hits that OnTriggerEnter misses due to tunneling.
        Vector3 currentPosition = transform.position;
        Vector3 direction = currentPosition - previousPosition;
        float distance = direction.magnitude;

        if (distance > 0.01f)
        {
            // QueryTriggerInteraction.Collide ensures we detect trigger colliders (like the Watcher's)
            if (Physics.SphereCast(previousPosition, 0.3f, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Collide))
            {
                // Skip player and enemy projectiles
                Transform root = hit.collider.transform.root;
                if (hit.collider.CompareTag("Player") || root.CompareTag("Player"))
                {
                    // ignore
                }
                else if (hit.collider.GetComponent<SadnessProjectile>() != null)
                {
                    // ignore enemy projectiles
                }
                else if (TryApplyLove(hit.collider))
                {
                    transform.position = hit.point;
                    SpawnHitEffect();
                    Destroy(gameObject);
                    return;
                }
                else if (!hit.collider.CompareTag("PlayerProjectile") && !root.CompareTag("PlayerProjectile"))
                {
                    // Hit a wall or environment
                    Destroy(gameObject);
                    return;
                }
            }
        }

        previousPosition = currentPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[LoveProjectile] Hit: {other.name} (root: {other.transform.root.name})");

        if (TryApplyLove(other))
        {
            SpawnHitEffect();
            Destroy(gameObject);
            return;
        }

        // Ignore enemy projectiles — don't let them eat our shots mid-air
        if (other.GetComponent<SadnessProjectile>() != null) return;

        // Hit a wall or ground — destroy projectile.
        Transform root = other.transform.root;
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerProjectile")
            && !root.CompareTag("Player") && !root.CompareTag("PlayerProjectile"))
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Checks if the collider belongs to an UnhappyPerson or WatcherAI and applies love.
    /// Returns true if love was applied (so the projectile should be destroyed).
    /// </summary>
    bool TryApplyLove(Collider col)
    {
        UnhappyPerson person = col.GetComponentInParent<UnhappyPerson>();
        if (person != null)
        {
            person.ReceiveLove(lovePower);
            Debug.Log($"[LoveProjectile] Loved {person.name}!");
            return true;
        }

        WatcherAI boss = col.GetComponentInParent<WatcherAI>();
        if (boss != null)
        {
            boss.ReceiveLove(lovePower, false);
            Debug.Log($"[LoveProjectile] Hit Watcher! Love: {boss.CurrentLove}/{boss.loveNeededToConvert}");
            return true;
        }

        return false;
    }

    void SpawnHitEffect()
    {
        if (hitEffect != null)
            Instantiate(hitEffect, transform.position, Quaternion.identity);
    }
}
