using UnityEngine;

/// <summary>
/// Translates a GameObject based on timeline scroll input.
/// Scrolling right moves the object forward, scrolling left moves it backward.
/// </summary>
public class TimelineDrivenTranslation : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("The TimelineController to listen to")]
    private TimelineController timelineController;
    
    [SerializeField, Tooltip("The object to move (leave empty to move this GameObject)")]
    private Transform objectToMove;
    
    [Header("Movement Settings")]
    [SerializeField, Tooltip("Scale factor for movement (higher = more sensitive)")]
    private float movementScale = 1f;
    
    [SerializeField, Tooltip("Use local forward direction instead of world forward")]
    private bool useLocalForward = true;
    
    [Header("Constraints")]
    [SerializeField, Tooltip("Limit movement to specific axes")]
    private bool constrainX = false;
    [SerializeField, Tooltip("Limit movement to specific axes")]
    private bool constrainY = false;
    [SerializeField, Tooltip("Limit movement to specific axes")]
    private bool constrainZ = false;

    void Start()
    {
        // If no object specified, move this GameObject
        if (objectToMove == null)
        {
            objectToMove = transform;
        }
        
        // Subscribe to the timeline scroll event
        if (timelineController != null)
        {
            timelineController.TimelineScrolled += OnTimelineScrolled;
            Debug.Log($"[TimelineDrivenTranslation] Subscribed to TimelineController on {gameObject.name}");
        }
        else
        {
            Debug.LogError($"[TimelineDrivenTranslation] TimelineController not assigned on {gameObject.name}!");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe when destroyed to prevent memory leaks
        if (timelineController != null)
        {
            timelineController.TimelineScrolled -= OnTimelineScrolled;
        }
    }
    
    private void OnTimelineScrolled(float scrollDeltaMeters)
    {
        if (objectToMove == null) return;
        
        try
        {
            SyncedObject syncedObject = objectToMove.GetComponent<SyncedObject>();
            if (syncedObject != null)
                syncedObject.RequestOwnership();
            else
                Debug.LogWarning($"[TimelineDrivenTranslation] SyncedObject not found on {objectToMove.name}!");
            
            // Scroll right (positive) = move forward
            // Scroll left (negative) = move backward
            Vector3 direction = useLocalForward ? objectToMove.forward : Vector3.forward;
            Vector3 movement = direction * scrollDeltaMeters * movementScale;
            
            // Apply axis constraints
            if (constrainX) movement.x = 0;
            if (constrainY) movement.y = 0;
            if (constrainZ) movement.z = 0;
            
            // Apply the movement
            objectToMove.position += movement;
        }
        catch (System.NullReferenceException ex)
        {
            Debug.LogWarning($"[TimelineDrivenTranslation] NullReferenceException when scrolling timeline. Target object may be disabled. Error: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TimelineDrivenTranslation] Unexpected error during timeline scroll: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

