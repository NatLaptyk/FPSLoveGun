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

        // Aim from the camera center (where the crosshair is), not from the gun.
        // This makes projectiles go exactly where the crosshair points.
        Camera cam = Camera.main;
        Vector3 aimDirection;
        if (cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Use whatever the crosshair is over as the target point,
            // or a far point straight ahead if nothing is in range.
            Vector3 targetPoint = Physics.Raycast(centerRay, out RaycastHit hit, 200f)
                ? hit.point
                : centerRay.origin + centerRay.direction * 200f;

            aimDirection = (targetPoint - firePoint.position).normalized;
        }
        else
        {
            aimDirection = firePoint.forward;
        }

        // Spawn projectile at gun barrel, rotated toward the crosshair target
        GameObject projectile = Instantiate(loveProjectilePrefab, firePoint.position, Quaternion.LookRotation(aimDirection));
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        rb.linearVelocity = aimDirection * projectileSpeed;

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
