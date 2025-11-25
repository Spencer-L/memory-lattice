using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class BallsManager : MonoBehaviour
{
    [Header("Timeline Reference")]
    [SerializeField, Tooltip("Reference to the TimelineController")]
    private TimelineController timelineController;
    
    [SerializeField, Tooltip("Reference to TimelineEventManager (for line settings)")]
    private TimelineEventManager timelineEventManager;
    
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
    [SerializeField, Tooltip("Base density threshold at reference zoom level (balls per square meter)")]
    private float baseDensityThreshold = 50f;
    
    [SerializeField, Tooltip("Reference zoom level in seconds (density threshold is optimal at this zoom)")]
    private double referenceZoomSeconds = 300.0; // 5 minutes - matches timeline initial zoom
    
    [SerializeField, Tooltip("How aggressively density scales with zoom (0.5 = sqrt scaling, 1.0 = linear)")]
    private float densityScalingPower = 0.7f;
    
    [Header("Randomization")]
    [SerializeField, Tooltip("Random seed for consistent ball placement")]
    private int randomSeed = 42;
    
    [SerializeField, Tooltip("Time distribution bias (1.0 = uniform, >1.0 = more recent, <1.0 = more ancient)")]
    [Range(0.1f, 5.0f)]
    private float timeBias = 2.0f;
    
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
    
    [Header("Line Renderer Settings")]
    [SerializeField, Tooltip("Enable line renderers connecting balls to timeline")]
    private bool enableBallLineRenderers = true;
    
    [SerializeField, Tooltip("Line thickness factor relative to event marker lines (0.5 = half thickness)")]
    [Range(0.1f, 1.0f)]
    private float lineThicknessFactor = 0.5f;
    
    [SerializeField, Tooltip("Fallback line thickness if TimelineEventManager not assigned")]
    private float fallbackLineThickness = 0.001f;
    
    [SerializeField, Tooltip("Line endpoint offset (same as event markers)")]
    private float lineEndpointOffset = 0.02f;
    
    [SerializeField, Tooltip("Color of the ball connection lines")]
    private Color ballLineColor = new Color(1f, 1f, 1f, 0.5f);
    
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
        public LineRenderer lineRenderer;
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
    
    // Line renderer pooling
    private Queue<LineRenderer> lineRendererPool;
    private List<LineRenderer> activeLineRenderers;
    private Material lineMaterial;
    
    // Cached ball prefab base radius (calculated from prefab bounds at unit scale)
    private float ballPrefabBaseRadius = 0.5f;
    
    // Cached line settings to detect changes at runtime
    private float cachedLineEndpointOffset;
    private float cachedLineThickness;
    
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
        
        // Calculate the ball prefab's base radius from its renderer bounds
        CalculateBallPrefabBaseRadius();
        
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
        
        // Initialize cached line settings
        cachedLineEndpointOffset = GetLineEndpointOffset();
        cachedLineThickness = GetBallLineThickness();
        
        DebugLog($"BallsManager initialized with {totalBallCount} balls");
    }
    
    void Update()
    {
        // Check if line settings have changed in the inspector
        if (!enableBallLineRenderers) return;
        
        float currentOffset = GetLineEndpointOffset();
        float currentThickness = GetBallLineThickness();
        
        bool settingsChanged = !Mathf.Approximately(currentOffset, cachedLineEndpointOffset) ||
                               !Mathf.Approximately(currentThickness, cachedLineThickness);
        
        if (settingsChanged)
        {
            cachedLineEndpointOffset = currentOffset;
            cachedLineThickness = currentThickness;
            
            // Update all visible ball line renderers
            UpdateAllLineRenderers();
        }
    }
    
    /// <summary>
    /// Updates all visible ball line renderers with current settings.
    /// Called when line settings change at runtime.
    /// </summary>
    private void UpdateAllLineRenderers()
    {
        float currentThickness = GetBallLineThickness();
        
        foreach (var ball in visibleBalls)
        {
            if (ball.lineRenderer != null && ball.isActive && ball.instance != null)
            {
                // Update line thickness
                ball.lineRenderer.startWidth = currentThickness;
                ball.lineRenderer.endWidth = currentThickness;
                
                // Recalculate line positions with new offset
                Vector3 ballPosition = ball.instance.transform.position;
                Vector3 arcPosition = timelineController.GetWorldPositionForTime(ball.timestamp);
                float finalScale = ball.sizeParameter * dynamicScaleFactor * baseBallScale;
                float actualBallRadius = finalScale * ballPrefabBaseRadius;
                
                PositionLineRenderer(ball.lineRenderer, ballPosition, arcPosition, actualBallRadius);
            }
        }
        
        DebugLog($"Updated {visibleBalls.Count} ball line renderers (offset: {cachedLineEndpointOffset:F3}, thickness: {cachedLineThickness:F4})");
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
        
        // Clean up active line renderers
        if (activeLineRenderers != null)
        {
            foreach (var lr in activeLineRenderers)
            {
                if (lr != null)
                {
                    Destroy(lr.gameObject);
                }
            }
            activeLineRenderers.Clear();
        }
        
        // Clean up pooled line renderers
        if (lineRendererPool != null)
        {
            while (lineRendererPool.Count > 0)
            {
                LineRenderer lr = lineRendererPool.Dequeue();
                if (lr != null)
                {
                    Destroy(lr.gameObject);
                }
            }
        }
        
        // Clean up line material
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
    
    /// <summary>
    /// Calculate the base radius of the ball prefab from its renderer bounds.
    /// This is the radius at unit scale (scale = 1).
    /// </summary>
    private void CalculateBallPrefabBaseRadius()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallsManager] Ball prefab is null, using default radius 0.5");
            ballPrefabBaseRadius = 0.5f;
            return;
        }
        
        // Try to get renderer from the prefab
        Renderer renderer = ballPrefab.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            // For a prefab, we need to temporarily instantiate it to get accurate bounds
            // OR we can use the shared mesh bounds if it's a MeshRenderer
            MeshFilter meshFilter = ballPrefab.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Use mesh bounds (local space, unaffected by transform)
                Vector3 meshExtents = meshFilter.sharedMesh.bounds.extents;
                // Account for the prefab's local scale
                Vector3 prefabScale = ballPrefab.transform.localScale;
                float scaledRadius = Mathf.Max(
                    meshExtents.x * prefabScale.x,
                    Mathf.Max(meshExtents.y * prefabScale.y, meshExtents.z * prefabScale.z)
                );
                ballPrefabBaseRadius = scaledRadius;
                DebugLog($"Ball prefab base radius calculated from mesh: {ballPrefabBaseRadius:F4}");
                return;
            }
        }
        
        // Try SphereCollider as fallback
        SphereCollider sphereCollider = ballPrefab.GetComponentInChildren<SphereCollider>();
        if (sphereCollider != null)
        {
            Vector3 prefabScale = ballPrefab.transform.localScale;
            float maxScale = Mathf.Max(prefabScale.x, Mathf.Max(prefabScale.y, prefabScale.z));
            ballPrefabBaseRadius = sphereCollider.radius * maxScale;
            DebugLog($"Ball prefab base radius calculated from SphereCollider: {ballPrefabBaseRadius:F4}");
            return;
        }
        
        // Last resort: use default
        Debug.LogWarning("[BallsManager] Could not determine ball prefab radius, using default 0.5");
        ballPrefabBaseRadius = 0.5f;
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
        
        // Initialize line renderer pool
        if (enableBallLineRenderers)
        {
            InitializeLineRendererPool();
        }
    }
    
    private void InitializeLineRendererPool()
    {
        lineRendererPool = new Queue<LineRenderer>();
        activeLineRenderers = new List<LineRenderer>();
        
        // Create a shared material for line renderers
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.color = ballLineColor;
        
        // Pre-populate the line renderer pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            LineRenderer lr = CreateLineRendererInstance();
            lr.gameObject.SetActive(false);
            lineRendererPool.Enqueue(lr);
        }
        
        DebugLog($"Line renderer pool initialized with {initialPoolSize} instances");
    }
    
    private LineRenderer CreateLineRendererInstance()
    {
        GameObject lineObj = new GameObject("BallLine");
        lineObj.transform.SetParent(transform);
        
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.material = lineMaterial;
        lr.startColor = ballLineColor;
        lr.endColor = ballLineColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        
        // Set line width based on event marker settings
        float lineWidth = GetBallLineThickness();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        
        return lr;
    }
    
    private float GetBallLineThickness()
    {
        if (timelineEventManager != null)
        {
            return timelineEventManager.GetEventMarkerLineThickness() * lineThicknessFactor;
        }
        return fallbackLineThickness;
    }
    
    private float GetLineEndpointOffset()
    {
        if (timelineEventManager != null)
        {
            return timelineEventManager.GetLineEndpointOffset();
        }
        return lineEndpointOffset;
    }
    
    private void GenerateBallParameters()
    {
        DebugLog("Generating ball parameters...");
        
        // Get timeline range from TimelineController
        // We'll distribute balls across the entire timeline range
        DateTime timelineStart = new DateTime(2025, 11, 16, 9, 0, 0); // From TimelineController
        DateTime timelineEnd = timelineStart.AddMonths(-14); // 14 months back
        double totalSeconds = (timelineStart - timelineEnd).TotalSeconds;
        
        // Track distribution for debug logging
        int recentBalls = 0; // Last 25% of timeline
        
        for (int i = 0; i < totalBallCount; i++)
        {
            BallData ball = new BallData();
            
            // Random timestamp within timeline range with time bias
            // timeBias > 1.0: more balls toward newer times (timelineStart)
            // timeBias = 1.0: uniform distribution
            // timeBias < 1.0: more balls toward older times (timelineEnd)
            double rawRandom = randomGenerator.NextDouble();
            double biasedRandom = Math.Pow(rawRandom, 1.0 / timeBias);
            double randomSeconds = biasedRandom * totalSeconds;
            ball.timestamp = timelineEnd.AddSeconds(randomSeconds);
            
            // Track distribution
            if (randomSeconds > totalSeconds * 0.75)
                recentBalls++;
            
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
        
        float recentPercentage = (recentBalls / (float)totalBallCount) * 100f;
        DebugLog($"Generated {allBalls.Count} ball parameters with time bias {timeBias:F2}");
        DebugLog($"Distribution: {recentPercentage:F1}% of balls in most recent 25% of timeline");
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
    
    private LineRenderer GetLineRendererFromPool()
    {
        if (!enableBallLineRenderers) return null;
        
        LineRenderer lr;
        
        if (lineRendererPool != null && lineRendererPool.Count > 0)
        {
            lr = lineRendererPool.Dequeue();
        }
        else
        {
            // Pool exhausted, create new instance
            lr = CreateLineRendererInstance();
        }
        
        lr.gameObject.SetActive(true);
        activeLineRenderers.Add(lr);
        return lr;
    }
    
    private void ReturnLineRendererToPool(LineRenderer lr)
    {
        if (lr == null) return;
        
        lr.gameObject.SetActive(false);
        activeLineRenderers.Remove(lr);
        lineRendererPool.Enqueue(lr);
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
        
        // Return all active line renderers to pool
        if (enableBallLineRenderers && activeLineRenderers != null)
        {
            for (int i = activeLineRenderers.Count - 1; i >= 0; i--)
            {
                LineRenderer lr = activeLineRenderers[i];
                if (lr != null)
                {
                    lr.gameObject.SetActive(false);
                    lineRendererPool.Enqueue(lr);
                }
            }
            activeLineRenderers.Clear();
        }
        
        // Clear instance references in ball data
        foreach (var ballData in allBalls)
        {
            ballData.instance = null;
            ballData.lineRenderer = null;
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
                
                // Get line renderer from pool if enabled
                if (enableBallLineRenderers)
                {
                    ball.lineRenderer = GetLineRendererFromPool();
                }
                
                // Configure ball instance
                ConfigureBallInstance(ball);
                
                // Position and scale the ball (also positions line renderer)
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
        
        // Calculate zoom-scaled density threshold
        // When zoomed out (large currentZoomLevel), threshold decreases (more aggressive culling)
        // When zoomed in (small currentZoomLevel), threshold increases (show more balls)
        float zoomRatio = (float)(referenceZoomSeconds / currentZoomLevel);
        float scaledDensityThreshold = baseDensityThreshold * Mathf.Pow(zoomRatio, densityScalingPower);
        
        // Calculate current density
        float currentDensity = visibleBalls.Count / unitArea;
        
        if (enableDebugLogs && Time.frameCount % 120 == 0)
        {
            DebugLog($"Zoom: {currentZoomLevel:F0}s | Density: {currentDensity:F2} balls/m² | Threshold: {scaledDensityThreshold:F2} (base: {baseDensityThreshold})");
        }
        
        // If density exceeds scaled threshold, cull smallest balls
        if (currentDensity > scaledDensityThreshold)
        {
            // Sort by size parameter (smallest first)
            var sortedBalls = visibleBalls.OrderBy(b => b.sizeParameter).ToList();
            
            // Calculate how many balls to cull
            int targetBallCount = Mathf.FloorToInt(scaledDensityThreshold * unitArea);
            int ballsToCull = Mathf.Max(0, visibleBalls.Count - targetBallCount);
            
            // Deactivate smallest balls and their line renderers
            for (int i = 0; i < ballsToCull && i < sortedBalls.Count; i++)
            {
                var ball = sortedBalls[i];
                if (ball.instance != null)
                {
                    ball.instance.SetActive(false);
                    ball.isActive = false;
                }
                if (ball.lineRenderer != null)
                {
                    ball.lineRenderer.gameObject.SetActive(false);
                }
            }
            
            if (enableDebugLogs && Time.frameCount % 120 == 0)
            {
                DebugLog($"Density culling: deactivated {ballsToCull} smallest balls (zoom factor: {zoomRatio:F2}x)");
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
        
        // Position the line renderer connecting ball to timeline
        // Ball radius = scale factor * prefab's base radius
        if (ball.lineRenderer != null)
        {
            float actualBallRadius = finalScale * ballPrefabBaseRadius;
            PositionLineRenderer(ball.lineRenderer, finalPosition, arcPosition, actualBallRadius);
        }
    }
    
    private void PositionLineRenderer(LineRenderer lr, Vector3 ballPosition, Vector3 timelinePosition, float ballRadius)
    {
        Vector3 lineDirection = (timelinePosition - ballPosition).normalized;
        float totalLineLength = Vector3.Distance(ballPosition, timelinePosition);
        
        // Get offset settings
        float endpointOffset = GetLineEndpointOffset();
        
        // Calculate offsets:
        // - Start (ball end): offset by ball radius + endpoint offset
        // - End (timeline end): offset by just endpoint offset
        float ballEndOffset = ballRadius + endpointOffset;
        float timelineEndOffset = endpointOffset;
        
        // Only apply offsets if line is long enough
        if (totalLineLength > ballEndOffset + timelineEndOffset + 0.01f)
        {
            Vector3 lineStart = ballPosition + lineDirection * ballEndOffset;
            Vector3 lineEnd = timelinePosition - lineDirection * timelineEndOffset;
            lr.SetPosition(0, lineStart);
            lr.SetPosition(1, lineEnd);
        }
        else
        {
            // Line is too short, just connect the points directly
            lr.SetPosition(0, ballPosition);
            lr.SetPosition(1, timelinePosition);
        }
    }
    
    private Vector3 CalculateTangentAtTime(DateTime time)
    {
        // Calculate tangent by sampling two nearby points on the arc
        // Use a delta that's proportional to the current zoom level
        // Sample at ~1% of visible range on each side
        double deltaSeconds = currentZoomLevel * 0.01;
        // Clamp to reasonable bounds (0.1 to 1000 seconds)
        deltaSeconds = Math.Max(0.1, Math.Min(1000.0, deltaSeconds));
        
        DateTime timeBefore = time.AddSeconds(-deltaSeconds);
        DateTime timeAfter = time.AddSeconds(deltaSeconds);
        
        Vector3 posBefore = timelineController.GetWorldPositionForTime(timeBefore);
        Vector3 posAfter = timelineController.GetWorldPositionForTime(timeAfter);
        Vector3 posCurrent = timelineController.GetWorldPositionForTime(time);
        
        // Handle edge cases where one sample is outside visible range
        // Use one-sided sampling to maintain correct tangent direction at edges
        if (posBefore == Vector3.zero && posAfter != Vector3.zero && posCurrent != Vector3.zero)
        {
            // At left edge of visible range: use current->after for tangent
            Vector3 tangent = (posAfter - posCurrent).normalized;
            if (tangent.sqrMagnitude > 0.001f)
                return tangent;
        }
        else if (posAfter == Vector3.zero && posBefore != Vector3.zero && posCurrent != Vector3.zero)
        {
            // At right edge of visible range: use before->current for tangent
            Vector3 tangent = (posCurrent - posBefore).normalized;
            if (tangent.sqrMagnitude > 0.001f)
                return tangent;
        }
        else if (posBefore != Vector3.zero && posAfter != Vector3.zero)
        {
            // Normal case: both samples valid, use full span
            Vector3 tangent = (posAfter - posBefore).normalized;
            if (tangent.sqrMagnitude > 0.001f)
                return tangent;
        }
        
        // Fallback: return a default tangent (should rarely happen now)
        return Vector3.right;
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
        baseDensityThreshold = threshold;
        ApplyDensityCulling();
        DebugLog($"Base density threshold updated to {threshold}");
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

