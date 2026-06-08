using UnityEngine;

// World-space minimap objective marker.
// Place this on an empty GameObject at the location you want marked
// (e.g. the stadium entrance, the street boss spawn point, etc.).

public class ObjectiveMarker : MonoBehaviour
{
    [Tooltip("The child GameObject that holds the icon (Quad or Sprite). " +
             "Its layer must be set to 'Minimap'.")]
    [SerializeField] private GameObject markerVisual;

    [Tooltip("If true the marker is visible when the scene starts.")]
    [SerializeField] private bool activeOnStart = true;

    void Start()
    {
        if (activeOnStart)
            Activate();
        else
            Deactivate();
    }

    // Show the marker on the minimap.<
    public void Activate()
    {
        if (markerVisual != null)
            markerVisual.SetActive(true);
    }

    // Hide the marker from the minimap.
    public void Deactivate()
    {
        if (markerVisual != null)
            markerVisual.SetActive(false);
    }
}
