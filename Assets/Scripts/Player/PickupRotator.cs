using UnityEngine;

// Simple rotating/bobbing animation for pickup items.

public class PickupRotator : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 90f;   // Degrees per second
    [SerializeField] private float bobSpeed = 2f;       // How fast it bobs up and down
    [SerializeField] private float bobHeight = 0.3f;    // How high it bobs

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Rotate
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        // Bob up and down
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }
}
