using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Unhappy Person NPC — the "enemies" in your love FPS.
/// They patrol between waypoints, throw sadness projectiles at the player,
/// and become happy when they receive enough love.
///
/// SETUP:
/// 1. Create a humanoid character (capsule placeholder is fine)
/// 2. Add a NavMeshAgent component
/// 3. Add this script
/// 4. Create empty GameObjects as patrol waypoints and assign them
/// 5. Set unhappinessLevel: 1-3 = normal (love gun works), 4-5 = very unhappy (needs love bomb)
/// </summary>
public class UnhappyPerson : MonoBehaviour
{
    public enum MoodState { Unhappy, Happy }

    [Header("Mood")]
    public int unhappinessLevel = 3;        // How much love needed to become happy (1-5)
    public bool isVeryUnhappy = false;       // If true, requires love bomb (resists normal shots)
    private int currentLoveReceived = 0;
    public MoodState currentMood = MoodState.Unhappy;

    [Header("Patrol")]
    public Transform[] patrolPoints;         // Assign waypoints in Inspector
    public float patrolSpeed = 2.5f;
    public float waitTimeAtPoint = 1f;       // Pause at each patrol point
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    [Header("Combat — Throwing Sadness")]
    public GameObject sadnessProjectilePrefab;  // Assign SadnessProjectile prefab
    public Transform throwPoint;                 // Empty child object where projectiles spawn
    public float detectionRange = 15f;           // How far they can see the player
    public float attackRange = 12f;              // Range at which they start throwing
    public float throwCooldown = 2f;             // Time between throws
    public float throwForce = 6f;                // Lowered so the sadness is easier to see in flight
    private float nextThrowTime = 0f;

    [Header("Sadness Trail")]
    [Tooltip("Attach a trail to thrown sadness so it's visible even at speed")]
    public bool addSadnessTrail = true;
    public Color sadnessTrailColor = new Color(0.2f, 0.2f, 0.6f, 1f); // Dark blue
    public float sadnessTrailTime = 0.5f;
    public float sadnessTrailStartWidth = 0.3f;
    public float sadnessTrailEndWidth = 0.02f;

    [Header("Visuals")]
    public Renderer bodyRenderer;               // To change color when becoming happy
    public Color unhappyColor = new Color(0.3f, 0.3f, 0.8f);   // Blue/sad
    public Color veryUnhappyColor = new Color(0.5f, 0.1f, 0.5f); // Purple/very sad
    public Color happyColor = new Color(1f, 0.9f, 0.2f);        // Yellow/happy
    public GameObject happyEffect;              // Particle effect when converted

    [Header("Audio")]
    public AudioClip sadSound;       // Periodic sad mumbling
    public AudioClip happySound;     // Plays when converted
    public AudioClip throwSound;

    [Header("UI Indicator")]
    public GameObject unhappyIndicator;   // Floating sad emoji/icon above head (optional)
    public GameObject happyIndicator;     // Floating happy emoji/icon (optional)

    private NavMeshAgent agent;
    private Transform playerTransform;
    private AudioSource audioSource;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        // Set initial visual
        UpdateVisuals();

        // Start patrolling
        if (patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update()
    {
        if (currentMood == MoodState.Happy)
        {
            // Happy people are controlled by HappyWanderRoutine + CityPeople animations.
            // Skip all the unhappy update logic.
            return;
        }

        float distanceToPlayer = playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : float.MaxValue;

        // If player is in detection range, face them and attack
        if (distanceToPlayer <= detectionRange && playerTransform != null)
        {
            // Look at the player
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0; // Keep upright
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 5f * Time.deltaTime);

            // Stop patrolling and face the player
            agent.isStopped = true;

            // Throw sadness if in attack range
            if (distanceToPlayer <= attackRange && Time.time >= nextThrowTime)
            {
                ThrowSadness();
            }
        }
        else
        {
            // Resume patrolling
            agent.isStopped = false;
            Patrol();
        }
    }

    void Patrol()
    {
        if (patrolPoints.Length == 0) return;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }
            return;
        }

        // Check if we've reached the current patrol point
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            waitTimer = waitTimeAtPoint;
        }
    }

    void ThrowSadness()
    {
        nextThrowTime = Time.time + throwCooldown;

        if (sadnessProjectilePrefab == null || throwPoint == null) return;

        // Calculate throw direction toward the player
        Vector3 directionToPlayer = (playerTransform.position + Vector3.up * 1.2f - throwPoint.position).normalized;

        GameObject projectile = Instantiate(sadnessProjectilePrefab, throwPoint.position, Quaternion.identity);

        // Tell the projectile who fired it so it ignores our collider
        SadnessProjectile sp = projectile.GetComponent<SadnessProjectile>();
        if (sp != null) sp.owner = transform;

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        rb.linearVelocity = directionToPlayer * throwForce;

        // Attach a visible trail so the player can see and dodge
        if (addSadnessTrail)
            AttachSadnessTrail(projectile);

        if (throwSound != null)
            audioSource.PlayOneShot(throwSound);
    }

    void AttachSadnessTrail(GameObject projectile)
    {
        TrailRenderer trail = projectile.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = projectile.AddComponent<TrailRenderer>();

        trail.time = sadnessTrailTime;
        trail.startWidth = sadnessTrailStartWidth;
        trail.endWidth = sadnessTrailEndWidth;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = sadnessTrailColor;
        trail.material = mat;
        trail.startColor = sadnessTrailColor;
        trail.endColor = new Color(sadnessTrailColor.r, sadnessTrailColor.g, sadnessTrailColor.b, 0f);
    }

    /// <summary>
    /// Called when this person is hit by a love projectile or love bomb.
    /// </summary>
    public void ReceiveLove(int amount)
    {
        if (currentMood == MoodState.Happy) return; // Already happy

        // Very unhappy people resist normal love shots (love power = 1)
        if (isVeryUnhappy && amount < 3)
        {
            // Show a "needs more love!" indicator or just absorb without effect
            // Small visual feedback — they flinch but don't convert
            Debug.Log(gameObject.name + " is very unhappy! Needs a Love Bomb!");
            return;
        }

        currentLoveReceived += amount;

        if (currentLoveReceived >= unhappinessLevel)
        {
            BecomeHappy();
        }
    }

    [Header("Happy Behavior")]
    [Tooltip("Speed at which happy people wander around")]
    public float happyWanderSpeed = 1.5f;
    [Tooltip("Radius around current position for happy wandering")]
    public float happyWanderRadius = 6f;
    [Tooltip("Seconds between picking new wander destinations")]
    public float happyWanderInterval = 5f;

    void BecomeHappy()
    {
        currentMood = MoodState.Happy;

        // Update visuals
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = happyColor;
        }

        // Show happy effect
        if (happyEffect != null)
            Instantiate(happyEffect, transform.position + Vector3.up, Quaternion.identity);

        // Swap indicators
        if (unhappyIndicator != null) unhappyIndicator.SetActive(false);
        if (happyIndicator != null) happyIndicator.SetActive(true);

        // Play happy sound
        if (happySound != null)
            audioSource.PlayOneShot(happySound);

        // Hand over animation control to CityPeople.
        // (Component should be present on the prefab but DISABLED.)
        var cityPeople = GetComponent<CityPeople.CityPeople>();
        if (cityPeople == null)
            cityPeople = GetComponentInChildren<CityPeople.CityPeople>(true);
        if (cityPeople != null)
        {
            // Just enable it — CityPeople.Start() will run on the next frame
            // and pick a random clip itself (because AutoPlayAnimations is on).
            cityPeople.enabled = true;
        }

        // Start happy wandering using the existing NavMeshAgent
        agent.isStopped = false;
        agent.speed = happyWanderSpeed;
        StartCoroutine(HappyWanderRoutine());

        // Notify the GameManager
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.PersonMadeHappy();
    }

    System.Collections.IEnumerator HappyWanderRoutine()
    {
        Vector3 origin = transform.position;
        while (currentMood == MoodState.Happy)
        {
            Vector2 offset = Random.insideUnitCircle * happyWanderRadius;
            Vector3 target = origin + new Vector3(offset.x, 0f, offset.y);

            // Snap target to nearest valid NavMesh position
            if (NavMesh.SamplePosition(target, out var hit, happyWanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            yield return new WaitForSeconds(happyWanderInterval);
        }
    }

    void UpdateVisuals()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = isVeryUnhappy ? veryUnhappyColor : unhappyColor;
        }

        if (unhappyIndicator != null) unhappyIndicator.SetActive(true);
        if (happyIndicator != null) happyIndicator.SetActive(false);
    }

    // Visualize detection and attack range in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
