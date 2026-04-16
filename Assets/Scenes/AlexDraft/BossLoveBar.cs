using UnityEngine;
using UnityEngine.UI;

public class BossLoveBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage;  // Image Type = Filled (Horizontal)
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.8f, 0f);

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;

    private Transform target;
    private Camera cam;

    public void Init(Transform followTarget)
    {
        target = followTarget;
        cam = Camera.main;
    }

    /// <summary>
    /// Full when CurrentLove=0. Empty when CurrentLove >= loveNeededToDefeat.
    /// </summary>
    public void SetValues(int currentLove, int loveNeededToDefeat)
    {
        loveNeededToDefeat = Mathf.Max(1, loveNeededToDefeat);

        float normalized = 1f - (currentLove / (float)loveNeededToDefeat);
        normalized = Mathf.Clamp01(normalized);

        if (fillImage != null)
            fillImage.fillAmount = normalized;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // If this bar is a child of the boss, localPosition is simplest/reliable.
        transform.localPosition = localOffset;

        if (faceCamera)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }
    }
}
