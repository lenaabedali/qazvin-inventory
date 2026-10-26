using UnityEngine;

/// <summary>
/// Simple over-the-shoulder third-person controller for the demo.
/// The player's pivot (this transform) should be at their feet.
/// Swap in your own controller in a real game and add it to InventoryToggle's "Disable While Open" list.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 12f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Camera")]
    public Camera cam;
    public float lookSensitivity = 2f;
    [Tooltip("Height above the player's feet that the camera orbits around.")]
    public float pivotHeight = 1.6f;
    public float distance = 4f;
    [Tooltip("Over-the-shoulder offset so the crosshair isn't hidden behind the character.")]
    public Vector3 shoulderOffset = new Vector3(0.6f, 0f, 0f);
    public float minPitch = -35f;
    public float maxPitch = 70f;
    [Tooltip("Keeps the camera from clipping through walls.")]
    public float cameraRadius = 0.2f;
    public LayerMask cameraCollisionMask = ~0;

    CharacterController controller;
    float yaw;
    float pitch = 15f;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cam == null) cam = Camera.main;
        yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        // Click to re-lock the cursor (e.g. after pressing Esc in the editor)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (InputHelper.PrimaryDown)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else
        {
            Vector2 look = InputHelper.LookDelta * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
        }

        // Move relative to where the camera is facing
        Vector2 input = InputHelper.Move;
        Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Turn the character toward the direction it's walking
        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (controller.isGrounded && InputHelper.JumpDown) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (cam == null) return;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * pivotHeight;
        Vector3 shoulder = pivot + rot * shoulderOffset;
        Vector3 back = rot * Vector3.back;

        // Pull the camera in if a wall is between it and the player
        float dist = distance;
        foreach (var hit in Physics.SphereCastAll(shoulder, cameraRadius, back, distance, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance < dist) dist = hit.distance;
        }

        cam.transform.position = shoulder + back * dist;
        cam.transform.rotation = rot;
    }
}
