using UnityEngine;

/// <summary>
/// Makes a carried bag swing back as the character walks, with a little bounce in step.
/// Put this on the pivot the bag hangs from (the hand), with the bag as its child.
/// </summary>
public class BagSway : MonoBehaviour
{
    [Tooltip("The character carrying the bag. Defaults to the parent.")]
    public Transform character;
    public float swingPerSpeed = 4f;
    public float maxAngle = 22f;
    public float smoothTime = 0.15f;
    public float stepBounce = 4f;
    public float stepFrequency = 9f;

    Vector3 lastPosition;
    float swing, swingVelocity;

    void Start()
    {
        if (character == null) character = transform.parent != null ? transform.parent : transform;
        lastPosition = character.position;
    }

    void LateUpdate()
    {
        if (character == null || Time.deltaTime <= 0f) return;

        Vector3 velocity = (character.position - lastPosition) / Time.deltaTime;
        lastPosition = character.position;

        float forwardSpeed = Vector3.Dot(velocity, character.forward);
        float target = Mathf.Clamp(forwardSpeed * swingPerSpeed, -maxAngle, maxAngle);
        swing = Mathf.SmoothDamp(swing, target, ref swingVelocity, smoothTime);

        float walking = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
        float bounce = Mathf.Sin(Time.time * stepFrequency) * stepBounce * walking;

        transform.localRotation = Quaternion.Euler(swing, 0f, bounce);
    }
}
