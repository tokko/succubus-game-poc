using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 7f;
    public float rotationSpeed = 15f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    private Rigidbody _rb;
    private Camera _cam;
    private Animator _animator;
    private bool _isGrounded;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        _cam = Camera.main;
    }

    void Update()
    {
        CheckGround();
        HandleJump();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        _isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
        {
            Vector3 vel = _rb.linearVelocity;
            vel.x = 0f;
            vel.z = 0f;
            _rb.linearVelocity = vel;
            if (_animator != null)
                _animator.SetFloat("speed", 0f);
            return;
        }

        // Camera-relative horizontal movement direction
        Vector3 camForward = _cam.transform.forward;
        Vector3 camRight   = _cam.transform.right;
        camForward.y = 0f;
        camRight.y   = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        Vector3 targetVelocity = moveDir * moveSpeed;
        targetVelocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = targetVelocity;

        float speed = new Vector3(targetVelocity.x, 0f, targetVelocity.z).magnitude;
        if (_animator != null)
            _animator.SetFloat("speed", speed);

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot,
                rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _rb.linearVelocity = vel;
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
    }
}
