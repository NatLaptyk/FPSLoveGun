using UnityEngine;

// Love Bomb Thrower — secondary weapon.
// Press G to throw a love bomb (area effect for very unhappy people).

public class LoveBombThrower : MonoBehaviour
{
    [Header("Love Bomb")]
    public GameObject loveBombPrefab;    // Assign LoveBombProjectile prefab
    public Transform throwPoint;          // Camera transform or a child of camera
    public float throwForce = 8f;         // Lowered so the bomb is easier to see in flight
    public float upwardForce = 4f;        // Slight upward arc

    [Header("Visual Trail")]
    [Tooltip("Automatically attach a trail to the bomb for visibility")]
    public bool addTrail = true;
    public Color trailColor = new Color(1f, 0.4f, 0.7f, 1f); // Pink
    public float trailTime = 0.6f;
    public float trailStartWidth = 0.4f;
    public float trailEndWidth = 0.05f;

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

        // Attach a trail renderer so the bomb is easy to follow visually
        if (addTrail)
            AttachTrail(bomb);

        // Sound
        if (throwSound != null)
            audioSource.PlayOneShot(throwSound);

        // Update HUD
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateBombs(currentBombs, maxBombs);
    }

    void AttachTrail(GameObject bomb)
    {
        TrailRenderer trail = bomb.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = bomb.AddComponent<TrailRenderer>();

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;

        // Simple unlit material so it shows up without needing URP/HDRP shaders
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = trailColor;
        trail.material = mat;
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

  
    // Called by bomb pickups to add bombs.
    
    public void AddBombs(int amount)
    {
        currentBombs = Mathf.Min(currentBombs + amount, maxBombs);
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.UpdateBombs(currentBombs, maxBombs);
    }
}
