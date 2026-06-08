using UnityEngine;

// Top-down orthographic camera that follows the player and renders to a RenderTexture
// displayed in the minimap UI.

[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("View")]
    [Tooltip("How many units wide the minimap shows. Increase to zoom out.")]
    public float viewSize = 30f;

    [Tooltip("Height above the player to position the camera.")]
    public float cameraHeight = 50f;

    [Header("Render Texture")]
    public RenderTexture renderTexture;

    private Camera minimapCam;

    void Start()
    {
        minimapCam = GetComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = viewSize;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark background
        minimapCam.nearClipPlane = 0.1f;
        minimapCam.farClipPlane = cameraHeight + 20f;

        if (renderTexture != null)
            minimapCam.targetTexture = renderTexture;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Stay directly above the player, always looking straight down
        transform.position = new Vector3(player.position.x, player.position.y + cameraHeight, player.position.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Look straight down
    }
}
