using UnityEngine;

/// <summary>
/// Free-fly inspection camera for the bake-off scene.
///   WASD       : forward / back / strafe (camera-relative, horizontal plane)
///   Q / E      : down / up
///   RMB / MMB drag : pitch + yaw look
///   Scroll     : adjust move speed (held value)
///   Shift      : 4x speed boost while held
///   1 / 2 / 3  : warp camera to inspect Slot_A / Slot_B / Slot_C (if present in scene)
///
/// Sits on Main Camera. No physics, no target — just transform manipulation.
/// </summary>
public class InspectorCamera : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 3.0f;
    public float speedMin  = 0.3f;
    public float speedMax  = 30f;
    public float boostMul  = 4f;

    [Header("Look")]
    public float lookSensitivity = 0.25f;
    public float pitchMin = -85f;
    public float pitchMax =  85f;

    [Header("Slot warp targets (auto-resolved if names match)")]
    public Transform slotA;
    public Transform slotB;
    public Transform slotC;
    public Vector3   warpOffset = new Vector3(0f, 1.4f, -2.3f);

    float _yaw;
    float _pitch;
    Vector3 _lastMouse;
    bool _looking;

    void Awake()
    {
        var e = transform.rotation.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizePitch(e.x);

        if (slotA == null) slotA = GameObject.Find("Slot_A")?.transform;
        if (slotB == null) slotB = GameObject.Find("Slot_B")?.transform;
        if (slotC == null) slotC = GameObject.Find("Slot_C")?.transform;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
        HandleSpeedScroll();
        HandleWarp();
    }

    void HandleLook()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            _looking = true;
            _lastMouse = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1) && !Input.GetMouseButton(2)) _looking = false;
        if (Input.GetMouseButtonUp(2) && !Input.GetMouseButton(1)) _looking = false;

        if (_looking && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
        {
            Vector3 d = Input.mousePosition - _lastMouse;
            _yaw   += d.x * lookSensitivity;
            _pitch -= d.y * lookSensitivity;
            _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);
            _lastMouse = Input.mousePosition;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }

    void HandleMove()
    {
        float h = Input.GetAxisRaw("Horizontal");        // A/D
        float v = Input.GetAxisRaw("Vertical");          // W/S
        float u = (Input.GetKey(KeyCode.E) ? 1f : 0f)    // E up
               - (Input.GetKey(KeyCode.Q) ? 1f : 0f);    // Q down

        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f) && Mathf.Approximately(u, 0f)) return;

        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 rgt = transform.right;   rgt.y = 0f; rgt.Normalize();

        Vector3 dir = fwd * v + rgt * h + Vector3.up * u;
        float speed = baseSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? boostMul : 1f);
        transform.position += dir.normalized * speed * Time.deltaTime;
    }

    void HandleSpeedScroll()
    {
        float s = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(s, 0f)) return;
        baseSpeed = Mathf.Clamp(baseSpeed * (1f + s * 1.5f), speedMin, speedMax);
    }

    void HandleWarp()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && slotA != null) WarpTo(slotA);
        if (Input.GetKeyDown(KeyCode.Alpha2) && slotB != null) WarpTo(slotB);
        if (Input.GetKeyDown(KeyCode.Alpha3) && slotC != null) WarpTo(slotC);
    }

    void WarpTo(Transform t)
    {
        transform.position = t.position + warpOffset;
        Vector3 lookAt = t.position + Vector3.up * 1.0f;
        Vector3 dir = (lookAt - transform.position).normalized;
        _pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        _yaw   =  Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    static float NormalizePitch(float x)
    {
        if (x > 180f) x -= 360f;
        return Mathf.Clamp(x, -180f, 180f);
    }
}
