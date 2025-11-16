using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class BallsManager : MonoBehaviour
{
    [Header("Timeline Reference")]
    [SerializeField, Tooltip("Reference to the TimelineController")]
    private TimelineController timelineController;
    
    [Header("Ball Configuration")]
    [SerializeField, Tooltip("Prefab for ball instances")]
    private GameObject ballPrefab;
    
    [SerializeField, Tooltip("Total number of balls to generate")]
    private int totalBallCount = 1000;
    
    [Header("Positioning")]
    [SerializeField, Tooltip("Minimum radial distance from timeline arc")]
    private float minDistanceFromTimeline = 0.2f;
    
    [SerializeField, Tooltip("Maximum radial distance from timeline arc")]
    private float maxDistanceFromTimeline = 0.8f;
    
    [Header("Scale Configuration")]
    [SerializeField, Tooltip("Base scale for ball size")]
    private float baseBallScale = 0.02f;
    
    [SerializeField, Tooltip("Dynamic multiplier for all ball sizes")]
    private float dynamicScaleFactor = 1.0f;
    
    [Header("Density Culling")]
    [SerializeField, Tooltip("Density threshold for culling (balls per square meter)")]
    private float densityThreshold = 50f;
    
    [Header("Randomization")]
    [SerializeField, Tooltip("Random seed for consistent ball placement")]
    private int randomSeed = 42;
    
    [Header("Color Palette")]
    [SerializeField, Tooltip("Colors for balls - customizable in Inspector")]
    private Color[] colorPalette = new Color[]
    {
        new Color(227f/255f, 30f/255f, 36f/255f),    // Red
        new Color(236f/255f, 0f/255f, 140f/255f),    // Pink
        new Color(94f/255f, 44f/255f, 131f/255f),    // Purple
        new Color(0f/255f, 81f/255f, 186f/255f),     // Blue
        new Color(0f/255f, 166f/255f, 80f/255f),     // Green
        new Color(255f/255f, 213f/255f, 0f/255f),    // Yellow
        new Color(255f/255f, 106f/255f, 0f/255f)     // Orange
    };
    
    [Header("Debug")]
    [SerializeField, Tooltip("Enable debug logging")]
    private bool enableDebugLogs = true;
    
    // Ball data structure
    [System.Serializable]
    private class BallData
    {
        public DateTime timestamp;
        public float distanceFromTimeline;
        public float angleInDegrees;
        public Color color;
        public float sizeParameter; // 0-1
        public GameObject instance;
        public bool isActive;
        public Vector3 cachedTangent; // Cached for performance
    }
    
    // Internal state
    private List<BallData> allBalls;
    private List<BallData> visibleBalls;
    private DateTime currentVisibleStart;
    private DateTime currentVisibleEnd;
    private double currentZoomLevel;
    private System.Random randomGenerator;
    
    void Start()
    {
        if (timelineController == null)
        {
            Debug.LogError("[BallsManager] TimelineController reference not assigned!");
            enabled = false;
            return;
        }
        
        if (ballPrefab == null)
        {
            Debug.LogError("[BallsManager] Ball prefab not assigned!");
            enabled = false;
            return;
        }
        
        if (colorPalette == null || colorPalette.Length == 0)
        {
            Debug.LogWarning("[BallsManager] Color palette is empty! Using default white.");
            colorPalette = new Color[] { Color.white };
        }
        
        DebugLog("Initializing BallsManager...");
        
        // Initialize random generator with fixed seed
        randomGenerator = new System.Random(randomSeed);
        
        // Initialize collections
        allBalls = new List<BallData>();
        visibleBalls = new List<BallData>();
        
        // Generate ball parameters
        GenerateBallParameters();
        
        // Subscribe to timeline events
        timelineController.TimelineUpdated += OnTimelineUpdated;
        
        DebugLog($"BallsManager initialized with {totalBallCount} balls");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (timelineController != null)
        {
            timelineController.TimelineUpdated -= OnTimelineUpdated;
        }
        
        // Clean up ball instances
        foreach (var ball in allBalls)
        {
            if (ball.instance != null)
            {
                Destroy(ball.instance);
            }
        }
    }
    
    private void GenerateBallParameters()
    {
        DebugLog("Generating ball parameters...");
        
        // Get timeline range from TimelineController
        // We'll distribute balls across the entire timeline range
        DateTime timelineStart = new DateTime(2025, 11, 16, 9, 0, 0); // From TimelineController
        DateTime timelineEnd = timelineStart.AddMonths(-14); // 14 months back
        double totalSeconds = (timelineStart - timelineEnd).TotalSeconds;
        
        for (int i = 0; i < totalBallCount; i++)
        {
            BallData ball = new BallData();
            
            // Random timestamp within timeline range
            double randomSeconds = randomGenerator.NextDouble() * totalSeconds;
            ball.timestamp = timelineEnd.AddSeconds(randomSeconds);
            
            // Random distance from timeline
            ball.distanceFromTimeline = Mathf.Lerp(
                minDistanceFromTimeline, 
                maxDistanceFromTimeline, 
                (float)randomGenerator.NextDouble()
            );
            
            // Random angle (0-360 degrees)
            ball.angleInDegrees = (float)(randomGenerator.NextDouble() * 360.0);
            
            // Random color from palette
            ball.color = colorPalette[randomGenerator.Next(colorPalette.Length)];
            
            // Random size parameter (0-1)
            ball.sizeParameter = (float)randomGenerator.NextDouble();
            
            // Instance will be created on demand
            ball.instance = null;
            ball.isActive = false;
            
            allBalls.Add(ball);
        }
        
        DebugLog($"Generated {allBalls.Count} ball parameters");
    }
    
    private void OnTimelineUpdated(DateTime visibleStart, DateTime visibleEnd, double zoomLevel)
    {
        currentVisibleStart = visibleStart;
        currentVisibleEnd = visibleEnd;
        currentZoomLevel = zoomLevel;
        
        UpdateVisibleBalls();
        ApplyDensityCulling();
    }
    
    private void UpdateVisibleBalls()
    {
        // Clear previous visible balls list
        visibleBalls.Clear();
        
        // Find balls within visible time range
        foreach (var ball in allBalls)
        {
            if (timelineController.IsTimeVisible(ball.timestamp))
            {
                visibleBalls.Add(ball);
                
                // Create instance if needed
                if (ball.instance == null)
                {
                    ball.instance = Instantiate(ballPrefab, transform);
                    ball.instance.name = $"Ball_{allBalls.IndexOf(ball)}";
                    
                    // Set color
                    Renderer renderer = ball.instance.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        // Create material instance to avoid modifying shared material
                        renderer.material = new Material(renderer.sharedMaterial);
                        renderer.material.color = ball.color;
                    }
                }
                
                // Position and scale the ball
                PositionBall(ball);
                
                // Activate the ball (will be potentially deactivated by density culling)
                ball.instance.SetActive(true);
                ball.isActive = true;
            }
            else
            {
                // Hide balls outside visible range
                if (ball.instance != null)
                {
                    ball.instance.SetActive(false);
                    ball.isActive = false;
                }
            }
        }
        
        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            DebugLog($"Visible balls: {visibleBalls.Count} / {allBalls.Count}");
        }
    }
    
    private void ApplyDensityCulling()
    {
        if (visibleBalls.Count == 0) return;
        
        // Calculate visible arc length
        float arcLength = CalculateVisibleArcLength();
        
        // Calculate average radial depth
        float radialDepth = maxDistanceFromTimeline - minDistanceFromTimeline;
        
        // Calculate unit area (arc length × radial depth)
        float unitArea = arcLength * radialDepth;
        
        if (unitArea <= 0.0001f) return; // Avoid division by zero
        
        // Calculate current density
        float currentDensity = visibleBalls.Count / unitArea;
        
        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            DebugLog($"Density: {currentDensity:F2} balls/m² (threshold: {densityThreshold})");
        }
        
        // If density exceeds threshold, cull smallest balls
        if (currentDensity > densityThreshold)
        {
            // Sort by size parameter (smallest first)
            var sortedBalls = visibleBalls.OrderBy(b => b.sizeParameter).ToList();
            
            // Calculate how many balls to cull
            int targetBallCount = Mathf.FloorToInt(densityThreshold * unitArea);
            int ballsToCull = Mathf.Max(0, visibleBalls.Count - targetBallCount);
            
            // Deactivate smallest balls
            for (int i = 0; i < ballsToCull && i < sortedBalls.Count; i++)
            {
                var ball = sortedBalls[i];
                if (ball.instance != null)
                {
                    ball.instance.SetActive(false);
                    ball.isActive = false;
                }
            }
            
            if (enableDebugLogs && Time.frameCount % 120 == 0)
            {
                DebugLog($"Density culling: deactivated {ballsToCull} smallest balls");
            }
        }
    }
    
    private float CalculateVisibleArcLength()
    {
        // Get arc configuration from timeline
        // These values match TimelineController's serialized fields
        float arcRadius = 1.5f; // Should match timelineController's arcRadius
        
        // Calculate what fraction of the total timeline is visible
        DateTime timelineStart = new DateTime(2025, 11, 16, 9, 0, 0);
        DateTime timelineEnd = timelineStart.AddMonths(-14);
        double totalSeconds = (timelineStart - timelineEnd).TotalSeconds;
        
        double visibleSeconds = (currentVisibleEnd - currentVisibleStart).TotalSeconds;
        float visibleFraction = (float)(visibleSeconds / totalSeconds);
        
        // Total arc degrees from timeline (default 120 degrees)
        float totalArcDegrees = 120f;
        float visibleArcDegrees = totalArcDegrees * visibleFraction;
        
        // Convert to arc length
        float visibleArcRadians = visibleArcDegrees * Mathf.Deg2Rad;
        float visibleArcLength = arcRadius * visibleArcRadians;
        
        return visibleArcLength;
    }
    
    private void PositionBall(BallData ball)
    {
        if (ball.instance == null) return;
        
        // Get world position for this timestamp on the timeline arc
        Vector3 arcPosition = timelineController.GetWorldPositionForTime(ball.timestamp);
        
        // Calculate tangent at this point
        Vector3 tangent = CalculateTangentAtTime(ball.timestamp);
        
        if (tangent == Vector3.zero)
        {
            // Fallback if tangent calculation fails
            tangent = Vector3.right;
        }
        
        // Calculate cylindrical position offset
        Vector3 offset = CalculateCylindricalOffset(tangent, ball.distanceFromTimeline, ball.angleInDegrees);
        
        // Final world position
        Vector3 finalPosition = arcPosition + offset;
        ball.instance.transform.position = finalPosition;
        
        // Calculate scale
        float finalScale = ball.sizeParameter * dynamicScaleFactor * baseBallScale;
        ball.instance.transform.localScale = Vector3.one * finalScale;
    }
    
    private Vector3 CalculateTangentAtTime(DateTime time)
    {
        // Calculate tangent by sampling two nearby points on the arc
        double deltaSeconds = 0.1; // Small time delta for numerical derivative
        
        DateTime timeBefore = time.AddSeconds(-deltaSeconds);
        DateTime timeAfter = time.AddSeconds(deltaSeconds);
        
        Vector3 posBefore = timelineController.GetWorldPositionForTime(timeBefore);
        Vector3 posAfter = timelineController.GetWorldPositionForTime(timeAfter);
        
        // Check if positions are valid (not zero)
        if (posBefore == Vector3.zero || posAfter == Vector3.zero)
        {
            // Fallback: return a default tangent
            return Vector3.right;
        }
        
        Vector3 tangent = (posAfter - posBefore).normalized;
        
        return tangent;
    }
    
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
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BallsManager] {message}");
        }
    }
    
    // Public API for runtime adjustments
    public void SetDynamicScaleFactor(float factor)
    {
        dynamicScaleFactor = factor;
        
        // Update all visible balls
        foreach (var ball in visibleBalls)
        {
            if (ball.instance != null && ball.isActive)
            {
                float finalScale = ball.sizeParameter * dynamicScaleFactor * baseBallScale;
                ball.instance.transform.localScale = Vector3.one * finalScale;
            }
        }
        
        DebugLog($"Dynamic scale factor updated to {factor}");
    }
    
    public void SetDensityThreshold(float threshold)
    {
        densityThreshold = threshold;
        ApplyDensityCulling();
        DebugLog($"Density threshold updated to {threshold}");
    }
    
    // Gizmos for editor visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || visibleBalls == null) return;
        
        // Draw visible balls in scene view
        Gizmos.color = Color.cyan;
        foreach (var ball in visibleBalls)
        {
            if (ball.isActive && ball.instance != null)
            {
                Gizmos.DrawWireSphere(ball.instance.transform.position, 0.01f);
            }
        }
    }
}

