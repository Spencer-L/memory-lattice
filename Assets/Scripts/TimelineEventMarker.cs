using UnityEngine;
using System;

/// <summary>
/// Represents a single event marker on the timeline. Automatically updates its position
/// and visibility based on the current timeline view.
/// </summary>
public class TimelineEventMarker : MonoBehaviour
{
    public DateTime EventTime { get; set; }
    public string EventLabel { get; set; }
    public string MarkerType { get; private set; }
    
    private TimelineController timeline;
    
    /// <summary>
    /// Initialize the event marker with a specific time and label
    /// </summary>
    public void Initialize(TimelineController controller, DateTime eventTime, string label, string markerType = "")
    {
        timeline = controller;
        EventTime = eventTime;
        EventLabel = label;
        MarkerType = markerType;
        
        // Subscribe to timeline updates for efficient position updates
        if (timeline != null)
        {
            timeline.TimelineUpdated += OnTimelineUpdated;
            UpdatePosition();
        }
    }
    
    void OnDestroy()
    {
        if (timeline != null)
        {
            timeline.TimelineUpdated -= OnTimelineUpdated;
        }
    }
    
    void OnTimelineUpdated(DateTime visibleStart, DateTime visibleEnd, double zoomLevel)
    {
        UpdatePosition();
    }
    
    void UpdatePosition()
    {
        if (timeline == null) return;
        
        // Check if this event's time is currently visible on the timeline
        bool isVisible = timeline.IsTimeVisible(EventTime);
        gameObject.SetActive(isVisible);
        
        if (isVisible)
        {
            // Get the world position for this specific time
            Vector3 position = timeline.GetWorldPositionForTime(EventTime);
            
            if (position != Vector3.zero)
            {
                transform.position = position;
            }
        }
    }
    
    /// <summary>
    /// Force an immediate position update (useful after changing EventTime)
    /// </summary>
    public void ForceUpdate()
    {
        UpdatePosition();
    }
}

