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
    
    [Header("References")]
    [SerializeField, Tooltip("Reference to the TimelineController")]
    private TimelineController timelineController;
    
    [Header("Event Marker Types")]
    [SerializeField, Tooltip("Different types of event markers with their prefabs")]
    private EventMarkerType[] markerTypes = new EventMarkerType[3];
    
    [Header("Example Events")]
    [SerializeField, Tooltip("Create example events on Start for testing")]
    private bool createExampleEvents = true;
    
    private List<TimelineEventMarker> activeMarkers = new List<TimelineEventMarker>();
    private Dictionary<string, GameObject> markerTypeLookup;
    
    void Start()
    {
        if (timelineController == null)
        {
            Debug.LogError("[TimelineEventManager] TimelineController not assigned!");
            return;
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
    /// <returns>The created TimelineEventMarker instance</returns>
    public TimelineEventMarker AddEvent(DateTime eventTime, string markerTypeName, string label = "")
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
        activeMarkers.Add(marker);
        
        Debug.Log($"[TimelineEventManager] Added {markerTypeName} event at {eventTime:yyyy-MM-dd HH:mm:ss} - {label}");
        
        return marker;
    }
    
    /// <summary>
    /// Add a new event marker using the first available marker type (fallback method)
    /// </summary>
    /// <param name="eventTime">The DateTime when this event occurs</param>
    /// <param name="label">Optional label for the event</param>
    /// <returns>The created TimelineEventMarker instance</returns>
    public TimelineEventMarker AddEvent(DateTime eventTime, string label = "")
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
        
        return AddEvent(eventTime, firstType, label);
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
    /// Creates some example events for testing - demonstrates using different marker types
    /// </summary>
    void CreateExampleEvents()
    {
        if (markerTypeLookup.Count == 0)
        {
            Debug.LogWarning("[TimelineEventManager] Cannot create example events - no marker types configured!");
            return;
        }
        
        // Get the center time from the timeline
        DateTime centerTime = timelineController.GetCenterTimestamp();
        
        // Validate the center time is reasonable
        if (centerTime == DateTime.MinValue || centerTime == DateTime.MaxValue || centerTime.Year < 1900 || centerTime.Year > 2100)
        {
            Debug.LogWarning($"[TimelineEventManager] Cannot create example events - timeline not properly initialized (center time: {centerTime})");
            return;
        }
        
        // Get available marker types
        List<string> typeNames = new List<string>(markerTypeLookup.Keys);
        
        try
        {
            if (typeNames.Count >= 1)
                AddEvent(centerTime.AddHours(-6), typeNames[0], "Kitchen");

            if (typeNames.Count >= 2)
                AddEvent(centerTime.AddDays(-5), typeNames[1], "MIT");
            
            if (typeNames.Count >= 3)
                AddEvent(centerTime.AddMinutes(-15), typeNames[2], "Stanford");

            Debug.Log($"[TimelineEventManager] Created {activeMarkers.Count} example events around {centerTime:yyyy-MM-dd HH:mm:ss}");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.LogError($"[TimelineEventManager] Failed to create example events - date calculation error: {ex.Message}. Center time was: {centerTime}");
        }
    }
    
    void OnDestroy()
    {
        ClearAllEvents();
    }
}

