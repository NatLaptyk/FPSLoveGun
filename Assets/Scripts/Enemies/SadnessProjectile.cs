using UnityEngine;

/// <summary>
/// Sadness Projectile — thrown by Unhappy People at the player.
/// Reduces the player's happiness on hit.
/// Create a Prefab: small dark blue/grey sphere with Rigidbody, Collider (Is Trigger), and this script.
/// </summary>
public class SadnessProjectile : MonoBehaviour
{
    [Header("Settings")]
    public int sadnessDamage = 10;       // How much happiness the player loses
    public float lifetime = 5f;
    public GameObject hitEffect;         // Dark/sad particle burst

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[SadnessProjectile] Hit: {other.name} (root: {other.transform.root.name}, tag={other.tag})");

        // Did we hit the player? Check the root too in case the collider
        // is on a child object (CharacterController cube, capsule, etc).
        Transform root = other.transform.root;
        bool hitPlayer = other.CompareTag("Player") || root.CompareTag("Player");

        if (hitPlayer)
        {
            // Look up the hierarchy so PlayerHealth on the root is found
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

        // Hit a wall or ground (not an NPC)
        if (!other.CompareTag("NPC") && !root.CompareTag("NPC"))
        {
            Destroy(gameObject);
        }
    }
}
