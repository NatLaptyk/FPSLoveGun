using UnityEngine;

/// <summary>
/// Love Bomb Projectile — thrown grenade-style.
/// Explodes on impact (or after a fuse timer), spreading love in an area.
/// Great for "very unhappy" people who need extra love.
/// Create a Prefab: a sphere/heart shape with Rigidbody (NOT kinematic), Collider, and this script.
/// </summary>
public class LoveBombProjectile : MonoBehaviour
{
    [Header("Explosion")]
    public float explosionRadius = 6f;      // How far the love spreads
    public int lovePower = 5;               // Much more love than a regular shot
    public float fuseTime = 3f;             // Explodes after this time if it hasn't hit anything
    public GameObject explosionEffect;      // Big pink/heart particle explosion

    [Header("Audio")]
    public AudioClip explosionSound;

    private bool hasExploded = false;

    void Start()
    {
        // Fuse timer — explodes after fuseTime seconds regardless
        Invoke(nameof(Explode), fuseTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Ignore the player and other player projectiles
        if (collision.collider.CompareTag("Player")) return;
        if (collision.collider.CompareTag("PlayerProjectile")) return;

        // Explode on first contact with anything else
        Explode();
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Find all UnhappyPerson NPCs within explosion radius.
        // Use GetComponentInParent so colliders on child meshes still work,
        // and de-duplicate in case an NPC has multiple colliders.
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        var converted = new System.Collections.Generic.HashSet<UnhappyPerson>();
        foreach (Collider col in hitColliders)
        {
            UnhappyPerson person = col.GetComponentInParent<UnhappyPerson>();
            if (person != null && converted.Add(person))
            {
                person.ReceiveLove(lovePower);
            }
        }

        // Spawn explosion effect
        if (explosionEffect != null)
        {
            GameObject fx = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(fx, 3f); // Clean up effect after 3 seconds
        }

        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        Destroy(gameObject);
    }

    // Visualize explosion radius in the editor (Scene view only)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.3f); // Pink
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
