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

    private float m_RotateSpeed = 2f;

    public float movementSpeed
    {
        get => m_MovementSpeed;
        set => m_MovementSpeed = value;
    }

    [SerializeField, Tooltip("Snap to target position when this component is enabled.")]
    private bool m_SnapOnEnable = true;

    public bool snapOnEnable
    {
        get => m_SnapOnEnable;
        set => m_SnapOnEnable = value;
    }

    private const float ROTATION_THRESHOLD = 0.05f; // 5% threshold for rotation
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
        // Calculate the direction from the transform to the rotation target
        var directionToRotationTarget = transform.position - m_RotationTarget.position;
        directionToRotationTarget.y = 0f; // Ignore the Y-axis

        // Calculate the rotation to face the rotation target
        Quaternion targetRotation = Quaternion.LookRotation(directionToRotationTarget.normalized, Vector3.up);

        // Check if the current rotation is sufficiently different from the target rotation
        float angleDifference = m_FollowInLocalSpace
            ? Quaternion.Angle(transform.localRotation, targetRotation)
            : Quaternion.Angle(transform.rotation, targetRotation);

        if (angleDifference > ROTATION_THRESHOLD * 180f)
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
        UpdateRotation();
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

// using UnityEngine;

// public class FollowPosition : MonoBehaviour
// {
//     [SerializeField, Tooltip("The transform to follow for position.")]
//     private Transform m_Target;

//     public Transform target
//     {
//         get => m_Target;
//         set => m_Target = value;
//     }

//     [SerializeField, Tooltip("The amount to offset the target's position when following. This position is relative/local to the target object.")]
//     //private Vector3 m_TargetOffset = new Vector3(0f, 0f, 0.5f);
//     private Vector3 offset = new Vector3(0f, 0f, 0f);


//     [SerializeField, Tooltip("The transform to follow for rotation. If not set, it will default to the main camera.")]
//     private Transform m_RotationTarget;

//     public Transform rotationTarget
//     {
//         get => m_RotationTarget;
//         set => m_RotationTarget = value;
//     }

//     [SerializeField, Tooltip("If true, read the local transform of the target to follow, otherwise read the world transform.")]
//     private bool m_FollowInLocalSpace;

//     public bool followInLocalSpace
//     {
//         get => m_FollowInLocalSpace;
//         set => m_FollowInLocalSpace = value;
//     }

//     [SerializeField, Tooltip("If true, apply the target offset in local space. If false, apply the target offset in world space.")]
//     private bool m_ApplyTargetInLocalSpace;

//     public bool applyTargetInLocalSpace
//     {
//         get => m_ApplyTargetInLocalSpace;
//         set => m_ApplyTargetInLocalSpace = value;
//     }

//     [SerializeField, Tooltip("Movement speed used when smoothing to new target. Lower values mean the follow lags further behind the target.")]
//     private float m_MovementSpeed = 4f;

//     private float m_RotateSpeed = 2f;

//     public float movementSpeed
//     {
//         get => m_MovementSpeed;
//         set => m_MovementSpeed = value;
//     }

//     [SerializeField, Tooltip("Snap to target position when this component is enabled.")]
//     private bool m_SnapOnEnable = true;

//     public bool snapOnEnable
//     {
//         get => m_SnapOnEnable;
//         set => m_SnapOnEnable = value;
//     }

//     private void Start()
//     {
//         // Default to main camera if rotation target is not set
//         if (m_RotationTarget == null)
//         {
//             var mainCamera = Camera.main;
//             if (mainCamera != null)
//                 m_RotationTarget = mainCamera.transform;
//         }

//         if (m_SnapOnEnable)
//         {
//             SnapToTarget();
//         }
//     }

//     private void Update()
//     {
//         if (m_Target == null || m_RotationTarget == null)
//             return;

//         UpdateRotation();
//         UpdatePosition();
//     }

//     private void UpdateRotation()
//     {
//         // Calculate the direction from the transform to the rotation target
//         var directionToRotationTarget = transform.position - m_RotationTarget.position;
//         directionToRotationTarget.y = 0f; // Ignore the Y-axis

//         // Calculate the rotation to face the rotation target
//         Quaternion targetRotation = Quaternion.LookRotation(directionToRotationTarget.normalized, Vector3.up);

//         // Smoothly interpolate the rotation
//         if (m_FollowInLocalSpace)
//             transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, m_RotateSpeed * Time.deltaTime);
//         else
//             transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_RotateSpeed * Time.deltaTime);
//     }

//     private void UpdatePosition()
//     {
//         // Rotate the offset vector based on the object's rotation
//         Vector3 rotatedOffset = transform.rotation * offset;

//         // Calculate the target position with the rotated offset
//         Vector3 targetPosition;
//         if (m_FollowInLocalSpace)
//             targetPosition = m_Target.localPosition + (m_ApplyTargetInLocalSpace ? offset : rotatedOffset);
//         else
//             targetPosition = m_Target.position + (m_ApplyTargetInLocalSpace ? m_Target.TransformVector(offset) : rotatedOffset);

//         // Smoothly interpolate the position
//         if (m_FollowInLocalSpace)
//             transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, m_MovementSpeed * Time.deltaTime);
//         else
//             transform.position = Vector3.Lerp(transform.position, targetPosition, m_MovementSpeed * Time.deltaTime);
//     }

//     private void SnapToTarget()
//     {
//         UpdateRotation();
//         if (m_FollowInLocalSpace)
//             transform.localPosition = m_Target.localPosition + (m_ApplyTargetInLocalSpace ? offset : transform.localRotation * offset);
//         else
//             transform.position = m_Target.position + (m_ApplyTargetInLocalSpace ? m_Target.TransformVector(offset) : transform.rotation * offset);
//     }

//     public void SetOffset(Vector3 newOffset)
//     {
//         offset = newOffset;
//     }
// }