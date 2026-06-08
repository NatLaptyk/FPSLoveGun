using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Section-level checkpoint / respawn system.
// On death, instead of reloading the whole scene (which throws away café progress when
// the player dies in the stadium, etc.), the Retry button calls
// <see cref="RespawnAtCheckpoint"/> which:
// • teleports the player to the last checkpoint Transform
// • refills happiness, ammo, and love bombs
// • hides the lose panel, clears <c>GameManager.isGameOver</c>, re-locks the cursor,
// restores <c>Time.timeScale</c>, clears <c>PlayerHealth.invincible</c>
// • leaves already-converted NPCs converted and section progress intact
// A checkpoint is "the start of the currently-active section". You set it as each
// section begins. The most recently set checkpoint wins.

public class CheckpointManager : MonoBehaviour
{
    // ── Player references ──────────────────────────────────────────────────────
    [Header("Player references (auto-found by 'Player' tag if blank)")]
    [SerializeField] private Transform       playerTransform;
    public PlayerHealth    playerHealth;
    [SerializeField] private LoveGun         loveGun;
    [SerializeField] private LoveBombThrower bombThrower;

    // ── Initial checkpoint ─────────────────────────────────────────────────────
    [Header("Initial checkpoint")]
    [Tooltip("Where the player respawns BEFORE any section has set a checkpoint. " +
             "Leave blank to snapshot the player's start position automatically.")]
    [SerializeField] private Transform initialCheckpoint;

    // ── Section checkpoint slots ───────────────────────────────────────────────
    [Header("Section checkpoints (assign whichever ones you use)")]
    [Tooltip("Empty GameObject placed at the café entrance.")]
    [SerializeField] private Transform cafeCheckpoint;
    [Tooltip("Empty GameObject placed at the breather entrance.")]
    [SerializeField] private Transform breatherCheckpoint;
    [Tooltip("Empty GameObject placed just inside the stadium entry trigger.")]
    [SerializeField] private Transform stadiumCheckpoint;
    [Tooltip("Empty GameObject placed where the Heartbreak Bridge ambush begins.")]
    [SerializeField] private Transform ambushCheckpoint;
    [Tooltip("Empty GameObject placed at the FinalBoss arena spawn — usually the same " +
             "Transform CatVisionEvent uses as its teleport destination.")]
    [SerializeField] private Transform bossArenaCheckpoint;

    // ── Reset values on respawn ────────────────────────────────────────────────
    [Header("Respawn resource refill")]
    [Tooltip("LoveGun magazine ammo to ensure on respawn (current ammo is NEVER reduced).")]
    [SerializeField] private int respawnMagAmmo     = 30;
    [Tooltip("LoveGun reserve ammo to ensure on respawn (reserve is NEVER reduced).")]
    [SerializeField] private int respawnReserveAmmo = 60;
    [Tooltip("Love bombs to ensure on respawn (bombs are NEVER reduced).")]
    [SerializeField] private int respawnBombs       = 3;

    // ── UI hooks ───────────────────────────────────────────────────────────────
    [Header("UI")]
    [Tooltip("The Game Over panel — hidden on respawn. Same one your GameManager uses.")]
    [SerializeField] private GameObject losePanel;

    // ── HUD feedback ───────────────────────────────────────────────────────────
    [Header("Feedback")]
    [Tooltip("Show a brief HUDManager message when a checkpoint is set.")]
    [SerializeField] private bool showCheckpointMessage = true;
    [Tooltip("Format string for the message. {0} is replaced with the checkpoint label.")]
    [SerializeField] private string checkpointMessageFormat = "Checkpoint reached — {0}";
    [Tooltip("Show a brief HUDManager message when the player respawns.")]
    [SerializeField] private bool showRespawnMessage = true;
    [Tooltip("Format string for the respawn message. {0} is the checkpoint label.")]
    [SerializeField] private string respawnMessageFormat = "Respawning at {0}…";

    // ── Events ─────────────────────────────────────────────────────────────────
    [Header("Events")]
    [SerializeField] private UnityEvent onCheckpointSet;
    [SerializeField] private UnityEvent onRespawn;

    // ── State ──────────────────────────────────────────────────────────────────
    public Transform CurrentCheckpoint { get; private set; }
    public string    CurrentLabel      { get; private set; } = "Start";

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        ResolveReferences();

        if (initialCheckpoint != null)
        {
            CurrentCheckpoint = initialCheckpoint;
            CurrentLabel      = "Start";
        }
        else if (playerTransform != null)
        {
            // Snapshot the player's starting position into a hidden marker so we always
            // have a valid fallback.
            GameObject marker = new GameObject("CheckpointMarker_Initial");
            marker.transform.SetPositionAndRotation(playerTransform.position,
                                                    playerTransform.rotation);
            CurrentCheckpoint = marker.transform;
            CurrentLabel      = "Start";
        }
        else
        {
            Debug.LogWarning("[CheckpointManager] No initial checkpoint and no player " +
                             "transform — RespawnAtCheckpoint will fall back to a scene reload.");
        }
    }

    void ResolveReferences()
    {
        if (playerTransform == null)
        {
            GameObject pl = GameObject.FindGameObjectWithTag("Player");
            if (pl != null) playerTransform = pl.transform;
        }
        if (playerHealth == null && playerTransform != null)
        {
            playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = playerTransform.GetComponentInChildren<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();
        }
        if (loveGun == null && playerTransform != null)
            loveGun = playerTransform.GetComponentInChildren<LoveGun>(true);
        if (bombThrower == null && playerTransform != null)
            bombThrower = playerTransform.GetComponentInChildren<LoveBombThrower>(true);
    }

    // ── Setting checkpoints ────────────────────────────────────────────────────
    /// <summary>Set the active checkpoint to an arbitrary Transform with a custom label.</summary>
    public void SetCheckpoint(Transform checkpoint, string label)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning($"[CheckpointManager] SetCheckpoint('{label}') ignored — null Transform.");
            return;
        }

        CurrentCheckpoint = checkpoint;
        CurrentLabel      = string.IsNullOrEmpty(label) ? checkpoint.name : label;

        Debug.Log($"[CheckpointManager] Checkpoint set: '{CurrentLabel}' at {checkpoint.position}");

        if (showCheckpointMessage)
        {
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null)
                hud.ShowMessage(string.Format(checkpointMessageFormat, CurrentLabel), 2.5f);
        }

        onCheckpointSet?.Invoke();
    }

    /// <summary>Set the active checkpoint to a Transform, using its name as the label.</summary>
    public void SetCheckpoint(Transform checkpoint)
    {
        SetCheckpoint(checkpoint, checkpoint != null ? checkpoint.name : "Checkpoint");
    }

    // Convenience wrappers — show up cleanly in UnityEvent dropdowns in the Inspector,
    // so wiring "café SectionTracker.onSectionComplete" → "SetCheckpointBreather()" is a
    // one-click operation.
    public void SetCheckpointCafe()      => SetCheckpoint(cafeCheckpoint,      "Café");
    public void SetCheckpointBreather()  => SetCheckpoint(breatherCheckpoint,  "Breather");
    public void SetCheckpointStadium()   => SetCheckpoint(stadiumCheckpoint,   "Stadium");
    public void SetCheckpointAmbush()    => SetCheckpoint(ambushCheckpoint,    "Heartbreak Bridge");
    public void SetCheckpointBossArena() => SetCheckpoint(bossArenaCheckpoint, "Boss arena");

    // ── The respawn flow ───────────────────────────────────────────────────────
    /// <summary>
    /// Wire your Retry button here INSTEAD OF GameManager.RestartLevel().
    /// </summary>
    public void RespawnAtCheckpoint()
    {
        if (CurrentCheckpoint == null)
        {
            Debug.LogWarning("[CheckpointManager] No checkpoint available — falling back to scene reload.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        ResolveReferences();

        Debug.Log($"[CheckpointManager] Respawning at '{CurrentLabel}'.");

        // 1. Clear "game over" state on the GameManager.
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.isGameOver = false;
            // isGameWon left alone — if you somehow died after winning, leave that state.
        }

        // 2. Hide the lose panel.
        if (losePanel != null) losePanel.SetActive(false);

        // 3. Time scale + cursor — GameOver unlocks the cursor; lock it again now.
        Time.timeScale       = 1f;
        Cursor.lockState     = CursorLockMode.Locked;
        Cursor.visible       = false;

        // 4. Teleport the player. Mirror CatVisionEvent's CC disable/enable dance
        //    so the CharacterController doesn't fight the new position.
        if (playerTransform != null)
        {
            CharacterController cc = playerTransform.GetComponentInChildren<CharacterController>();
            if (cc == null) cc = playerTransform.GetComponentInParent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.SetPositionAndRotation(CurrentCheckpoint.position,
                                                   CurrentCheckpoint.rotation);

            if (cc != null) cc.enabled = true;

            Rigidbody rb = playerTransform.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // 5. Refill happiness, ammo, bombs (Max semantics — never reduce what they had).
        HUDManager hud = FindFirstObjectByType<HUDManager>();

        if (playerHealth != null)
        {
            playerHealth.invincible       = false;
            playerHealth.currentHappiness = playerHealth.maxHappiness;
            if (hud != null) hud.UpdateHappiness(playerHealth.currentHappiness, playerHealth.maxHappiness);
        }

        if (loveGun != null)
        {
            loveGun.currentAmmo = Mathf.Max(loveGun.currentAmmo, respawnMagAmmo);
            loveGun.reserveAmmo = Mathf.Max(loveGun.reserveAmmo, respawnReserveAmmo);
            if (hud != null) hud.UpdateAmmo(loveGun.currentAmmo, loveGun.reserveAmmo);
        }

        if (bombThrower != null)
        {
            bombThrower.currentBombs = Mathf.Max(bombThrower.currentBombs, respawnBombs);
            if (hud != null) hud.UpdateBombs(bombThrower.currentBombs, bombThrower.maxBombs);
        }

        // 6. Friendly HUD note so the player understands what just happened.
        if (showRespawnMessage && hud != null)
            hud.ShowMessage(string.Format(respawnMessageFormat, CurrentLabel), 2.5f);

        onRespawn?.Invoke();
    }

    // ── Editor visualisation ───────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        void DrawMarker(Transform t, Color c, string label)
        {
            if (t == null) return;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(t.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(t.position, t.position + Vector3.up * 2.5f);
        }
        DrawMarker(initialCheckpoint,     Color.white,                "Start");
        DrawMarker(cafeCheckpoint,        new Color(1f, 0.7f, 0.3f),  "Café");
        DrawMarker(breatherCheckpoint,    new Color(0.5f, 1f, 0.5f),  "Breather");
        DrawMarker(stadiumCheckpoint,     new Color(1f, 0.3f, 0.3f),  "Stadium");
        DrawMarker(ambushCheckpoint,      new Color(1f, 0.4f, 0.7f),  "Ambush");
        DrawMarker(bossArenaCheckpoint,   new Color(0.8f, 0.2f, 1f),  "Boss arena");
    }
}