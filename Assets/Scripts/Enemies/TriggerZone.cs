using UnityEngine;

// Trigger Zone — an invisible box that triggers events when the player walks through.

public class TriggerZone : MonoBehaviour
{
    public enum TriggerType { Event, Pickup, Checkpoint, AreaMessage }

    [Header("Trigger Settings")]
    [SerializeField] private TriggerType triggerType = TriggerType.Event;
    [SerializeField] private bool triggerOnce = true;
    private bool hasTriggered = false;

    [Header("Event Trigger")]
    [SerializeField] private EventManager eventToTrigger;      // Assign the EventManager this zone activates

    public enum PickupType { Ammo, LoveBomb, Happiness }

    [Header("Pickup Settings (if type = Pickup)")]
    [SerializeField] private PickupType pickupType = PickupType.Ammo;
    [SerializeField] private int pickupAmount = 10;
    [SerializeField] private GameObject pickupVisual;          // The visible pickup object to hide after collecting

    [Header("Checkpoint (if type = Checkpoint)")]
    [SerializeField] private Transform respawnPoint;           // Where the player respawns

    [Header("Area Message (if type = AreaMessage)")]
    [SerializeField] private string areaMessage = "";          // E.g., "The Town Square" or "Caution: Very Unhappy Zone"
    [SerializeField] private float messageDuration = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip triggerSound;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered && triggerOnce) return;

        hasTriggered = true;

        // Play trigger sound
        if (triggerSound != null)
            AudioSource.PlayClipAtPoint(triggerSound, transform.position);

        switch (triggerType)
        {
            case TriggerType.Event:
                if (eventToTrigger != null)
                    eventToTrigger.TriggerEvent();
                break;

            case TriggerType.Pickup:
                HandlePickup(other.gameObject);
                break;

            case TriggerType.Checkpoint:
                HandleCheckpoint(other.gameObject);
                break;

            case TriggerType.AreaMessage:
                HUDManager hud = FindFirstObjectByType<HUDManager>();
                if (hud != null) hud.ShowMessage(areaMessage, messageDuration);
                break;
        }
    }

    void HandlePickup(GameObject player)
    {
        switch (pickupType)
        {
            case PickupType.Ammo:
                LoveGun gun = player.GetComponentInChildren<LoveGun>();
                if (gun != null) gun.AddAmmo(pickupAmount);
                break;

            case PickupType.LoveBomb:
                LoveBombThrower thrower = player.GetComponent<LoveBombThrower>();
                if (thrower != null) thrower.AddBombs(pickupAmount);
                break;

            case PickupType.Happiness:
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null) health.Heal(pickupAmount);
                break;
        }

        // Hide or destroy the pickup visual
        if (pickupVisual != null)
            pickupVisual.SetActive(false);

        // Optionally destroy the whole trigger zone after pickup
        if (triggerOnce)
            gameObject.SetActive(false);
    }

    void HandleCheckpoint(GameObject player)
    {
        // Simple checkpoint: just save the respawn position
        // In a full game you'd have a CheckpointManager
        GameManager gm = FindFirstObjectByType<GameManager>();
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null) hud.ShowMessage("Checkpoint Reached!", 2f);
    }

    // Visualize the trigger zone in Scene view
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = triggerType switch
            {
                TriggerType.Event => new Color(1f, 0.5f, 0f, 0.3f),      // Orange
                TriggerType.Pickup => new Color(0f, 1f, 0f, 0.3f),       // Green
                TriggerType.Checkpoint => new Color(0f, 0.5f, 1f, 0.3f), // Blue
                TriggerType.AreaMessage => new Color(1f, 1f, 0f, 0.3f),  // Yellow
                _ => new Color(1f, 1f, 1f, 0.3f)
            };

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
