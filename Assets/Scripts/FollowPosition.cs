//performance optimizations to adding threshold checks before performing Lerp and Slerp calculations. 
//This will help reduce unnecessary computations when the object is already close to its target position and rotation.
using UnityEngine;

public class FollowPosition : MonoBehaviour
{
    [SerializeField, Tooltip("The transform to follow for position.")]
    private Transform m_Target;

    public Transform target
    {
        get => m_Target;
        set => m_Target = value;
    }

    [SerializeField, Tooltip("The amount to offset the target's position when following. This position is relative/local to the target object.")]
    private Vector3 offset = new Vector3(0f, 0f, 0f);

    [SerializeField, Tooltip("The transform to follow for rotation. If not set, it will default to the main camera.")]
    private Transform m_RotationTarget;

    public Transform rotationTarget
    {
        get => m_RotationTarget;
        set => m_RotationTarget = value;
    }

    [SerializeField, Tooltip("If true, read the local transform of the target to follow, otherwise read the world transform.")]
    private bool m_FollowInLocalSpace;

    public bool followInLocalSpace
    {
        get => m_FollowInLocalSpace;
        set => m_FollowInLocalSpace = value;
    }

    [SerializeField, Tooltip("If true, apply the target offset in local space. If false, apply the target offset in world space.")]
    private bool m_ApplyTargetInLocalSpace;

    public bool applyTargetInLocalSpace
    {
        get => m_ApplyTargetInLocalSpace;
        set => m_ApplyTargetInLocalSpace = value;
    }

    [SerializeField, Tooltip("Movement speed used when smoothing to new target. Lower values mean the follow lags further behind the target.")]
    private float m_MovementSpeed = 4f;

    public float movementSpeed
    {
        get => m_MovementSpeed;
        set => m_MovementSpeed = value;
    }

    [SerializeField, Tooltip("Rotation speed used when smoothing to new target. Lower values mean slower, more gradual rotation.")]
    private float m_RotateSpeed = 0.5f;

    public float rotateSpeed
    {
        get => m_RotateSpeed;
        set => m_RotateSpeed = value;
    }

    [SerializeField, Tooltip("Minimum rotation difference in degrees before rotation adjustment begins (deadzone). Higher values make it less eager to adjust.")]
    private float m_RotationDeadzoneDegrees = 2f;

    public float rotationDeadzoneDegrees
    {
        get => m_RotationDeadzoneDegrees;
        set => m_RotationDeadzoneDegrees = value;
    }

    [SerializeField, Tooltip("Snap to target position when this component is enabled.")]
    private bool m_SnapOnEnable = true;

    public bool snapOnEnable
    {
        get => m_SnapOnEnable;
        set => m_SnapOnEnable = value;
    }

    [SerializeField, Tooltip("If true, match the rotation target's rotation directly. If false, face away from rotation target (billboard style).")]
    private bool m_MatchTargetRotation = false;

    public bool matchTargetRotation
    {
        get => m_MatchTargetRotation;
        set => m_MatchTargetRotation = value;
    }

    private const float POSITION_THRESHOLD = 0.001f; // 0.1% threshold for position

    private void Start()
    {
        // Default to main camera if rotation target is not set
        if (m_RotationTarget == null)
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
                m_RotationTarget = mainCamera.transform;
        }

        if (m_SnapOnEnable)
        {
            SnapToTarget();
        }
    }

    private void Update()
    {
        if (m_Target == null || m_RotationTarget == null)
            return;

        UpdateRotation();
        UpdatePosition();
    }

    private void UpdateRotation()
    {
        Quaternion targetRotation;

        if (m_MatchTargetRotation)
        {
            // Match only the yaw (Y-axis rotation) of the rotation target
            // Extract the forward direction and project it onto the horizontal plane
            Vector3 targetForward = m_RotationTarget.forward;
            targetForward.y = 0f; // Zero out vertical component to get only horizontal direction
            
            // If the forward vector becomes zero (looking straight up/down), use the current forward
            if (targetForward.sqrMagnitude < 0.001f)
            {
                targetForward = transform.forward;
                targetForward.y = 0f;
            }
            
            // Create rotation from horizontal direction only (yaw only)
            targetRotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
        }
        else
        {
            // Calculate the direction from the transform to the rotation target (billboard mode - face away)
            var directionToRotationTarget = transform.position - m_RotationTarget.position;
            directionToRotationTarget.y = 0f; // Ignore the Y-axis

            // Calculate the rotation to face away from the rotation target
            targetRotation = Quaternion.LookRotation(directionToRotationTarget.normalized, Vector3.up);
        }

        // Check if the current rotation is sufficiently different from the target rotation
        float angleDifference = m_FollowInLocalSpace
            ? Quaternion.Angle(transform.localRotation, targetRotation)
            : Quaternion.Angle(transform.rotation, targetRotation);

        if (angleDifference > m_RotationDeadzoneDegrees)
        {
            // Smoothly interpolate the rotation
            if (m_FollowInLocalSpace)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, m_RotateSpeed * Time.deltaTime);
            else
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotateSpeed * Time.deltaTime);
        }
    }

    private void UpdatePosition()
    {
        // Rotate the offset vector based on the object's rotation
        Vector3 rotatedOffset = transform.rotation * offset;

        // Calculate the target position with the rotated offset
        Vector3 targetPosition;
        if (m_FollowInLocalSpace)
            targetPosition = m_Target.localPosition + (m_ApplyTargetInLocalSpace ? offset : rotatedOffset);
        else
            targetPosition = m_Target.position + (m_ApplyTargetInLocalSpace ? m_Target.TransformVector(offset) : rotatedOffset);

        // Check if the current position is sufficiently different from the target position
        float positionDifference = m_FollowInLocalSpace
            ? Vector3.Distance(transform.localPosition, targetPosition)
            : Vector3.Distance(transform.position, targetPosition);

        if (positionDifference > POSITION_THRESHOLD)
        {
            // Smoothly interpolate the position
            if (m_FollowInLocalSpace)
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, m_MovementSpeed * Time.deltaTime);
            else
                transform.position = Vector3.Lerp(transform.position, targetPosition, m_MovementSpeed * Time.deltaTime);
        }
    }

    private void SnapToTarget()
    {
        // Snap rotation immediately
        if (m_RotationTarget != null)
        {
            if (m_MatchTargetRotation)
            {
                // Match only the yaw (Y-axis rotation) of the rotation target
                Vector3 targetForward = m_RotationTarget.forward;
                targetForward.y = 0f; // Zero out vertical component
                
                if (targetForward.sqrMagnitude < 0.001f)
                {
                    targetForward = Vector3.forward; // Default forward if looking straight up/down
                }
                
                Quaternion targetRotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
                
                if (m_FollowInLocalSpace)
                    transform.localRotation = targetRotation;
                else
                    transform.rotation = targetRotation;
            }
            else
            {
                // Face away from rotation target (billboard mode)
                var directionToRotationTarget = transform.position - m_RotationTarget.position;
                directionToRotationTarget.y = 0f;
                Quaternion targetRotation = Quaternion.LookRotation(directionToRotationTarget.normalized, Vector3.up);
                
                if (m_FollowInLocalSpace)
                    transform.localRotation = targetRotation;
                else
                    transform.rotation = targetRotation;
            }
        }

        // Snap position
        if (m_FollowInLocalSpace)
            transform.localPosition = m_Target.localPosition + (m_ApplyTargetInLocalSpace ? offset : transform.localRotation * offset);
        else
            transform.position = m_Target.position + (m_ApplyTargetInLocalSpace ? m_Target.TransformVector(offset) : transform.rotation * offset);
    }

    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
}