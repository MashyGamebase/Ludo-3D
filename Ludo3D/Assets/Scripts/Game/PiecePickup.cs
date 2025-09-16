using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PiecePickup : MonoBehaviour
{
    [Header("Hover/Lift")]
    [Tooltip("Desired hover amount while dragging (meters). Limited by Max Lift Height.")]
    [Min(0f)] public float baseHoverHeight = 0.25f;

    [Tooltip("Maximum allowed height above the ground while dragging (meters).")]
    [Min(0.01f)] public float maxLiftHeight = 1.0f;

    [Header("Follow Tuning")]
    [Tooltip("Higher = snappier following toward the mouse target.")]
    [Range(1f, 60f)] public float followLerp = 20f;

    [Tooltip("Cap the object translation per physics tick (meters/sec).")]
    [Min(0.1f)] public float maxMoveSpeed = 12f;

    [Header("Release")]
    [Tooltip("Downward velocity applied on release for a satisfying plop.")]
    [Min(0f)] public float plopForce = 4f;

    [Header("Grounding")]
    [Tooltip("Layers considered 'ground' when figuring out height and clamp.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Max distance for the mouse ray when looking for ground.")]
    [Min(5f)] public float rayMaxDistance = 200f;

    private Rigidbody rb;
    private Camera cam;

    // drag state
    private bool isDragging;
    private Vector3 targetPoint;
    private Vector3 horizontalGrabOffset; // preserves the XY screen pick offset on ground (X/Z in world)
    private float clampedHover;            // effective hover during drag (min(baseHoverHeight, maxLiftHeight))

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        if (!cam)
        {
            Debug.LogWarning("[MouseDragRigidbody3D] No Camera.main found. Assign a MainCamera tag.");
        }
    }

    // -- Mouse Events (require a Collider on this object) ----------------------

    void OnMouseDown()
    {
        if (!rb || !cam) return;

        // Find where we clicked on THIS object to compute a horizontal offset,
        // so the object doesn't jump when we start dragging.
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, rayMaxDistance))
        {
            // horizontal offset between object center and hit point
            Vector3 delta = transform.position - hit.point;
            horizontalGrabOffset = new Vector3(delta.x, 0f, delta.z);
        }
        else
        {
            horizontalGrabOffset = Vector3.zero;
        }

        // turn off gravity while dragging (we'll use MovePosition)
        rb.useGravity = false;
        isDragging = true;

        clampedHover = Mathf.Min(baseHoverHeight, maxLiftHeight);
    }

    void OnMouseDrag()
    {
        if (!rb || !cam) return;

        // Project mouse onto ground to get a base point
        if (TryGetGroundPointUnderMouse(out Vector3 groundPt, out float groundY))
        {
            // keep the horizontal grab offset
            Vector3 baseXZ = groundPt + horizontalGrabOffset;

            // final Y is ground + min(hover, maxLift)
            float targetY = Mathf.Min(groundY + maxLiftHeight, groundY + clampedHover);

            targetPoint = new Vector3(baseXZ.x, targetY, baseXZ.z);
        }
        else
        {
            // Fallback: use a plane at current Y minus desired hover
            Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y - clampedHover, 0f));
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (plane.Raycast(ray, out float t))
                targetPoint = ray.GetPoint(t) + horizontalGrabOffset;
            else
                targetPoint = transform.position;
        }
    }

    void OnMouseUp()
    {
        if (!rb) return;

        isDragging = false;

        // Re-enable gravity and give a small downward nudge
        rb.useGravity = true;

        // Wipe existing vertical motion for a consistent plop, keep horizontal
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (plopForce > 0f)
            rb.AddForce(Vector3.down * plopForce, ForceMode.VelocityChange);
    }

    // -- Physics move ----------------------------------------------------------

    void FixedUpdate()
    {
        if (!rb) return;

        if (isDragging)
        {
            // Smoothly move toward targetPoint, clamped by maxMoveSpeed
            Vector3 desired = targetPoint;
            Vector3 next = ExponentialLerp(rb.position, desired, followLerp, Time.fixedDeltaTime);
            Vector3 delta = next - rb.position;

            float maxStep = maxMoveSpeed * Time.fixedDeltaTime;
            if (delta.magnitude > maxStep)
                delta = delta.normalized * maxStep;

            rb.MovePosition(rb.position + delta);

            // Keep rotations unaffected unless you want to lock them:
            // rb.MoveRotation(Quaternion.Euler(0, rb.rotation.eulerAngles.y, 0));
        }
    }

    // -- Helpers ---------------------------------------------------------------

    // Exponential smoothing toward target; stable across framerates.
    private static Vector3 ExponentialLerp(Vector3 current, Vector3 target, float lerpPerSecond, float dt)
    {
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, lerpPerSecond) * dt);
        return Vector3.LerpUnclamped(current, target, t);
    }

    private bool TryGetGroundPointUnderMouse(out Vector3 point, out float groundY)
    {
        point = Vector3.zero;
        groundY = 0f;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Prefer a ground hit (so we know actual ground Y for clamping)
        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            groundY = hit.point.y;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (!isDragging) return;

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.35f);
        Gizmos.DrawSphere(targetPoint, 0.08f);

        // Visualize max height clamp above ground if we have a ground ray at target XZ
        if (cam && TryGetGroundPointUnderMouse(out var ground, out var gy))
        {
            float capY = gy + maxLiftHeight;
            Vector3 a = new Vector3(ground.x, gy, ground.z);
            Vector3 b = new Vector3(ground.x, capY, ground.z);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.7f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(b, 0.06f);
        }
    }
#endif
}
