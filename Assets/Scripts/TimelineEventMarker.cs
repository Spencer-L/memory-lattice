using UnityEngine;
using System;

/// <summary>
/// Represents a single event marker on the timeline. Automatically updates its position
/// and visibility based on the current timeline view.
/// </summary>
public class TimelineEventMarker : MonoBehaviour
{
    [Header("Position Offset Settings")]
    [SerializeField, Tooltip("Minimum offset from timeline position")]
    private Vector3 minOffset = new Vector3(-0.1f, -0.1f, -0.1f);
    
    [SerializeField, Tooltip("Maximum offset from timeline position")]
    private Vector3 maxOffset = new Vector3(0.1f, 0.1f, 0.1f);
    
    [Header("Connection Line Settings")]
    [SerializeField, Tooltip("Width of the line connecting marker to timeline")]
    private float lineWidth = 0.002f;
    
    [SerializeField, Tooltip("Color of the connection line")]
    private Color lineColor = Color.white;
    
    public DateTime EventTime { get; set; }
    public string EventLabel { get; set; }
    public string MarkerType { get; private set; }
    
    private TimelineController timeline;
    private Vector3 randomOffset; // Stored offset that stays consistent for this marker
    private LineRenderer connectionLine; // Line connecting marker to timeline position
    
    /// <summary>
    /// Initialize the event marker with a specific time and label
    /// </summary>
    public void Initialize(TimelineController controller, DateTime eventTime, string label, string markerType = "")
    {
        timeline = controller;
        EventTime = eventTime;
        EventLabel = label;
        MarkerType = markerType;
        
        // Generate a random offset for this marker that will be consistent
        randomOffset = new Vector3(
            UnityEngine.Random.Range(minOffset.x, maxOffset.x),
            UnityEngine.Random.Range(minOffset.y, maxOffset.y),
            UnityEngine.Random.Range(minOffset.z, maxOffset.z)
        );
        
        // Create and configure the connection line
        SetupConnectionLine();
        
        // Subscribe to timeline updates for efficient position updates
        if (timeline != null)
        {
            timeline.TimelineUpdated += OnTimelineUpdated;
            UpdatePosition();
        }
    }
    
    /// <summary>
    /// Setup the LineRenderer component for the connection line
    /// </summary>
    void SetupConnectionLine()
    {
        // Check if LineRenderer already exists
        connectionLine = GetComponent<LineRenderer>();
        if (connectionLine == null)
        {
            connectionLine = gameObject.AddComponent<LineRenderer>();
        }
        
        // Configure the line renderer
        connectionLine.positionCount = 2;
        connectionLine.startWidth = lineWidth;
        connectionLine.endWidth = lineWidth;
        connectionLine.startColor = lineColor;
        connectionLine.endColor = lineColor;
        
        // Use unlit shader so the line appears white regardless of lighting
        connectionLine.material = new Material(Shader.Find("Sprites/Default"));
        connectionLine.material.color = lineColor;
        
        // Disable shadows for the line
        connectionLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        connectionLine.receiveShadows = false;
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
            Vector3 timelinePosition = timeline.GetWorldPositionForTime(EventTime);
            
            if (timelinePosition != Vector3.zero)
            {
                // Apply the random offset to position the marker near but not on the timeline
                Vector3 markerPosition = timelinePosition + randomOffset;
                transform.position = markerPosition;
                
                // Update the connection line between marker and timeline position
                if (connectionLine != null)
                {
                    connectionLine.SetPosition(0, markerPosition); // Start at marker
                    connectionLine.SetPosition(1, timelinePosition); // End at timeline position
                }
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

