using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    [Header("Orbit")]
    public float yaw = 0f;                  // 0 = camera behind a character facing +Z (camera at -Z)
    public float pitch = 15f;
    public float distance = 4f;
    public float lookAtHeightOffset = 1.5f; // mid-chest on a 1.8m humanoid

    [Header("Over-the-shoulder")]
    public float shoulderOffset = 0.4f;     // lateral shift along camera-right; character ends in left third

    [Header("Zoom")]
    public float minDistance = 0.3f;
    public float maxDistance = 25f;
    public float zoomSpeed = 5f;

    [Header("Rotation")]
    public float keyRotateSpeed = 90f;
    public float dragSensitivity = 0.25f;
    public float minPitch = 5f;
    public float maxPitch = 85f;

    [Header("Auto-recenter (forward motion only)")]
    public float defaultYawOffset = 0f;     // added to target.eulerAngles.y when recentering
    public float defaultPitch = 15f;
    public float recenterGracePeriod = 1.0f;
    public float recenterSpeed = 2.5f;
    public float forwardThreshold = 0.5f;   // Vertical input must exceed this to trigger recenter

    [Header("Smoothing")]
    public float positionSmoothing = 10f;

    private Vector3 _lastMousePos;
    private bool _dragging;
    private float _lastDragEndTime = -999f;

    void LateUpdate()
    {
        if (target == null) return;

        HandleRotation();
        HandleZoom();
        HandleAutoRecenter();

        Vector3 cameraRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 lookAt = target.position + Vector3.up * lookAtHeightOffset + cameraRight * shoulderOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = lookAt + rot * new Vector3(0f, 0f, -distance);

        transform.position = Vector3.Lerp(transform.position, desiredPos,
            positionSmoothing * Time.deltaTime);
        transform.LookAt(lookAt);
    }

    void HandleRotation()
    {
        if (Input.GetKey(KeyCode.Q)) yaw -= keyRotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) yaw += keyRotateSpeed * Time.deltaTime;

        if (Input.GetMouseButtonDown(2))
        {
            _dragging = true;
            _lastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(2))
        {
            _dragging = false;
            _lastDragEndTime = Time.time;
        }

        if (_dragging && Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            yaw   += delta.x * dragSensitivity;
            pitch -= delta.y * dragSensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
            _lastMousePos = Input.mousePosition;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
    }

    void HandleAutoRecenter()
    {
        if (_dragging) return;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E)) return;
        if (Time.time - _lastDragEndTime < recenterGracePeriod) return;

        if (Input.GetAxis("Vertical") <= forwardThreshold) return;

        float targetYaw = target.eulerAngles.y + defaultYawOffset;
        yaw = Mathf.LerpAngle(yaw, targetYaw, recenterSpeed * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, defaultPitch, recenterSpeed * Time.deltaTime);
    }
}
