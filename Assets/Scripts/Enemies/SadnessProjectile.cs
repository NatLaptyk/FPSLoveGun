using UnityEngine;

/// <summary>
/// Sadness Projectile — thrown by Unhappy People or the Watcher boss at the player.
/// Reduces the player's happiness on hit.
/// Create a Prefab: small dark blue/grey sphere with Rigidbody, Collider (Is Trigger), and this script.
/// </summary>
public class SadnessProjectile : MonoBehaviour
{
    [Header("Settings")]
    public int sadnessDamage = 10;       // How much happiness the player loses
    public float lifetime = 5f;
    public GameObject hitEffect;         // Dark/sad particle burst

    /// <summary>
    /// The root Transform of whoever fired this projectile.
    /// Set by the shooter so the projectile ignores its own creator.
    /// </summary>
    [HideInInspector]
    public Transform owner;

    void Start()
    {
        Destroy(gameObject, lifetime);

        // Safety: if owner wasn't set, try to detect overlap with shooter
        // and ignore it by physics
        if (owner != null)
        {
            Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>();
            Collider myCol = GetComponent<Collider>();
            if (myCol != null)
            {
                foreach (Collider oc in ownerColliders)
                    Physics.IgnoreCollision(myCol, oc);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignore the shooter
        if (owner != null && other.transform.root == owner.root)
            return;

        Debug.Log($"[SadnessProjectile] Hit: {other.name} (root: {other.transform.root.name}, tag={other.tag})");

        // Did we hit the player?
        Transform root = other.transform.root;
        bool hitPlayer = other.CompareTag("Player") || root.CompareTag("Player");

        if (hitPlayer)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeSadness(sadnessDamage);
            }

            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
            return;
        }

        // Ignore other NPCs, Watchers, and the Final Boss — only destroy on walls/floor
        if (other.GetComponentInParent<UnhappyPerson>() != null) return;
        if (other.GetComponentInParent<WatcherAI>() != null) return;
        if (other.GetComponentInParent<FinalBossAI>() != null) return;

        // Hit a wall or ground
        Destroy(gameObject);
    }
}
