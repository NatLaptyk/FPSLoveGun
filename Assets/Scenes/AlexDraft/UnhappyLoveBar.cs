using UnityEngine;
using UnityEngine.UI;

public class UnhappyLoveBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;   // Image Type = Filled (Horizontal)
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private bool faceCamera = true;

    private Transform target;
    private Camera cam;

    public void Init(Transform followTarget, int startingUnhappinessLevel)
    {
        target = followTarget;
        cam = Camera.main;

        // Start full at unhappinessLevel (0 love received => full bar)
        SetValues(currentLoveReceived: 0, unhappinessLevel: Mathf.Max(1, startingUnhappinessLevel));
    }

    public void SetValues(int currentLoveReceived, int unhappinessLevel)
    {
        unhappinessLevel = Mathf.Max(1, unhappinessLevel);

        // Full when 0 received; decreases as currentLoveReceived increases
        float normalized = 1f - (currentLoveReceived / (float)unhappinessLevel);
        normalized = Mathf.Clamp01(normalized);

        if (fillImage != null)
            fillImage.fillAmount = normalized;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + worldOffset;

        if (faceCamera)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
