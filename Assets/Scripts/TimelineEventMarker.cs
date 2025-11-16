using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Represents a single event marker on the timeline. Automatically updates its position
/// and visibility based on the current timeline view.
/// </summary>
public class TimelineEventMarker : MonoBehaviour
{
    [System.Serializable]
    public class MarkerSelectedEvent : UnityEvent<TimelineEventMarker> { }
    
    [Header("Position Offset Settings (Set via TimelineEventManager)")]
    [SerializeField, Tooltip("Radial distance from timeline arc")]
    private float distanceFromTimeline = 0.3f;
    
    [SerializeField, Tooltip("Angle around the tangent (0-360 degrees)")]
    private float angleInDegrees = 0f;
    
    // Randomization defaults (used if position not specified by manager)
    private bool randomizePosition = true;
    private float minDistance = 0.2f;
    private float maxDistance = 0.8f;
    
    [Header("Connection Line Settings")]
    [SerializeField, Tooltip("Width of the line connecting marker to timeline")]
    private float lineWidth = 0.002f;
    
    [SerializeField, Tooltip("Color of the connection line")]
    private Color lineColor = Color.white;
    
    [Header("Selection Settings")]
    [SerializeField, Tooltip("Event fired when marker is selected after lingering")]
    public MarkerSelectedEvent onMarkerSelected = new MarkerSelectedEvent();
    
    [SerializeField, Tooltip("Duration in seconds to hold proximity before selection")]
    private float selectionDuration = 1.0f;
    
    public DateTime EventTime { get; set; }
    public string EventLabel { get; set; }
    public string MarkerType { get; private set; }
    
    // Selection state properties
    public bool IsInProximity { get; private set; }
    public float SelectionProgress => Mathf.Clamp01(selectionTimer / selectionDuration);
    public bool IsSelected { get; private set; }
    
    private float selectionTimer = 0f;
    
    private TimelineController timeline;
    private LineRenderer connectionLine; // Line connecting marker to timeline position
    private double currentZoomLevel = 300.0; // Cache current visible seconds for tangent calculation
    
    /// <summary>
    /// Initialize the event marker with a specific time and label
    /// </summary>
    public void Initialize(TimelineController controller, DateTime eventTime, string label, string markerType = "")
    {
        timeline = controller;
        EventTime = eventTime;
        EventLabel = label;
        MarkerType = markerType;
        
        // Randomize position if enabled
        if (randomizePosition)
        {
            distanceFromTimeline = Mathf.Lerp(minDistance, maxDistance, UnityEngine.Random.value);
            angleInDegrees = UnityEngine.Random.Range(0f, 360f);
        }
        
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
    
    /// <summary>
    /// Update the proximity state of this marker. Call this each frame to track selection progress.
    /// </summary>
    /// <param name="isNear">True if the timeline center is within proximity threshold</param>
    public void UpdateProximity(bool isNear)
    {
        IsInProximity = isNear;
        
        if (isNear)
        {
            // Increment selection timer
            selectionTimer += Time.deltaTime;
            
            // Check if selection is complete
            if (selectionTimer >= selectionDuration && !IsSelected)
            {
                IsSelected = true;
                onMarkerSelected.Invoke(this);
                Debug.Log($"[TimelineEventMarker] Marker selected: {EventLabel} at {EventTime:yyyy-MM-dd HH:mm:ss}");
            }
        }
        else
        {
            // Reset timer and selection state when out of proximity
            selectionTimer = 0f;
            IsSelected = false;
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
        currentZoomLevel = zoomLevel;
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
                // Calculate tangent at this point (like BallsManager does)
                Vector3 tangent = CalculateTangentAtTime(EventTime);
                
                // Calculate cylindrical offset using distance and angle
                // This ensures the offset is perpendicular to the arc, creating proper radial positioning
                Vector3 offset = CalculateCylindricalOffset(tangent, distanceFromTimeline, angleInDegrees);
                
                Vector3 markerPosition = timelinePosition + offset;
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
    /// Calculate the tangent direction at a specific time on the timeline arc.
    /// Uses numerical derivative by sampling nearby points.
    /// Delta is proportional to zoom level to work at all zoom ranges.
    /// </summary>
    private Vector3 CalculateTangentAtTime(DateTime time)
    {
        // Use a delta that's proportional to the current zoom level
        // Sample at ~1% of visible range on each side
        double deltaSeconds = currentZoomLevel * 0.01;
        // Clamp to reasonable bounds (0.1 to 1000 seconds)
        deltaSeconds = Math.Max(0.1, Math.Min(1000.0, deltaSeconds));
        
        DateTime timeBefore = time.AddSeconds(-deltaSeconds);
        DateTime timeAfter = time.AddSeconds(deltaSeconds);
        
        Vector3 posBefore = timeline.GetWorldPositionForTime(timeBefore);
        Vector3 posAfter = timeline.GetWorldPositionForTime(timeAfter);
        
        // Check if positions are valid (not zero)
        if (posBefore == Vector3.zero || posAfter == Vector3.zero)
        {
            // Fallback: return a default tangent
            return Vector3.right;
        }
        
        Vector3 tangent = (posAfter - posBefore).normalized;
        return tangent;
    }
    
    /// <summary>
    /// Calculate offset perpendicular to the tangent using cylindrical coordinates.
    /// This matches how BallsManager positions balls relative to the arc.
    /// </summary>
    private Vector3 CalculateCylindricalOffset(Vector3 tangent, float distance, float angleInDegrees)
    {
        // Create a perpendicular vector to the tangent for the radial direction
        // Use cross product with up vector to get a perpendicular direction
        Vector3 radialBase = Vector3.Cross(tangent, Vector3.up).normalized;
        
        // If tangent is parallel to up, use forward instead
        if (radialBase.magnitude < 0.01f)
        {
            radialBase = Vector3.Cross(tangent, Vector3.forward).normalized;
        }
        
        // Rotate the radial base around the tangent by the specified angle
        Quaternion rotation = Quaternion.AngleAxis(angleInDegrees, tangent);
        Vector3 radialDirection = rotation * radialBase;
        
        // Apply distance
        Vector3 offset = radialDirection * distance;
        
        return offset;
    }
    
    /// <summary>
    /// Force an immediate position update (useful after changing EventTime)
    /// </summary>
    public void ForceUpdate()
    {
        UpdatePosition();
    }
    
    /// <summary>
    /// Set the marker's distance from the timeline arc
    /// </summary>
    public void SetDistance(float distance)
    {
        distanceFromTimeline = Mathf.Clamp(distance, 0f, 2f);
        UpdatePosition();
    }
    
    /// <summary>
    /// Set the marker's angle around the tangent
    /// </summary>
    public void SetAngle(float angle)
    {
        angleInDegrees = angle % 360f;
        if (angleInDegrees < 0f) angleInDegrees += 360f;
        UpdatePosition();
    }
    
    /// <summary>
    /// Set both distance and angle at once
    /// </summary>
    public void SetPosition(float distance, float angle)
    {
        distanceFromTimeline = Mathf.Clamp(distance, 0f, 2f);
        angleInDegrees = angle % 360f;
        if (angleInDegrees < 0f) angleInDegrees += 360f;
        UpdatePosition();
    }
    
    /// <summary>
    /// Get current distance from timeline
    /// </summary>
    public float GetDistance() => distanceFromTimeline;
    
    /// <summary>
    /// Get current angle in degrees
    /// </summary>
    public float GetAngle() => angleInDegrees;
}

