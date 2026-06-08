using UnityEngine;


// Orchestrates the multi-phase stadium finale event by coordinating with <see cref="SectionTracker"


//This controller delegates wave tracking and HUD management to individual <see cref="SectionTracker"/>s. 
//It acts as the master director: locking doors, waking up dormant enemies via the trackers' arrays, 
//and progressing the fight from snipers, to rushers, to the final boss.

// <list type="table">
// <item><term>Phase 1</term><description>Wakes up dormant seat snipers and triggers the Phase 1 <see cref="SectionTracker"/>.</description></item>
// <item><term>Phase 2</term><description>Wakes up ground rushers and triggers the Phase 2 <see cref="SectionTracker"/>.</description></item>
// <item><term>Phase 3</term><description>Activates the <see cref="WatcherAI"/> and monitors it for the final victory condition.</description></item>

public class StadiumEventController : MonoBehaviour
{
   
    // Represents the current stage of the stadium battle.
   
    public enum ArenaPhase { Waiting, Phase1_Seats, Phase2_Rush, Phase3_Boss, Completed }


    // Gets the current active phase of the arena.
  
    // An <see cref="ArenaPhase"/> enum representing the ongoing battle stage.
    public ArenaPhase CurrentPhase { get; private set; } = ArenaPhase.Waiting;

    [Header("Environment")]
    [Tooltip("The door GameObjects that will close when the event starts.")]
    [SerializeField] private GameObject[] stadiumDoors;

    [Header("Wave Tracking Integrations")]
    [Tooltip("The SectionTracker responsible for the seat snipers.")]
    [SerializeField] private SectionTracker phase1Tracker;

    [Tooltip("The SectionTracker responsible for the ground rushers.")]
    [SerializeField] private SectionTracker phase2Tracker;

    [Header("Phase 3: The Boss")]
    [Tooltip("The boss that will be enabled for the final phase.")]
    [SerializeField] private WatcherAI stadiumBoss;

    private HUDManager hud;


    // Prepares the stadium by forcing the dormant seat enemies (found via the <see cref="phase1Tracker"/>) to appear happy initially.
   
    private void Start()
    {
        hud = FindFirstObjectByType<HUDManager>();

        if (phase1Tracker != null && phase1Tracker.sectionPeople != null)
        {
            for (int i = 0; i < phase1Tracker.sectionPeople.Length; i++)
            {
                UnhappyPerson spectator = phase1Tracker.sectionPeople[i];
                if (spectator != null)
                {
                    // Keep AI disabled so they don't move or attack
                    spectator.enabled = false;

                    // Paint them happy
                    if (spectator.bodyRenderer != null)
                        spectator.bodyRenderer.material.color = spectator.happyColor;

                    if (spectator.happyIndicator != null)
                        spectator.happyIndicator.SetActive(true);

                    if (spectator.unhappyIndicator != null)
                        spectator.unhappyIndicator.SetActive(false);
                }
            }
        }
    }

   
    // Initiates the stadium event, locks the doors, shocks the crowd into unhappiness, and tells Phase 1 Tracker to begin.
   
    public void StartStadiumEvent()
    {
        if (CurrentPhase != ArenaPhase.Waiting) return;

        // Lock the doors
        for (int i = 0; i < stadiumDoors.Length; i++)
        {
            if (stadiumDoors[i] != null) stadiumDoors[i].SetActive(true);
        }

        // Wake up Phase 1 enemies
        if (phase1Tracker != null && phase1Tracker.sectionPeople != null)
        {
            for (int i = 0; i < phase1Tracker.sectionPeople.Length; i++)
            {
                if (phase1Tracker.sectionPeople[i] != null)
                {
                    // Enabling the script runs UnhappyPerson.Start(), reverting them to hostile
                    phase1Tracker.sectionPeople[i].enabled = true;
                }
            }

            // Tell the SectionTracker to start updating the HUD
            phase1Tracker.BeginSection();
        }

        CurrentPhase = ArenaPhase.Phase1_Seats;
        Debug.Log("[StadiumEvent] Doors locked. Phase 1 Tracker started!");

        if (hud != null) hud.ShowMessage("Survive the Snipers!", 3f);
    }


    // Monitors the progress of the <see cref="SectionTracker"/>s to advance the arena phases.
  
    private void Update()
    {
        switch (CurrentPhase)
        {
            case ArenaPhase.Phase1_Seats:
                if (phase1Tracker != null && phase1Tracker.IsComplete)
                {
                    StartPhase2();
                }
                break;

            case ArenaPhase.Phase2_Rush:
                if (phase2Tracker != null && phase2Tracker.IsComplete)
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

  
    // Activates the ground rush enemies and hands HUD control over to the Phase 2 <see cref="SectionTracker"/>.
  
    private void StartPhase2()
    {
        CurrentPhase = ArenaPhase.Phase2_Rush;

        if (phase2Tracker != null && phase2Tracker.sectionPeople != null)
        {
            for (int i = 0; i < phase2Tracker.sectionPeople.Length; i++)
            {
                if (phase2Tracker.sectionPeople[i] != null)
                    phase2Tracker.sectionPeople[i].gameObject.SetActive(true);
            }

            phase2Tracker.BeginSection();
        }

        Debug.Log("[StadiumEvent] Phase 1 cleared. Phase 2 Tracker started.");
        if (hud != null) hud.ShowMessage("Incoming Rush!", 3f);
    }

   
    // Activates the <see cref="WatcherAI"/> for the final phase and clears the standard wave HUD.
  
    private void StartPhase3()
    {
        CurrentPhase = ArenaPhase.Phase3_Boss;

        if (stadiumBoss != null)
        {
            stadiumBoss.gameObject.SetActive(true);
        }

        if (hud != null)
        {
            hud.ShowMessage("Defeat the Watcher!", 4f);

            // Clear the SectionTracker numbers from the screen during the boss fight
            hud.UpdatePeopleCount(0, 0);
        }

        Debug.Log("[StadiumEvent] Phase 2 cleared. Phase 3: Boss started.");
    }

  
    // Triggers the final victory conditions for the game by interfacing with the <see cref="GameManager"/>.
   
    private void CompleteEvent()
    {
        CurrentPhase = ArenaPhase.Completed;
        Debug.Log("[StadiumEvent] Boss defeated! You win!");

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.TriggerWin();
        }

        if (hud != null)
        {
            hud.ShowMessage("The stadium is full of love! You win!", 5f);
        }
    }

   
    // Evaluates whether the boss has been converted or destroyed.
  
    private bool IsBossDefeated()
    {
        if (stadiumBoss == null) return true;
        return stadiumBoss.CurrentState == WatcherAI.BossState.Converted;
    }
}