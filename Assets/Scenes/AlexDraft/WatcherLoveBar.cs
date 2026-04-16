using UnityEngine;
using UnityEngine.UI;

public class WatcherLoveBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage;                 // Image Type = Filled (Horizontal)
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

    // Full when currentLove = 0. Empty when currentLove >= loveNeededToConvert.
    public void SetValues(int currentLove, int loveNeededToConvert)
    {
        loveNeededToConvert = Mathf.Max(1, loveNeededToConvert);

        float normalized = 1f - (currentLove / (float)loveNeededToConvert);
        normalized = Mathf.Clamp01(normalized);

        if (fillImage != null)
            fillImage.fillAmount = normalized;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Because this bar is a CHILD of the watcher, local positioning is simplest.
        transform.localPosition = localOffset;

        if (faceCamera)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }
    }
}
