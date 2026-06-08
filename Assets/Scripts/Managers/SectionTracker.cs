using UnityEngine;
using UnityEngine.Events;

// Tracks progress for a single section of the level.
// Unlike GameManager (which wins when ALL unhappy people in the scene are happy),
// a SectionTracker only cares about the specific NPCs assigned to it — letting you
// chain multiple sections together.

public class SectionTracker : MonoBehaviour
{
    [Header("Identity")]
    public string sectionName = "Section 1";

    [Header("Goal")]
    [Tooltip("The unhappy people that belong to this section")]
    public UnhappyPerson[] sectionPeople;

    [Tooltip("How many must be made happy to clear this section. 0 = all of them.")]
    public int goalCount = 0;

    [Header("HUD")]
    [Tooltip("If true, updates HUD with this section's progress instead of global")]
    public bool showProgressOnHUD = true;

    [Header("Events")]
    public UnityEvent onSectionStarted;
    public UnityEvent onSectionComplete;
    [Tooltip("Fired every time a person in this section becomes happy (param: current count)")]
    public UnityEvent<int> onPersonMadeHappy;

    public int HappyCount { get; private set; }
    public int TotalCount { get; private set; }
    public bool IsComplete { get; private set; }
    public bool IsStarted { get; private set; }

    void Start()
    {
        TotalCount = sectionPeople != null ? sectionPeople.Length : 0;
        if (goalCount <= 0) goalCount = TotalCount;
        HappyCount = 0;
        IsComplete = false;
        IsStarted = false;

        Debug.Log($"[SectionTracker] '{sectionName}' initialized: goal {goalCount}/{TotalCount}");
    }

    
    // Call this when the player enters the section (e.g. from EventManager.onSectionStarted).
    // Optional — the tracker works without it, but calling this enables HUD progress updates.
       public void BeginSection()
    {
        if (IsStarted) return;
        IsStarted = true;

        Debug.Log($"[SectionTracker] '{sectionName}' started");

        if (showProgressOnHUD)
            PushProgressToHUD();

        onSectionStarted?.Invoke();
    }

    void Update()
    {
        if (IsComplete || sectionPeople == null) return;

        // Poll each assigned person for mood changes.
        // Lightweight: just a reference comparison and an int check.
        int newlyHappy = 0;
        for (int i = 0; i < sectionPeople.Length; i++)
        {
            var p = sectionPeople[i];
            if (p == null) continue;
            if (p.currentMood == UnhappyPerson.MoodState.Happy)
                newlyHappy++;
        }

        if (newlyHappy != HappyCount)
        {
            HappyCount = newlyHappy;
            Debug.Log($"[SectionTracker] '{sectionName}' progress: {HappyCount}/{goalCount}");

            onPersonMadeHappy?.Invoke(HappyCount);

            if (showProgressOnHUD && IsStarted)
                PushProgressToHUD();

            if (HappyCount >= goalCount)
                CompleteSection();
        }
    }

    void CompleteSection()
    {
        IsComplete = true;
        Debug.Log($"[SectionTracker] '{sectionName}' COMPLETE!");

        onSectionComplete?.Invoke();
    }

    void PushProgressToHUD()
    {
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null)
            hud.UpdatePeopleCount(HappyCount, goalCount);
    }

    // Visualize section people in Scene view
    void OnDrawGizmosSelected()
    {
        if (sectionPeople == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        foreach (var p in sectionPeople)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.transform.position + Vector3.up * 1.2f, 0.6f);
            Gizmos.DrawLine(transform.position, p.transform.position);
        }
    }
}
