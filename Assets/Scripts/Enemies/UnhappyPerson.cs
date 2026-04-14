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
    public enum BehaviourMode { Patrol, Stadium }

    [Header("Mood")]
    public int unhappinessLevel = 3;        // How much love needed to become happy (1-5)
    public bool isVeryUnhappy = false;       // If true, requires love bomb (resists normal shots)
    private int currentLoveReceived = 0;
    public MoodState currentMood = MoodState.Unhappy;

    [Header("Behaviour Mode")]
    public BehaviourMode behaviourMode = BehaviourMode.Patrol;

    [Header("Patrol")]
    public Transform[] patrolPoints;         // Assign waypoints in Inspector
    public float patrolSpeed = 2.5f;
    public float waitTimeAtPoint = 1f;       // Pause at each patrol point
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    [Header("Stadium Behaviour")]
    [Tooltip("Where this NPC should walk to on the field after descending from seats.")]
    public Transform fieldTarget;
    [Tooltip("Speed when walking down to the field.")]
    public float descentSpeed = 3.5f;
    [Tooltip("Radius of the circle NPCs form around the player.")]
    public float crowdRadius = 4f;
    [Tooltip("Speed when crowding toward the player on the field.")]
    public float crowdSpeed = 2f;
    [Tooltip("This NPC's index in the crowd ring (set by Section2Spawner).")]
    public int crowdSlotIndex = 0;
    [Tooltip("Total number of NPCs sharing the crowd ring (set by Section2Spawner).")]
    public int totalCrowdSlots = 1;

    private enum StadiumPhase { Descending, Crowding }
    private StadiumPhase stadiumPhase = StadiumPhase.Descending;
    private bool stadiumActivated = false;

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

        // Start patrolling (only in Patrol mode)
        if (behaviourMode == BehaviourMode.Patrol && patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update()
    {
        if (currentMood == MoodState.Happy) return;

        if (behaviourMode == BehaviourMode.Stadium)
        {
            UpdateStadium();
            return;
        }

        // ── Default patrol behaviour ──────────────────────────────────────────
        float distanceToPlayer = playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : float.MaxValue;

        if (distanceToPlayer <= detectionRange && playerTransform != null)
        {
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDir), 5f * Time.deltaTime);

            agent.isStopped = true;

            if (distanceToPlayer <= attackRange && Time.time >= nextThrowTime)
                ThrowSadness();
        }
        else
        {
            agent.isStopped = false;
            Patrol();
        }
    }

    void UpdateStadium()
    {
        if (!stadiumActivated) return;

        float distToPlayer = playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : float.MaxValue;

        // Always throw sadness at the player if in range
        if (distToPlayer <= attackRange && Time.time >= nextThrowTime && playerTransform != null)
        {
            FacePlayer();
            ThrowSadness();
        }

        // Stadium movement uses pure transform (no NavMesh).
        // Seats are elevated geometry not covered by the NavMesh bake, so
        // NavMeshAgent would teleport NPCs to the floor on spawn.
        switch (stadiumPhase)
        {
            case StadiumPhase.Descending:
                if (fieldTarget != null)
                {
                    Vector3 target = fieldTarget.position;

                    // Face direction of travel (horizontal only so they don't tip forward)
                    Vector3 flatDir = new Vector3(
                        target.x - transform.position.x, 0f,
                        target.z - transform.position.z);
                    if (flatDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(flatDir), 8f * Time.deltaTime);

                    transform.position = Vector3.MoveTowards(
                        transform.position, target, descentSpeed * Time.deltaTime);

                    if (Vector3.Distance(transform.position, target) < 1f)
                    {
                        stadiumPhase = StadiumPhase.Crowding;
                        Debug.Log($"[StadiumNPC] {gameObject.name} reached the field — crowding.");
                    }
                }
                else
                {
                    stadiumPhase = StadiumPhase.Crowding;
                }
                break;

            case StadiumPhase.Crowding:
                if (playerTransform == null) break;

                // Each NPC gets a unique angle slot so they spread into a ring
                // rather than piling on top of each other.
                float slotAngle = crowdSlotIndex * (360f / Mathf.Max(1, totalCrowdSlots));
                Vector3 slotOffset = Quaternion.Euler(0, slotAngle, 0) * Vector3.forward * crowdRadius;
                Vector3 slotTarget = playerTransform.position + slotOffset;

                // Flatten Y so NPCs don't try to sink into or float above the floor
                slotTarget.y = transform.position.y;

                float distToSlot = Vector3.Distance(transform.position, slotTarget);
                if (distToSlot > 0.3f)
                {
                    Vector3 dir = (slotTarget - transform.position).normalized;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), 8f * Time.deltaTime);
                    transform.position += dir * crowdSpeed * Time.deltaTime;
                }
                else
                {
                    // At assigned slot — face the player
                    FacePlayer();
                }
                break;
        }
    }

    /// <summary>
    /// Call this to activate a stadium NPC (e.g. from Section2Spawner after spawn).
    /// </summary>
    public void ActivateStadiumBehaviour()
    {
        // Cache agent in case Start() hasn't run yet
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        behaviourMode = BehaviourMode.Stadium;
        stadiumActivated = true;
        stadiumPhase = StadiumPhase.Descending;
        // Note: agent is already disabled by Section2Spawner right after Instantiate
        // to prevent NavMesh snapping from elevated seat positions.
        // Stadium movement uses direct transform — no NavMesh needed.

        Debug.Log($"[StadiumNPC] {gameObject.name} activated — descending to field.");
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), 8f * Time.deltaTime);
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

        // ── Permanently remove all physics obstacles so happy NPCs are fully passthrough.
        // Destroy() is used instead of disable because an animation system or other
        // component could re-enable a disabled component on the next frame.
        // CharacterController is NOT a Collider subclass — must be handled separately.
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            Destroy(col);
        foreach (CharacterController cc in GetComponentsInChildren<CharacterController>(true))
            Destroy(cc);
        // If there is a Rigidbody, make it kinematic so it can't push other objects.
        Rigidbody rb = GetComponentInChildren<Rigidbody>(true);
        if (rb != null) rb.isKinematic = true;

        // Update visuals
        if (bodyRenderer != null)
            bodyRenderer.material.color = happyColor;

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
            cityPeople.enabled = true;

        // ── Happy wandering — only if the NavMeshAgent is active.
        // Stadium NPCs have their agent disabled (pure transform movement);
        // trying to call agent.SetDestination on a disabled agent throws errors.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = happyWanderSpeed;
            StartCoroutine(HappyWanderRoutine());
        }
        // else: stadium NPC — CityPeople will play a dance animation in place,
        // which is exactly what we want for the surrounded-player crowd.

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
