using UnityEngine;
using System.Collections;

/// <summary>
/// Love Gun — the player's primary weapon.
/// Left-click to shoot. Auto-reloads from reserve when magazine empties.
/// Reserve ammo comes from pickups — no cap. Empty click only when truly out.
///
/// Ammo model:
///   currentAmmo  — shots left in the current magazine (0 – maxAmmo)
///   reserveAmmo  — shots stored from pickups (no upper limit)
///
/// Reload draws up to maxAmmo shots from the reserve into the magazine.
/// Empty click plays only when magazine AND reserve are both zero.
/// </summary>
public class LoveGun : MonoBehaviour
{
    [Header("Shooting")]
    public GameObject loveProjectilePrefab;
    public Transform  firePoint;
    public float      fireRate       = 0.3f;
    public float      projectileSpeed = 30f;

    [Header("Ammo")]
    [Tooltip("Magazine size — how many shots per reload.")]
    public int maxAmmo     = 30;
    [Tooltip("Shots currently in the magazine.")]
    public int currentAmmo = 30;
    [Tooltip("Reserve shots accumulated from pickups. No upper limit.")]
    public int reserveAmmo = 0;
    public float reloadTime = 1.5f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Visual Feedback")]
    public ParticleSystem muzzleFlash;

    private float      nextFireTime = 0f;
    private bool       isReloading  = false;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        UpdateHUD();
    }

    void Update()
    {
        if (isReloading) return;

        // Manual reload with R — only useful if magazine isn't full and reserve has ammo
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo && reserveAmmo > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
            }
            else if (reserveAmmo > 0)
            {
                // Magazine empty but reserve available — auto-reload
                StartCoroutine(Reload());
            }
            else
            {
                // Truly out of ammo — click once per trigger press
                if (Input.GetButtonDown("Fire1") && emptySound != null)
                    audioSource.PlayOneShot(emptySound);
            }
        }
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        // Aim from camera centre so projectiles go exactly where the crosshair points
        Camera cam = Camera.main;
        Vector3 aimDirection;
        if (cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint = Physics.Raycast(centerRay, out RaycastHit hit, 200f)
                ? hit.point
                : centerRay.origin + centerRay.direction * 200f;
            aimDirection = (targetPoint - firePoint.position).normalized;
        }
        else
        {
            aimDirection = firePoint.forward;
        }

        GameObject projectile = Instantiate(loveProjectilePrefab, firePoint.position,
                                            Quaternion.LookRotation(aimDirection));
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = aimDirection * projectileSpeed;

        if (muzzleFlash != null) muzzleFlash.Play();
        if (shootSound  != null) audioSource.PlayOneShot(shootSound);

        UpdateHUD();
    }

    IEnumerator Reload()
    {
        isReloading = true;

        if (reloadSound != null) audioSource.PlayOneShot(reloadSound);

        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowReloading(true);

        yield return new WaitForSeconds(reloadTime);

        // Draw enough shots from reserve to fill the magazine
        int needed = maxAmmo - currentAmmo;
        int drawn  = Mathf.Min(needed, reserveAmmo);
        currentAmmo += drawn;
        reserveAmmo -= drawn;

        isReloading = false;

        if (hud != null) hud.ShowReloading(false);
        UpdateHUD();

        Debug.Log($"[LoveGun] Reloaded. Magazine: {currentAmmo}/{maxAmmo}  Reserve: {reserveAmmo}");
    }

    /// <summary>
    /// Called by ammo pickups. Adds directly to reserve — no upper limit.
    /// </summary>
    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        UpdateHUD();
        Debug.Log($"[LoveGun] Picked up {amount} ammo. Magazine: {currentAmmo}  Reserve: {reserveAmmo}");
    }

    void UpdateHUD()
    {
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        // Show  currentAmmo / reserveAmmo  so the player always knows both counts
        if (hud != null) hud.UpdateAmmo(currentAmmo, reserveAmmo);
    }
}
