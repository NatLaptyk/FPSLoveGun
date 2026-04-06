using UnityEngine;

/// <summary>
/// Love Bomb Thrower — secondary weapon.
/// Press G to throw a love bomb (area effect for very unhappy people).
/// Attach this to the Player GameObject.
/// </summary>
public class LoveBombThrower : MonoBehaviour
{
    [Header("Love Bomb")]
    public GameObject loveBombPrefab;    // Assign LoveBombProjectile prefab
    public Transform throwPoint;          // Camera transform or a child of camera
    public float throwForce = 15f;
    public float upwardForce = 3f;        // Slight upward arc

    [Header("Inventory")]
    public int maxBombs = 3;
    public int currentBombs = 3;

    [Header("Audio")]
    public AudioClip throwSound;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // Throw Love Bomb with G key
        if (Input.GetKeyDown(KeyCode.G) && currentBombs > 0)
        {
            ThrowBomb();
        }
    }

    void ThrowBomb()
    {
        currentBombs--;

        // Spawn bomb slightly in front and above the player
        GameObject bomb = Instantiate(loveBombPrefab, throwPoint.position + throwPoint.forward * 0.5f, throwPoint.rotation);
        Rigidbody rb = bomb.GetComponent<Rigidbody>();

        // Throw forward with a slight arc
        Vector3 throwDirection = throwPoint.forward * throwForce + Vector3.up * upwardForce;
        rb.linearVelocity = throwDirection;

        // Sound
        if (throwSound != null)
            audioSource.PlayOneShot(throwSound);

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateBombs(currentBombs, maxBombs);
    }

    /// <summary>
    /// Called by bomb pickups to add bombs.
    /// </summary>
    public void AddBombs(int amount)
    {
        currentBombs = Mathf.Min(currentBombs + amount, maxBombs);
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateBombs(currentBombs, maxBombs);
    }
}
