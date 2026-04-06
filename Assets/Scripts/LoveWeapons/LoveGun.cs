using UnityEngine;
using System.Collections;

/// <summary>
/// Love Gun — the player's primary weapon.
/// Left-click to shoot love projectiles at unhappy people.
/// Attach this to the Gun object (child of the Camera).
/// </summary>
public class LoveGun : MonoBehaviour
{
    [Header("Shooting")]
    public GameObject loveProjectilePrefab; // Assign LoveProjectile prefab
    public Transform firePoint;             // Empty GameObject at the gun barrel tip
    public float fireRate = 0.3f;           // Seconds between shots
    public float projectileSpeed = 30f;

    [Header("Ammo")]
    public int maxAmmo = 30;
    public int currentAmmo = 30;
    public float reloadTime = 1.5f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Visual Feedback")]
    public ParticleSystem muzzleFlash; // Pink/heart particle effect at fire point

    private float nextFireTime = 0f;
    private bool isReloading = false;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (isReloading) return;

        // Reload with R key
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
        {
            StartCoroutine(Reload());
            return;
        }

        // Shoot with Left Mouse Button
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
            }
            else
            {
                // Play empty click sound
                if (emptySound != null)
                    audioSource.PlayOneShot(emptySound);

                // Auto-reload when empty
                StartCoroutine(Reload());
            }
        }
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        // Spawn projectile
        GameObject projectile = Instantiate(loveProjectilePrefab, firePoint.position, firePoint.rotation);
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        rb.linearVelocity = firePoint.forward * projectileSpeed;

        // Visual effect
        if (muzzleFlash != null)
            muzzleFlash.Play();

        // Sound
        if (shootSound != null)
            audioSource.PlayOneShot(shootSound);

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateAmmo(currentAmmo, maxAmmo);
    }

    IEnumerator Reload()
    {
        isReloading = true;

        if (reloadSound != null)
            audioSource.PlayOneShot(reloadSound);

        // Update HUD to show reloading
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowReloading(true);

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;

        if (hud != null)
        {
            hud.ShowReloading(false);
            hud.UpdateAmmo(currentAmmo, maxAmmo);
        }
    }

    /// <summary>
    /// Called by ammo pickups to add ammo.
    /// </summary>
    public void AddAmmo(int amount)
    {
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateAmmo(currentAmmo, maxAmmo);
    }
}
