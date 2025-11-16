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
    
    [Header("Object Pooling")]
    [SerializeField, Tooltip("Initial pool size for ball instances")]
    private int initialPoolSize = 100;
    
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
    }
    
    // Internal state
    private List<BallData> allBalls;
    private List<BallData> visibleBalls;
    private DateTime currentVisibleStart;
    private DateTime currentVisibleEnd;
    private double currentZoomLevel;
    private System.Random randomGenerator;
    
    // Object pooling
    private Queue<GameObject> ballPool;
    private List<GameObject> activeBalls;
    
    // Material property block for efficient color changes without material instances
    private MaterialPropertyBlock propertyBlock;
    private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyID = Shader.PropertyToID("_BaseColor"); // For URP shaders
    
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
        
        // Initialize material property block for efficient color changes
        propertyBlock = new MaterialPropertyBlock();
        
        // Initialize object pool
        InitializeBallPool();
        
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
        
        // Clean up active ball instances
        if (activeBalls != null)
        {
            foreach (var ball in activeBalls)
            {
                if (ball != null)
                {
                    Destroy(ball);
                }
            }
            activeBalls.Clear();
        }
        
        // Clean up pooled instances
        if (ballPool != null)
        {
            while (ballPool.Count > 0)
            {
                GameObject pooledBall = ballPool.Dequeue();
                if (pooledBall != null)
                {
                    Destroy(pooledBall);
                }
            }
        }
    }
    
    private void InitializeBallPool()
    {
        ballPool = new Queue<GameObject>();
        activeBalls = new List<GameObject>();
        
        if (ballPrefab != null)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject ball = Instantiate(ballPrefab, transform);
                ball.SetActive(false);
                ballPool.Enqueue(ball);
            }
            
            DebugLog($"Ball pool initialized with {initialPoolSize} instances");
        }
        else
        {
            Debug.LogWarning("[BallsManager] Ball prefab not assigned - pool will be created on demand");
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
    
    private GameObject GetBallFromPool()
    {
        GameObject ball;
        
        if (ballPool.Count > 0)
        {
            ball = ballPool.Dequeue();
        }
        else
        {
            // Pool exhausted, create new instance
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[BallsManager] Ball pool exhausted! Creating new instance. Consider increasing pool size.");
            }
            ball = Instantiate(ballPrefab, transform);
        }
        
        ball.SetActive(true);
        activeBalls.Add(ball);
        return ball;
    }
    
    private void ReturnBallToPool(GameObject ball)
    {
        if (ball == null) return;
        
        ball.SetActive(false);
        activeBalls.Remove(ball);
        ballPool.Enqueue(ball);
    }
    
    private void ReturnAllBallsToPool()
    {
        // Return all active balls to pool
        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            GameObject ball = activeBalls[i];
            if (ball != null)
            {
                ball.SetActive(false);
                ballPool.Enqueue(ball);
            }
        }
        activeBalls.Clear();
        
        // Clear instance references in ball data
        foreach (var ballData in allBalls)
        {
            ballData.instance = null;
            ballData.isActive = false;
        }
    }
    
    private void UpdateVisibleBalls()
    {
        // Return all current balls to pool
        ReturnAllBallsToPool();
        
        // Clear previous visible balls list
        visibleBalls.Clear();
        
        // Find balls within visible time range and assign instances from pool
        foreach (var ball in allBalls)
        {
            if (timelineController.IsTimeVisible(ball.timestamp))
            {
                visibleBalls.Add(ball);
                
                // Get instance from pool
                ball.instance = GetBallFromPool();
                
                // Configure ball instance
                ConfigureBallInstance(ball);
                
                // Position and scale the ball
                PositionBall(ball);
                
                ball.isActive = true;
            }
        }
        
        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            DebugLog($"Visible balls: {visibleBalls.Count} / {allBalls.Count} | Pool: {ballPool.Count} | Active: {activeBalls.Count}");
        }
    }
    
    private void ConfigureBallInstance(BallData ball)
    {
        if (ball.instance == null) return;
        
        // Set color using MaterialPropertyBlock to avoid creating material instances
        Renderer renderer = ball.instance.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Use MaterialPropertyBlock for efficient per-object color changes
            // This doesn't create material instances and is perfect for pooled objects
            
            // Create a new MaterialPropertyBlock for this ball to avoid sharing
            MaterialPropertyBlock ballPropertyBlock = new MaterialPropertyBlock();
            
            // Set color for both Standard and URP shaders
            ballPropertyBlock.SetColor(ColorPropertyID, ball.color);      // Standard shader (_Color)
            ballPropertyBlock.SetColor(BaseColorPropertyID, ball.color);  // URP shader (_BaseColor)
            renderer.SetPropertyBlock(ballPropertyBlock);
            
            if (enableDebugLogs && Time.frameCount % 300 == 0 && visibleBalls.IndexOf(ball) < 3)
            {
                DebugLog($"Ball color set: {ball.color} for ball at {ball.timestamp:HH:mm:ss}");
            }
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

