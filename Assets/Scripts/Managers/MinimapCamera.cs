using UnityEngine;

/// <summary>
/// Top-down orthographic camera that follows the player and renders to a RenderTexture
/// displayed in the minimap UI.
///
/// SETUP:
/// 1. Create an empty GameObject named "MinimapCamera"
/// 2. Add a Camera component to it
/// 3. Add this script to it
/// 4. Create a RenderTexture asset (Project window > right-click > Create > Render Texture)
///    Set size to 256x256, name it "MinimapRT"
/// 5. Drag the RenderTexture into this script's "renderTexture" field
/// 6. Also drag the RenderTexture into the Camera's "Target Texture" field
/// 7. Set the Camera's Projection to Orthographic
/// 8. Set the Camera's Culling Mask to exclude the "UI" layer (to avoid showing UI in minimap)
/// 9. Assign your Player transform
/// </summary>
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
