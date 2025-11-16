using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages multiple event markers on the timeline. Handles spawning, tracking,
/// and cleanup of timeline events. Supports multiple marker types with different graphics.
/// </summary>
public class TimelineEventManager : MonoBehaviour
{
    [System.Serializable]
    public class EventMarkerType
    {
        public string typeName;
        public GameObject prefab;
    }
    
    [System.Serializable]
    public class EventConfiguration
    {
        [Tooltip("Display name for this event")]
        public string eventName = "New Event";
        
        [Tooltip("Marker type to use (must match a type name above)")]
        public string markerType = "Kitchen";
        
        [Tooltip("Time offset from timeline center (in hours, negative = past)")]
        public float timeOffsetHours = -1f;
        
        [Header("Position")]
        [Tooltip("Radial distance from timeline arc (0-2m)")]
        [Range(0f, 2f)]
        public float distance = 0.5f;
        
        [Tooltip("Angle around the tangent (0-360 degrees)")]
        [Range(0f, 360f)]
        public float angle = 0f;
        
        [Tooltip("Use random position instead")]
        public bool randomizePosition = false;
        
        [Header("Memory Association")]
        [Tooltip("Index of the splat to open when this marker is selected (-1 = none)")]
        public int splatIndex = -1;
    }
    
    [Header("References")]
    [SerializeField, Tooltip("Reference to the TimelineController")]
    private TimelineController timelineController;
    
    [SerializeField, Tooltip("Reference to the MemoryManager")]
    private MemoryManager memoryManager;
    
    [Header("Event Marker Types")]
    [SerializeField, Tooltip("Different types of event markers with their prefabs")]
    private EventMarkerType[] markerTypes = new EventMarkerType[3];
    
    [Header("Event Configuration")]
    [SerializeField, Tooltip("Configure events to create at runtime")]
    private EventConfiguration[] eventConfigurations = new EventConfiguration[0];
    
    [Header("Example Events")]
    [SerializeField, Tooltip("Create example events on Start for testing (uses eventConfigurations if not empty)")]
    private bool createExampleEvents = true;
    
    private List<TimelineEventMarker> activeMarkers = new List<TimelineEventMarker>();
    private Dictionary<string, GameObject> markerTypeLookup;
    
    void Start()
    {
        Debug.Log($"[MARKER→SPLAT] ===== INITIALIZATION =====");
        Debug.Log($"[MARKER→SPLAT] TimelineController: {(timelineController != null ? "ASSIGNED" : "NULL")}");
        Debug.Log($"[MARKER→SPLAT] MemoryManager: {(memoryManager != null ? "ASSIGNED ✓" : "NULL ⚠")}");
        
        if (timelineController == null)
        {
            Debug.LogError("[MARKER→SPLAT] TimelineController not assigned!");
            return;
        }
        
        if (memoryManager == null)
        {
            Debug.LogWarning("[MARKER→SPLAT] ⚠ MemoryManager not assigned - markers will not be able to open splats!");
        }
        
        // Build lookup dictionary for marker types
        BuildMarkerTypeLookup();
        
        // Delay example event creation to ensure timeline is fully initialized
        if (createExampleEvents)
        {
            StartCoroutine(CreateExampleEventsDelayed());
        }
    }
    
    System.Collections.IEnumerator CreateExampleEventsDelayed()
    {
        // Wait for timeline to initialize
        yield return new WaitForEndOfFrame();
        CreateExampleEvents();
    }
    
    void BuildMarkerTypeLookup()
    {
        markerTypeLookup = new Dictionary<string, GameObject>();
        
        foreach (var markerType in markerTypes)
        {
            if (markerType != null && !string.IsNullOrEmpty(markerType.typeName))
            {
                if (markerType.prefab == null)
                {
                    Debug.LogWarning($"[TimelineEventManager] Marker type '{markerType.typeName}' has no prefab assigned!");
                }
                else
                {
                    markerTypeLookup[markerType.typeName] = markerType.prefab;
                    Debug.Log($"[TimelineEventManager] Registered marker type: {markerType.typeName}");
                }
            }
        }
        
        if (markerTypeLookup.Count == 0)
        {
            Debug.LogWarning("[TimelineEventManager] No marker types configured!");
        }
    }
    
    /// <summary>
    /// Add a new event marker at the specified time with a specific marker type
    /// </summary>
    /// <param name="eventTime">The DateTime when this event occurs</param>
    /// <param name="markerTypeName">The type name of the marker (matches typeName in markerTypes array)</param>
    /// <param name="label">Optional label for the event</param>
    /// <param name="distance">Radial distance from timeline arc (0-2m). Use -1 for random.</param>
    /// <param name="angle">Angle around the tangent (0-360 degrees). Use -1 for random.</param>
    /// <param name="splatIndex">Index of the splat to open when selected. Use -1 for none.</param>
    /// <returns>The created TimelineEventMarker instance</returns>
    public TimelineEventMarker AddEvent(DateTime eventTime, string markerTypeName, string label = "", float distance = -1f, float angle = -1f, int splatIndex = -1)
    {
        if (!markerTypeLookup.ContainsKey(markerTypeName))
        {
            Debug.LogError($"[TimelineEventManager] Cannot add event - marker type '{markerTypeName}' not found! Available types: {string.Join(", ", markerTypeLookup.Keys)}");
            return null;
        }
        
        GameObject prefab = markerTypeLookup[markerTypeName];
        if (prefab == null)
        {
            Debug.LogError($"[TimelineEventManager] Marker type '{markerTypeName}' has null prefab!");
            return null;
        }
        
        // Instantiate the marker prefab
        GameObject markerObj = Instantiate(prefab, transform);
        markerObj.name = $"Event_{markerTypeName}_{eventTime:yyyy-MM-dd_HH-mm-ss}";
        
        // Add the marker component and initialize it
        TimelineEventMarker marker = markerObj.GetComponent<TimelineEventMarker>();
        if (marker == null)
        {
            marker = markerObj.AddComponent<TimelineEventMarker>();
        }
        
        marker.Initialize(timelineController, eventTime, label, markerTypeName);
        
        // Set position if specified (otherwise uses randomization from marker)
        if (distance >= 0f && angle >= 0f)
        {
            marker.SetPosition(distance, angle);
        }
        else if (distance >= 0f)
        {
            marker.SetDistance(distance);
        }
        else if (angle >= 0f)
        {
            marker.SetAngle(angle);
        }
        
        activeMarkers.Add(marker);
        
        // Subscribe to marker selection event to open associated splat
        if (splatIndex >= 0)
        {
            if (memoryManager != null)
            {
                int capturedIndex = splatIndex; // Capture for closure
                marker.onMarkerSelected.AddListener((selectedMarker) => OnMarkerSelected(selectedMarker, capturedIndex));
                Debug.Log($"[MARKER→SPLAT] ✓ Added {markerTypeName} event at {eventTime:yyyy-MM-dd HH:mm:ss} - {label}");
                Debug.Log($"[MARKER→SPLAT]   Position: distance={distance:F2}, angle={angle:F1}°, SPLAT INDEX={splatIndex}");
                Debug.Log($"[MARKER→SPLAT]   ✓ Subscribed to onMarkerSelected event for splat index {splatIndex}");
            }
            else
            {
                Debug.LogWarning($"[MARKER→SPLAT] ⚠ Cannot subscribe to marker selection - MemoryManager is NULL! Marker: {label}, Splat: {splatIndex}");
            }
        }
        else
        {
            Debug.Log($"[MARKER→SPLAT] Added {markerTypeName} event at {eventTime:yyyy-MM-dd HH:mm:ss} - {label} (NO SPLAT ASSOCIATED, index={splatIndex})");
        }
        
        return marker;
    }
    
    /// <summary>
    /// Add a new event marker using the first available marker type (fallback method)
    /// </summary>
    /// <param name="eventTime">The DateTime when this event occurs</param>
    /// <param name="label">Optional label for the event</param>
    /// <param name="distance">Radial distance from timeline arc (0-2m). Use -1 for random.</param>
    /// <param name="angle">Angle around the tangent (0-360 degrees). Use -1 for random.</param>
    /// <param name="splatIndex">Index of the splat to open when selected. Use -1 for none.</param>
    /// <returns>The created TimelineEventMarker instance</returns>
    public TimelineEventMarker AddEvent(DateTime eventTime, string label = "", float distance = -1f, float angle = -1f, int splatIndex = -1)
    {
        if (markerTypeLookup.Count == 0)
        {
            Debug.LogError("[TimelineEventManager] Cannot add event - no marker types configured!");
            return null;
        }
        
        // Use the first available marker type
        string firstType = null;
        foreach (var key in markerTypeLookup.Keys)
        {
            firstType = key;
            break;
        }
        
        return AddEvent(eventTime, firstType, label, distance, angle, splatIndex);
    }
    
    /// <summary>
    /// Remove a specific event marker
    /// </summary>
    public void RemoveEvent(TimelineEventMarker marker)
    {
        if (marker != null && activeMarkers.Contains(marker))
        {
            activeMarkers.Remove(marker);
            Destroy(marker.gameObject);
        }
    }
    
    /// <summary>
    /// Called when a marker is selected. Opens the associated splat in the MemoryManager.
    /// </summary>
    private void OnMarkerSelected(TimelineEventMarker marker, int splatIndex)
    {
        Debug.Log($"[MARKER→SPLAT] ===== OnMarkerSelected CALLED =====");
        Debug.Log($"[MARKER→SPLAT]   Marker: {marker?.EventLabel ?? "NULL"}");
        Debug.Log($"[MARKER→SPLAT]   Splat Index: {splatIndex}");
        Debug.Log($"[MARKER→SPLAT]   MemoryManager: {(memoryManager != null ? "ASSIGNED" : "NULL")}");
        
        if (memoryManager == null)
        {
            Debug.LogError("[MARKER→SPLAT] ⚠⚠⚠ CANNOT OPEN SPLAT - MemoryManager is NULL!");
            return;
        }
        
        Debug.Log($"[MARKER→SPLAT] → Calling memoryManager.OpenSplat({splatIndex})...");
        memoryManager.OpenSplat(splatIndex);
        Debug.Log($"[MARKER→SPLAT] ← memoryManager.OpenSplat({splatIndex}) completed");
    }
    
    /// <summary>
    /// Remove all event markers
    /// </summary>
    public void ClearAllEvents()
    {
        foreach (var marker in activeMarkers)
        {
            if (marker != null)
            {
                Destroy(marker.gameObject);
            }
        }
        activeMarkers.Clear();
        Debug.Log("[TimelineEventManager] Cleared all events");
    }
    
    /// <summary>
    /// Get all currently active markers
    /// </summary>
    public List<TimelineEventMarker> GetActiveMarkers()
    {
        return new List<TimelineEventMarker>(activeMarkers);
    }
    
    /// <summary>
    /// Get all available marker type names
    /// </summary>
    public List<string> GetAvailableMarkerTypes()
    {
        return new List<string>(markerTypeLookup.Keys);
    }
    
    /// <summary>
    /// Check if a marker type exists
    /// </summary>
    public bool HasMarkerType(string typeName)
    {
        return markerTypeLookup.ContainsKey(typeName);
    }
    
    /// <summary>
    /// Creates events from configuration or uses example events for testing
    /// </summary>
    void CreateExampleEvents()
    {
        if (markerTypeLookup.Count == 0)
        {
            Debug.LogWarning("[TimelineEventManager] Cannot create events - no marker types configured!");
            return;
        }
        
        // Get the center time from the timeline
        DateTime centerTime = timelineController.GetCenterTimestamp();
        
        // Validate the center time is reasonable
        if (centerTime == DateTime.MinValue || centerTime == DateTime.MaxValue || centerTime.Year < 1900 || centerTime.Year > 2100)
        {
            Debug.LogWarning($"[TimelineEventManager] Cannot create events - timeline not properly initialized (center time: {centerTime})");
            return;
        }
        
        try
        {
            // Use configured events if available
            if (eventConfigurations != null && eventConfigurations.Length > 0)
            {
                foreach (var config in eventConfigurations)
                {
                    if (string.IsNullOrEmpty(config.markerType))
                    {
                        Debug.LogWarning($"[TimelineEventManager] Skipping event '{config.eventName}' - no marker type specified");
                        continue;
                    }
                    
                    // Calculate event time from offset
                    DateTime eventTime = centerTime.AddHours(config.timeOffsetHours);
                    
                    // Determine distance and angle
                    float distance = config.randomizePosition ? -1f : config.distance;
                    float angle = config.randomizePosition ? -1f : config.angle;
                    
                    AddEvent(eventTime, config.markerType, config.eventName, distance, angle, config.splatIndex);
                }
                
                Debug.Log($"[TimelineEventManager] Created {activeMarkers.Count} configured events around {centerTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                // Fallback to hardcoded example events
                List<string> typeNames = new List<string>(markerTypeLookup.Keys);
                
                // Create markers with different positioning and associated splats:
                // - Kitchen: close, at 0 degrees (top), splat 0
                // - MIT: medium distance, at 90 degrees (right side), splat 1
                // - Stanford: far, at 180 degrees (bottom), splat 2
                
                if (typeNames.Count >= 1)
                    AddEvent(centerTime.AddHours(-6), typeNames[0], "Kitchen", distance: 0.3f, angle: 0f, splatIndex: 0);

                if (typeNames.Count >= 2)
                    AddEvent(centerTime.AddDays(-5), typeNames[1], "MIT", distance: 0.5f, angle: 90f, splatIndex: 1);
                
                if (typeNames.Count >= 3)
                    AddEvent(centerTime.AddMinutes(-15), typeNames[2], "Stanford", distance: 0.7f, angle: 180f, splatIndex: 2);

                Debug.Log($"[TimelineEventManager] Created {activeMarkers.Count} example events around {centerTime:yyyy-MM-dd HH:mm:ss}");
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.LogError($"[TimelineEventManager] Failed to create events - date calculation error: {ex.Message}. Center time was: {centerTime}");
        }
    }
    
    void OnDestroy()
    {
        ClearAllEvents();
    }
}

