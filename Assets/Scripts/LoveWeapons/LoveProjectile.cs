using UnityEngine;

/// <summary>
/// Love Projectile — fired by the Love Gun.
/// On hitting an UnhappyPerson, it adds love to them.
/// Create a Prefab: small pink/red sphere with a Rigidbody, Collider (Is Trigger), and this script.
/// </summary>
public class LoveProjectile : MonoBehaviour
{
    [Header("Settings")]
    public int lovePower = 1;           // How much love each shot gives
    public float lifetime = 5f;         // Auto-destroy after this time
    public GameObject hitEffect;        // Pink heart particle burst on impact

    void Start()
    {
        // Auto-destroy after lifetime to prevent clutter
        Destroy(gameObject, lifetime);

        // Point-blank fix: if we spawned overlapping an UnhappyPerson,
        // OnTriggerEnter will never fire. Detect it manually here.
        Collider myCol = GetComponent<Collider>();
        float radius = 0.5f;
        if (myCol is SphereCollider sc) radius = sc.radius * transform.lossyScale.x;
        else if (myCol != null) radius = myCol.bounds.extents.magnitude;

        Collider[] overlaps = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider col in overlaps)
        {
            UnhappyPerson person = col.GetComponentInParent<UnhappyPerson>();
            if (person != null)
            {
                person.ReceiveLove(lovePower);
                if (hitEffect != null)
                    Instantiate(hitEffect, transform.position, Quaternion.identity);
                Destroy(gameObject);
                return;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Did we hit an unhappy person?
        Debug.Log($"[LoveProjectile] Hit: {other.name} (root: {other.transform.root.name})");
        UnhappyPerson person = other.GetComponentInParent<UnhappyPerson>();
        if (person != null)
        {
            person.ReceiveLove(lovePower);

            // Spawn hit effect
            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
            return;
        }

        // Hit a wall or ground (not the player) — check the root too,
        // because child colliders (like the player's Cube) are often untagged.
        Transform root = other.transform.root;
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerProjectile")
            && !root.CompareTag("Player") && !root.CompareTag("PlayerProjectile"))
        {
            // Spawn hit effect on walls too (smaller)
            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
