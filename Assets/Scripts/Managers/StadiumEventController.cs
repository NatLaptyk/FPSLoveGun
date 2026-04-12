using UnityEngine;

/// <summary>
/// Orchestrates the multi-phase stadium finale event.
/// </summary>
/// <remarks>
/// <para>
/// This controller handles closing the arena doors, enabling waves of enemies, 
/// and tracking their conversion to happiness to progress the fight. It uses 
/// zero-GC loops in its <c>Update</c> method to monitor enemy states.
/// </para>
/// <example>
/// To trigger this event from a <see cref="TriggerZone"/>:
/// <code>
/// // Assuming trigger is hooked up via Unity Events or direct reference:
/// stadiumEventController.StartStadiumEvent();
/// </code>
/// </example>
/// <list type="bullet">
/// <item><term>Phase 1</term><description>Seat snipers (<see cref="UnhappyPerson"/> without NavMesh paths).</description></item>
/// <item><term>Phase 2</term><description>Ground rushers (<see cref="UnhappyPerson"/> waves).</description></item>
/// <item><term>Phase 3</term><description>The final boss (<see cref="WatcherAI"/>).</description></item>
/// </list>
/// </remarks>
public class StadiumEventController : MonoBehaviour
{
    /// <summary>
    /// Represents the current stage of the stadium battle.
    /// </summary>
    public enum ArenaPhase { Waiting, Phase1_Seats, Phase2_Rush, Phase3_Boss, Completed }

    /// <summary>
    /// Gets the current active phase of the arena.
    /// </summary>
    /// <value>An <see cref="ArenaPhase"/> enum representing the ongoing battle stage.</value>
    public ArenaPhase CurrentPhase { get; private set; } = ArenaPhase.Waiting;

    [Header("Environment")]
    [Tooltip("The door GameObjects that will close when the event starts.")]
    public GameObject[] stadiumDoors;

    [Header("Phase 1: Seat Snipers")]
    [Tooltip("Enemies already active in the seats at the start of the event.")]
    public UnhappyPerson[] seatEnemies;

    [Header("Phase 2: Ground Rush")]
    [Tooltip("Enemies that will be enabled when Phase 2 begins.")]
    public UnhappyPerson[] rushEnemies;

    [Header("Phase 3: The Boss")]
    [Tooltip("The boss that will be enabled for the final phase.")]
    public WatcherAI stadiumBoss;

    /// <summary>
    /// Initiates the stadium event, locks the doors, and begins Phase 1.
    /// </summary>
    /// <remarks>
    /// <para>Call this method from your <see cref="TriggerZone"/> or EventManager when the player enters.</para>
    /// </remarks>
    public void StartStadiumEvent()
    {
        if (CurrentPhase != ArenaPhase.Waiting) return;

        // Close the doors behind the player
        for (int i = 0; i < stadiumDoors.Length; i++)
        {
            if (stadiumDoors[i] != null) stadiumDoors[i].SetActive(true);
        }

        CurrentPhase = ArenaPhase.Phase1_Seats;
        Debug.Log("[StadiumEvent] Doors locked. Phase 1: Snipers started.");
    }

    /// <summary>
    /// Checks the conditions required to advance the arena phases frame-by-frame.
    /// </summary>
    private void Update()
    {
        switch (CurrentPhase)
        {
            case ArenaPhase.Phase1_Seats:
                if (AreAllEnemiesHappy(seatEnemies))
                {
                    StartPhase2();
                }
                break;

            case ArenaPhase.Phase2_Rush:
                if (AreAllEnemiesHappy(rushEnemies))
                {
                    StartPhase3();
                }
                break;

            case ArenaPhase.Phase3_Boss:
                if (IsBossDefeated())
                {
                    CompleteEvent();
                }
                break;
        }
    }

    /// <summary>
    /// Activates the ground rush enemies for the second phase of the battle.
    /// </summary>
    private void StartPhase2()
    {
        CurrentPhase = ArenaPhase.Phase2_Rush;

        for (int i = 0; i < rushEnemies.Length; i++)
        {
            if (rushEnemies[i] != null) rushEnemies[i].gameObject.SetActive(true);
        }

        Debug.Log("[StadiumEvent] Phase 1 cleared. Phase 2: Rush started.");
    }

    /// <summary>
    /// Activates the boss for the final phase of the battle.
    /// </summary>
    private void StartPhase3()
    {
        CurrentPhase = ArenaPhase.Phase3_Boss;

        if (stadiumBoss != null)
        {
            stadiumBoss.gameObject.SetActive(true);
        }

        Debug.Log("[StadiumEvent] Phase 2 cleared. Phase 3: Boss started.");
    }

    /// <summary>
    /// Triggers the final victory conditions for the game.
    /// </summary>
    private void CompleteEvent()
    {
        CurrentPhase = ArenaPhase.Completed;
        Debug.Log("[StadiumEvent] Boss defeated! You win!");

        // Assuming your GameManager has a GameWon method. 
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            // gm.GameWon(); 
        }
    }

    /// <summary>
    /// Evaluates whether an entire array of enemies has been converted to the Happy state.
    /// </summary>
    /// <param name="enemies">An array of <see cref="UnhappyPerson"/> to evaluate.</param>
    /// <returns><c>true</c> if all valid enemies have a MoodState of Happy; otherwise, <c>false</c>.</returns>
    private bool AreAllEnemiesHappy(UnhappyPerson[] enemies)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].currentMood != UnhappyPerson.MoodState.Happy)
            {
                return false; // Found an unhappy person, wave is not over
            }
        }
        return true; // Everyone is happy (or array is empty/null)
    }

    /// <summary>
    /// Evaluates whether the boss has been converted or destroyed.
    /// </summary>
    /// <returns><c>true</c> if the boss is null or in the Converted state; otherwise, <c>false</c>.</returns>
    private bool IsBossDefeated()
    {
        // Check if boss was destroyed or successfully converted
        if (stadiumBoss == null) return true;
        return stadiumBoss.CurrentState == WatcherAI.BossState.Converted;
    }
}