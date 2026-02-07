using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;
    public float panBorderThickness = 10f;
    public bool useScreenEdgePan = false; // Optional: pan when mouse at screen edge

    [Header("Zoom Settings")]
    public float scrollSpeed = 5f;
    public float minY = 5f;
    public float maxY = 30f;

    [Header("Rotation Settings (Optional)")]
    public float rotationSpeed = 100f;
    public bool enableRotation = true;

    [Header("Boundaries")]
    public float minX = 0f;
    public float maxX = 10f;
    public float minZ = 0f;
    public float maxZ = 10f;

    [Header("Smoothing")]
    public float smoothTime = 0.2f;

    private Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        HandleMovement();
        HandleZoom();
        if (enableRotation) HandleRotation();

        // Smooth camera movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        // WASD / Arrow Keys - CAMERA RELATIVE
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            moveDirection += transform.forward;  // ← FIX: Camera's forward
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            moveDirection += -transform.forward; // ← Camera's back
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            moveDirection += -transform.right;   // ← Camera's left
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            moveDirection += transform.right;    // ← Camera's right

        // Optional: Screen edge panning (same as before)
        if (useScreenEdgePan)
        {
            if (Input.mousePosition.x >= Screen.width - panBorderThickness)
                moveDirection += transform.right;
            if (Input.mousePosition.x <= panBorderThickness)
                moveDirection += -transform.right;
            if (Input.mousePosition.y >= Screen.height - panBorderThickness)
                moveDirection += transform.forward;
            if (Input.mousePosition.y <= panBorderThickness)
                moveDirection += -transform.forward;
        }

        // Flatten movement (prevent Y-axis movement from camera angle)
        moveDirection.y = 0;

        // Apply movement
        Vector3 targetMove = moveDirection.normalized * panSpeed * Time.deltaTime;
        targetPosition += targetMove;

        // Clamp to boundaries
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        targetPosition.y -= scroll * scrollSpeed * 100f * Time.deltaTime;
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
    }

    void HandleRotation()
    {
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}