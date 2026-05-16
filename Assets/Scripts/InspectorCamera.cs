using UnityEngine;

/// <summary>
/// Orbital inspection camera for the bake-off scene.
///   MMB or RMB drag : orbit yaw + pitch around the focus point
///   Scroll          : zoom (distance to focus, clamped minDistance..maxDistance)
///   WASD            : pan focus point (camera-relative horizontal)
///   Q / E           : pan focus point down / up
///   Shift           : 4x pan speed
///   1 / 2 / 3       : snap focus to Slot A / B / C, auto-fit distance to slot bounds
///   F               : re-frame current focus (auto-distance)
///
/// Designed to live on Main Camera. No physics. Auto-resolves Slot_A/B/C transforms
/// by name on Awake so the user can drop this into a scene without wiring.
/// </summary>
public class InspectorCamera : MonoBehaviour
{
    [Header("Orbit target")]
    public Vector3 focus = new Vector3(0f, 1.0f, 0f);
    public float distance = 4f;
    public float yaw = 0f;
    public float pitch = 10f;

    [Header("Limits")]
    public float minDistance = 0.4f;
    public float maxDistance = 15f;
    public float minPitch = -85f;
    public float maxPitch =  85f;

    [Header("Sensitivities")]
    public float lookSensitivity = 0.25f;
    public float zoomFactor = 0.15f;       // scroll multiplier (fraction of current distance per tick)
    public float panSpeed = 2.5f;
    public float boostMul = 4f;

    [Header("Auto-fit on slot snap")]
    public float fitFovMargin = 1.25f;     // distance = bounds.height / 2 / tan(fov/2) * margin

    [Header("Slot warp targets (auto-resolved if names match)")]
    public Transform slotA;
    public Transform slotB;
    public Transform slotC;

    Vector3 _lastMouse;
    bool _dragging;
    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (slotA == null) slotA = GameObject.Find("Slot_A")?.transform;
        if (slotB == null) slotB = GameObject.Find("Slot_B")?.transform;
        if (slotC == null) slotC = GameObject.Find("Slot_C")?.transform;
    }

    void Start()
    {
        // Initial framing on Slot_A if it exists, else use the inspector defaults.
        if (slotA != null) WarpAndFit(slotA);
        ApplyTransform();
    }

    void Update()
    {
        HandleOrbit();
        HandleZoom();
        HandlePan();
        HandleSnap();
        ApplyTransform();
    }

    void HandleOrbit()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            _dragging = true;
            _lastMouse = Input.mousePosition;
        }
        if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) _dragging = false;

        if (_dragging)
        {
            Vector3 d = Input.mousePosition - _lastMouse;
            yaw   += d.x * lookSensitivity;
            pitch -= d.y * lookSensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
            _lastMouse = Input.mousePosition;
        }
    }

    void HandleZoom()
    {
        float s = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(s, 0f)) return;
        // Multiplicative zoom — feels natural at both ends of the range.
        distance = Mathf.Clamp(distance * (1f - s * zoomFactor * 10f), minDistance, maxDistance);
    }

    void HandlePan()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float u = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f) && Mathf.Approximately(u, 0f)) return;

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);   // pan in camera-yaw-only plane
        Vector3 fwd = rot * Vector3.forward;
        Vector3 rgt = rot * Vector3.right;
        Vector3 dir = (fwd * v + rgt * h + Vector3.up * u).normalized;

        float speed = panSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? boostMul : 1f);
        // Pan speed scales with distance so far zoom feels equally responsive.
        focus += dir * speed * distance * 0.25f * Time.deltaTime;
    }

    void HandleSnap()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && slotA != null) WarpAndFit(slotA);
        if (Input.GetKeyDown(KeyCode.Alpha2) && slotB != null) WarpAndFit(slotB);
        if (Input.GetKeyDown(KeyCode.Alpha3) && slotC != null) WarpAndFit(slotC);
        if (Input.GetKeyDown(KeyCode.F))
        {
            // Re-fit on whatever the focus is closest to
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var t in new[] { slotA, slotB, slotC })
            {
                if (t == null) continue;
                float d = Vector3.Distance(t.position, focus);
                if (d < bestDist) { best = t; bestDist = d; }
            }
            if (best != null) WarpAndFit(best);
        }
    }

    void WarpAndFit(Transform slot)
    {
        // Compute renderer bounds of the slot's children, set focus to their centre,
        // set distance to fit the bounds vertically in the camera FOV.
        var renderers = slot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            focus = slot.position + Vector3.up * 1.0f;
            distance = 4f;
        }
        else
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            focus = b.center;
            float fov = _cam != null ? _cam.fieldOfView : 60f;
            float halfFovRad = fov * 0.5f * Mathf.Deg2Rad;
            float requiredForHeight = b.extents.y / Mathf.Tan(halfFovRad);
            float requiredForWidth  = b.extents.x / Mathf.Tan(halfFovRad) * (Screen.height / (float)Mathf.Max(1, Screen.width));
            distance = Mathf.Clamp(Mathf.Max(requiredForHeight, requiredForWidth) * fitFovMargin, minDistance, maxDistance);
        }
        // Default a slight downward yaw so we view from front-three-quarter
        yaw = 0f;
        pitch = 10f;
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = focus + rot * new Vector3(0f, 0f, -distance);
        transform.position = pos;
        transform.LookAt(focus);
    }
}
