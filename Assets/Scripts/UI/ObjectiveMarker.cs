using UnityEngine;

/// <summary>
/// World-space minimap objective marker.
/// Place this on an empty GameObject at the location you want marked
/// (e.g. the stadium entrance, the street boss spawn point, etc.).
///
/// SETUP — do this once:
/// 1. Create a Layer called "Minimap" (Edit → Project Settings → Tags and Layers).
/// 2. Set your MinimapCamera's Culling Mask to include ONLY the "Minimap" layer
///    (plus whatever else the minimap should see).
/// 3. Set your main camera's Culling Mask to EXCLUDE the "Minimap" layer
///    so the icons are invisible in the normal view.
///
/// PER-MARKER SETUP:
/// 1. Create an empty GameObject at the objective world position.
/// 2. Add a child Quad (or Sprite Renderer) — assign your icon texture/sprite.
///    Scale it so it looks right on the minimap (e.g. 2×2 units).
///    Rotate it -90° on X so it faces upward (minimap camera looks straight down).
/// 3. Set the Quad/Sprite GameObject's layer to "Minimap".
/// 4. Add this script to the parent empty GameObject.
/// 5. Drag the Quad/Sprite into the "Marker Visual" field.
/// 6. Start disabled or enabled depending on whether the marker should
///    be visible from the start of the scene.
///
/// WIRING EVENTS:
/// - To show a marker: call Activate() from a UnityEvent or script.
/// - To hide a marker: call Deactivate() from a UnityEvent or script.
///
/// Example — deactivate stadium marker when all waves clear:
///   Section2Spawner → On All Waves Complete → StadiumMarker.Deactivate()
/// Example — activate boss marker when player teleports:
///   CatVisionEvent (after teleport) → BossMarker.Activate()
/// </summary>
public class ObjectiveMarker : MonoBehaviour
{
    [Tooltip("The child GameObject that holds the icon (Quad or Sprite). " +
             "Its layer must be set to 'Minimap'.")]
    public GameObject markerVisual;

    [Tooltip("If true the marker is visible when the scene starts.")]
    public bool activeOnStart = true;

    void Start()
    {
        if (activeOnStart)
            Activate();
        else
            Deactivate();
    }

    /// <summary>Show the marker on the minimap.</summary>
    public void Activate()
    {
        if (markerVisual != null)
            markerVisual.SetActive(true);
    }

    /// <summary>Hide the marker from the minimap.</summary>
    public void Deactivate()
    {
        if (markerVisual != null)
            markerVisual.SetActive(false);
    }
}
