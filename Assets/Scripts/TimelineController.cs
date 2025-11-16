using UnityEngine;
using System.Collections.Generic;
using System;

public class TimelineController : MonoBehaviour
{
    public delegate void TimelineScrolledHandler(float scrollDeltaMeters);
    public event TimelineScrolledHandler TimelineScrolled;

    [Header("Timeline Range")]
    [SerializeField, Tooltip("How far back in time the timeline extends (in months)")]
    private float timelineRangeMonths = 14f;
    
    [Header("Arc Configuration")]
    [SerializeField, Tooltip("Radius of the curved timeline arc")]
    private float arcRadius = 1.5f;
    
    [SerializeField, Tooltip("Arc coverage in degrees (120 = 120 degree arc)")]
    private float arcDegrees = 120f;
    
    [SerializeField, Tooltip("Offset from head position")]
    private Vector3 arcOffset = new Vector3(0f, -0.3f, 0.5f);
    
    [Header("Zoom Configuration")]
    [SerializeField, Tooltip("Minimum visible time range in seconds (max zoom in)")]
    private float minVisibleSeconds = 60f; // 1 minute
    
    [SerializeField, Tooltip("Zoom sensitivity multiplier (higher = more sensitive zoom)")]
    private float zoomSensitivity = 1.0f;
    
    [Header("Scroll Configuration")]
    [SerializeField, Tooltip("Scroll sensitivity multiplier (higher = faster scrolling)")]
    private float scrollSensitivity = 1.0f;
    
    [SerializeField, Tooltip("Reference to the hand pinch manager")]
    private HandPinchInteractionManager pinchManager;
    
    [Header("Tick Configuration")]
    [SerializeField, Tooltip("Prefab for timeline ticks (should use GPU instancing material)")]
    private GameObject tickPrefab;
    
    [SerializeField, Tooltip("Target number of visible ticks at any time")]
    private int targetTickCount = 200;
    
    [SerializeField, Tooltip("Material for instanced rendering")]
    private Material instancedMaterial;
    
    [Header("Label Configuration")]
    [SerializeField, Tooltip("TextMeshPro prefab for tick labels")]
    private GameObject labelPrefab;
    
    [SerializeField, Tooltip("Vertical offset above ticks")]
    private float labelOffsetY = 0.1f;
    
    [SerializeField, Tooltip("Label pool size")]
    private int labelPoolSize = 100;
    
    [SerializeField, Tooltip("Minimum angular separation between labels (in degrees)")]
    private float minLabelAngleDegrees = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Tick level definitions
    private TickLevel[] tickLevels;
    
    // Timeline state
    private DateTime timelineStart; // NOW
    private DateTime timelineEnd;   // 14 months ago
    private double totalTimelineSeconds;
    
    // Current view state
    private double currentScrollTime; // Time offset in seconds
    private double currentVisibleSeconds; // How many seconds are visible
    private double zoomStartVisibleSeconds; // Snapshot when zoom gesture starts
    
    // Instancing data
    private Dictionary<int, List<Matrix4x4>> tickMatricesByLevel;
    private Dictionary<int, MaterialPropertyBlock> propertyBlocksByLevel;
    
    // Instancing base tick scale
    private Vector3 basePrefabScale = Vector3.one;
    
    // Label pooling
    private Queue<GameObject> labelPool;
    private List<GameObject> activeLabels;
    
    // For future event markers
    public delegate void OnTimelineUpdated(DateTime visibleStart, DateTime visibleEnd, double zoomLevel);
    public event OnTimelineUpdated TimelineUpdated;
    
    [System.Serializable]
    private class TickLevel
    {
        public string name;
        public int level; // 0-8
        public double intervalSeconds;
        public float scale; // Visual scale multiplier
        public Color color; // For future differentiation
        
        public TickLevel(string name, int level, double intervalSeconds, float scale)
        {
            this.name = name;
            this.level = level;
            this.intervalSeconds = intervalSeconds;
            this.scale = scale;
            this.color = Color.white;
        }
    }
    
    void Start()
    {
        InitializeTimeline();
        InitializeTickLevels();
    
        if (pinchManager == null)
        {
            Debug.LogError("HandPinchInteractionManager not assigned!");
        }
    
        // Capture the prefab's base scale
        if (tickPrefab != null)
        {
            basePrefabScale = tickPrefab.transform.localScale;
            DebugLog($"Tick prefab base scale: {basePrefabScale}");
        }
        else
        {
            Debug.LogWarning("Tick prefab not assigned! Using default scale.");
        }
    
        tickMatricesByLevel = new Dictionary<int, List<Matrix4x4>>();
        propertyBlocksByLevel = new Dictionary<int, MaterialPropertyBlock>();
        
        InitializeLabelPool();
    
        DebugLog("Timeline initialized");
    }
    
    void InitializeTimeline()
    {
        // Set timeline end (newest/rightmost) to 9:00 AM Sun Nov 16 2025
        timelineStart = new DateTime(2025, 11, 16, 9, 0, 0);
        timelineEnd = timelineStart.AddMonths(-(int)timelineRangeMonths);
        totalTimelineSeconds = (timelineStart - timelineEnd).TotalSeconds;
        
        // Initialize view to show 5 minutes, starting at newest time (scroll=0 is now newest)
        currentVisibleSeconds = 300; // 5 minutes = 300 seconds
        currentScrollTime = 0; // 0 = showing the newest/rightmost end
        
        DebugLog($"Timeline range: {timelineEnd:yyyy-MM-dd HH:mm} to {timelineStart:yyyy-MM-dd HH:mm}");
        DebugLog($"Total duration: {totalTimelineSeconds / 86400:F1} days");
        DebugLog($"Initial view: 5 minutes at newest end");
    }
    
    void InitializeTickLevels()
    {
        tickLevels = new TickLevel[]
        {
            new TickLevel("Second", 0, 1, 0.5f),
            new TickLevel("15 Seconds", 1, 15, 0.6f),
            new TickLevel("Minute", 2, 60, 0.8f),
            new TickLevel("15 Minutes", 3, 900, 1.0f),
            new TickLevel("Hour", 4, 3600, 1.2f),
            new TickLevel("Day", 5, 86400, 1.5f),
            new TickLevel("Week", 6, 604800, 1.8f),
            new TickLevel("Month", 7, 2592000, 2.2f), // ~30 days
            new TickLevel("Year", 8, 31536000, 3.0f)
        };
    }
    
    void InitializeLabelPool()
    {
        labelPool = new Queue<GameObject>();
        activeLabels = new List<GameObject>();
        
        if (labelPrefab != null)
        {
            for (int i = 0; i < labelPoolSize; i++)
            {
                GameObject label = Instantiate(labelPrefab, transform);
                label.SetActive(false);
                labelPool.Enqueue(label);
            }
            
            // Check if we can find TextMeshPro component in prefab
            TMPro.TextMeshPro testText = labelPrefab.GetComponentInChildren<TMPro.TextMeshPro>();
            if (testText != null)
            {
                DebugLog($"Label pool initialized with {labelPoolSize} labels. TextMeshPro found in prefab.");
            }
            else
            {
                Debug.LogWarning("[Timeline] Label prefab assigned but no TextMeshPro component found in prefab or children!");
            }
        }
        else
        {
            Debug.LogWarning("[Timeline] Label prefab not assigned!");
        }
    }
    
    void Update()
    {
        if (pinchManager == null) return;
        
        UpdateTimelineFromInput();
        UpdateTickPositions();
        RenderTicks();
    }
    
    void UpdateTimelineFromInput()
    {
        bool zoomChanged = ApplyZoomFromInput();
        bool scrollChanged = ApplyScrollFromInput();
        
        // Reset zoom start when gesture ends
        if (!pinchManager.IsTwoHandPinching)
        {
            zoomStartVisibleSeconds = 0;
        }
        
        // Inverted: scroll=0 is newest (timelineStart), higher values go back in time
        DateTime visibleEnd = timelineStart.AddSeconds(-currentScrollTime);
        DateTime visibleStart = visibleEnd.AddSeconds(-currentVisibleSeconds);
        
        if (enableDebugLogs && (zoomChanged || scrollChanged) && Time.frameCount % 30 == 0)
        {
            double secondsPerMeter = GetSecondsPerMeter();
            DebugLog($"View: {visibleStart:MM/dd HH:mm} to {visibleEnd:MM/dd HH:mm} | Range: {currentVisibleSeconds / 3600:F1}hrs | Seconds/m: {secondsPerMeter:F3}");
        }
        
        TimelineUpdated?.Invoke(visibleStart, visibleEnd, currentVisibleSeconds);
    }
    
    void UpdateTickPositions()
    {
        // Return all labels to pool
        ReturnAllLabelsToPool();
        
        // Clear previous frame's data
        foreach (var kvp in tickMatricesByLevel)
        {
            kvp.Value.Clear();
        }
        
        // Determine which tick levels to show based on current zoom
        List<int> visibleTickLevels = DetermineVisibleTickLevels();
        
        // Apply angular culling to determine which levels should have labels
        // (This doesn't affect tick rendering, only labels)
        List<int> visibleLabelLevels = ApplyAngleBasedLabelCulling(new List<int>(visibleTickLevels));
        
        // Find the smallest (finest granularity) LABEL level - we won't label this one
        int smallestLabelLevel = visibleLabelLevels.Count > 0 ? visibleLabelLevels[0] : -1;
        
        // Generate ticks for each visible level
        foreach (int level in visibleTickLevels)
        {
            // Only create labels if:
            // 1. This level is in the label levels list (passed angular culling)
            // 2. This level is not the smallest label level
            bool shouldCreateLabels = visibleLabelLevels.Contains(level) && (level != smallestLabelLevel);
            GenerateTicksForLevel(level, shouldCreateLabels, visibleLabelLevels);
        }
    }
    
    List<int> DetermineVisibleTickLevels()
    {
        List<int> visibleLevels = new List<int>();
        
        // Calculate how many ticks each level would produce
        for (int i = 0; i < tickLevels.Length; i++)
        {
            double tickInterval = tickLevels[i].intervalSeconds;
            int estimatedTickCount = (int)(currentVisibleSeconds / tickInterval);
            
            // Include level if it produces a reasonable number of ticks
            // We want 2-3 levels visible for context
            if (estimatedTickCount >= 3 && estimatedTickCount <= targetTickCount)
            {
                visibleLevels.Add(i);
            }
        }
        
        // Ensure we show at least 2 levels for context
        if (visibleLevels.Count == 0)
        {
            // Find the level that's closest to our target tick count
            int bestLevel = 0;
            int bestDifference = int.MaxValue;
            
            for (int i = 0; i < tickLevels.Length; i++)
            {
                double tickInterval = tickLevels[i].intervalSeconds;
                int estimatedTickCount = (int)(currentVisibleSeconds / tickInterval);
                int difference = Mathf.Abs(estimatedTickCount - 20); // Target ~20 ticks
                
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestLevel = i;
                }
            }
            
            visibleLevels.Add(bestLevel);
            
            // Add the level below and above for context if they exist
            if (bestLevel > 0) visibleLevels.Add(bestLevel - 1);
            if (bestLevel < tickLevels.Length - 1) visibleLevels.Add(bestLevel + 1);
        }
        
        // Limit to max 3 levels for clarity
        if (visibleLevels.Count > 3)
        {
            visibleLevels = visibleLevels.GetRange(0, 3);
        }
        
        visibleLevels.Sort();
        return visibleLevels;
    }
    
    List<int> ApplyAngleBasedLabelCulling(List<int> candidateLevels)
    {
        if (candidateLevels.Count == 0) return candidateLevels;
        
        // Calculate the angular spacing between ticks for the finest (smallest) level
        int finestLevel = candidateLevels[0];
        double finestInterval = tickLevels[finestLevel].intervalSeconds;
        
        // Calculate how much angle each tick interval represents
        // arcDegrees is the total arc span, currentVisibleSeconds is the time span
        double degreesPerSecond = arcDegrees / currentVisibleSeconds;
        double angularSpacing = finestInterval * degreesPerSecond;
        
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            DebugLog($"Angle-based culling: Finest level {finestLevel} ({tickLevels[finestLevel].name}) has {angularSpacing:F2}° spacing (min: {minLabelAngleDegrees}°)");
        }
        
        // If the angular spacing is too small, remove the finest level
        if (angularSpacing < minLabelAngleDegrees)
        {
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                DebugLog($"Culling level {finestLevel} ({tickLevels[finestLevel].name}) - angular spacing {angularSpacing:F2}° < minimum {minLabelAngleDegrees}°");
            }
            
            candidateLevels.RemoveAt(0); // Remove finest level
            
            // Recursively check the next finest level
            return ApplyAngleBasedLabelCulling(candidateLevels);
        }
        
        return candidateLevels;
    }
    
    bool ApplyZoomFromInput()
    {
        if (!pinchManager.IsTwoHandPinching)
        {
            // Zoom gesture ended, no changes
            return false;
        }
        
        float initialDistance = pinchManager.InitialTwoHandDistance;
        float currentDistance = pinchManager.CurrentTwoHandDistance;
        
        if (initialDistance <= Mathf.Epsilon)
            return false;
        
        // On first frame of zoom gesture, capture the starting visible time
        if (zoomStartVisibleSeconds <= 0)
        {
            zoomStartVisibleSeconds = currentVisibleSeconds;
        }
        
        // Ratio-based zoom: if hands move apart 2x, show half the time (zoom in)
        // if hands move together to 0.5x, show 2x the time (zoom out)
        float rawRatio = initialDistance / currentDistance;
        
        // Apply zoom sensitivity: lerp between 1.0 (no change) and the raw ratio
        // Higher sensitivity = more responsive zoom
        float ratio = Mathf.Lerp(1.0f, rawRatio, zoomSensitivity);
        
        double newVisibleSeconds = zoomStartVisibleSeconds * ratio;
        double clamped = Clamp(newVisibleSeconds, minVisibleSeconds, totalTimelineSeconds);
        
        // Center-based zoom: adjust scroll to keep the center point fixed
        double oldVisibleSeconds = currentVisibleSeconds;
        double centerTime = currentScrollTime + (oldVisibleSeconds * 0.5);
        
        bool changed = Math.Abs(clamped - currentVisibleSeconds) > 1e-6;
        if (changed)
        {
            currentVisibleSeconds = clamped;
            
            // Reposition scroll so the center stays in the same place
            currentScrollTime = centerTime - (currentVisibleSeconds * 0.5);
            ClampScrollTime();
        }
        
        return changed;
    }
    
    bool ApplyScrollFromInput()
    {
        float scrollDeltaMeters = pinchManager.ScrollDeltaThisFrame;
        if (Mathf.Approximately(scrollDeltaMeters, 0f))
            return false;
        
        double arcLength = GetArcLengthMeters();
        if (arcLength <= Mathf.Epsilon)
            return false;
        
        // Apply scroll sensitivity multiplier
        scrollDeltaMeters *= scrollSensitivity;
        
        // Direct mapping: scrolling right moves timeline right (forward in time, toward scroll=0/newest)
        double secondsPerMeter = currentVisibleSeconds / arcLength;
        double scrollSecondsDelta = scrollDeltaMeters * secondsPerMeter;
        
        double scrollableRange = Math.Max(0, totalTimelineSeconds - currentVisibleSeconds);
        double clamped = Clamp(currentScrollTime + scrollSecondsDelta, 0, scrollableRange);
        
        bool changed = Math.Abs(clamped - currentScrollTime) > 1e-6;
        currentScrollTime = clamped;

        if (changed)
        {
            TimelineScrolled?.Invoke(scrollDeltaMeters); // Fire the event
        }

        return changed;
    }
    
    void ClampScrollTime()
    {
        double scrollableRange = Math.Max(0, totalTimelineSeconds - currentVisibleSeconds);
        currentScrollTime = Clamp(currentScrollTime, 0, scrollableRange);
    }
    
    double GetSecondsPerMeter()
    {
        double arcLength = GetArcLengthMeters();
        if (arcLength <= Mathf.Epsilon)
            return 0d;
        
        return currentVisibleSeconds / arcLength;
    }
    
    float GetArcLengthMeters()
    {
        float radians = Mathf.Abs(arcDegrees) * Mathf.Deg2Rad;
        return Mathf.Max(0f, arcRadius * radians);
    }
    
    double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
 void GenerateTicksForLevel(int level, bool shouldCreateLabels, List<int> visibleLabelLevels)
{
    if (!tickMatricesByLevel.ContainsKey(level))
    {
        tickMatricesByLevel[level] = new List<Matrix4x4>();
    }
    
    TickLevel tickLevel = tickLevels[level];
    double tickInterval = tickLevel.intervalSeconds;
    
    // Calculate visible time range (inverted: scroll=0 is newest)
    DateTime visibleEnd = timelineStart.AddSeconds(-currentScrollTime);
    DateTime visibleStart = visibleEnd.AddSeconds(-currentVisibleSeconds);
    
    // Find the first tick time (aligned to interval)
    DateTime firstTick = AlignToInterval(visibleStart, tickInterval);
    
    // Generate ticks
    DateTime currentTick = firstTick;
    int tickCount = 0;
    int maxTicks = targetTickCount; // Safety limit
    
    while (currentTick <= visibleEnd && tickCount < maxTicks)
    {
        // Calculate normalized position on timeline (0 = oldest, 1 = newest/now)
        double timeSinceEnd = (currentTick - timelineEnd).TotalSeconds;
        double normalizedTime = timeSinceEnd / totalTimelineSeconds;
        
        // Check if this tick is within the visible range
        if (currentTick >= visibleStart && currentTick <= visibleEnd)
        {
            // Calculate position within visible range for arc placement
            double visibleProgress = (currentTick - visibleStart).TotalSeconds / currentVisibleSeconds;
            
            // Convert to arc position
            Vector3 tickPosition = CalculateArcPosition((float)visibleProgress);
            Quaternion tickRotation = CalculateArcRotation((float)visibleProgress);
            
            // FIXED: Apply both the prefab's base scale AND the level scale multiplier
            Vector3 tickScale = Vector3.Scale(basePrefabScale, Vector3.one * tickLevel.scale);
            
            // Create transformation matrix
            Matrix4x4 matrix = Matrix4x4.TRS(tickPosition, tickRotation, tickScale);
            tickMatricesByLevel[level].Add(matrix);
            
            // Create label if requested and label prefab is assigned
            if (shouldCreateLabels && labelPrefab != null)
            {
                // Check if this tick coincides with a larger granularity level
                // If so, skip this label (the larger level will show its label instead)
                bool shouldSkipLabel = ShouldSkipLabelDueToLargerLevel(currentTick, level, visibleLabelLevels);
                
                if (!shouldSkipLabel)
                {
                    GameObject label = GetLabelFromPool();
                    
                    // Position label above the tick
                    Vector3 labelPosition = tickPosition + (Vector3.up * labelOffsetY);
                    label.transform.position = labelPosition;
                    
                    // Calculate label rotation - should face outward from arc center (opposite of tick)
                    // Ticks face inward with -angle, so labels face outward with +angle (180 degree difference)
                    float angle = Mathf.Lerp(-arcDegrees / 2f, arcDegrees / 2f, (float)visibleProgress);
                    Quaternion labelRotation = Quaternion.Euler(0f, angle + 180f, 0f);
                    label.transform.rotation = transform.rotation * labelRotation;
                    
                    // Set label text - search recursively in children for TextMeshPro
                    TMPro.TextMeshPro textMesh = label.GetComponentInChildren<TMPro.TextMeshPro>();
                    if (textMesh != null)
                    {
                        string labelText = FormatTickLabel(currentTick, level);
                        textMesh.text = labelText;
                        
                        if (enableDebugLogs && tickCount < 3) // Log first 3 labels per level
                        {
                            DebugLog($"Label created for {currentTick:yyyy-MM-dd HH:mm:ss} at level {level} ({tickLevel.name}): '{labelText}' at position {labelPosition}");
                        }
                    }
                    else
                    {
                        if (tickCount == 0) // Log error only once per level
                        {
                            Debug.LogWarning($"[Timeline] TextMeshPro component not found in label prefab or its children!");
                        }
                    }
                }
                else
                {
                    if (enableDebugLogs && tickCount < 3)
                    {
                        DebugLog($"Label SKIPPED for {currentTick:yyyy-MM-dd HH:mm:ss} at level {level} ({tickLevel.name}) - coincides with larger level");
                    }
                }
            }
            
            tickCount++;
        }
        
        // Move to next tick
        currentTick = currentTick.AddSeconds(tickInterval);
    }
    
    if (enableDebugLogs && Time.frameCount % 60 == 0)
    {
        DebugLog($"Level {level} ({tickLevel.name}): {tickCount} ticks");
    }
}
 
    DateTime AlignToInterval(DateTime time, double intervalSeconds)
    {
        // Align to the nearest interval boundary before the given time
        long ticks = time.Ticks;
        long intervalTicks = (long)(intervalSeconds * TimeSpan.TicksPerSecond);
        long alignedTicks = (ticks / intervalTicks) * intervalTicks;
        return new DateTime(alignedTicks);
    }
    
    Vector3 CalculateArcPosition(float progress)
    {
        // Progress: 0 = left edge of arc, 1 = right edge of arc
        // Map to angle: -arcDegrees/2 to +arcDegrees/2
        float angle = Mathf.Lerp(-arcDegrees / 2f, arcDegrees / 2f, progress);
        float angleRad = angle * Mathf.Deg2Rad;
        
        // Calculate position on arc (XZ plane initially)
        float x = Mathf.Sin(angleRad) * arcRadius;
        float z = Mathf.Cos(angleRad) * arcRadius;
        
        // Apply offset and convert to local space
        Vector3 localPosition = new Vector3(x, 0f, z) + arcOffset;
        
        // Convert to world space relative to this transform
        return transform.TransformPoint(localPosition);
    }
    
    Quaternion CalculateArcRotation(float progress)
    {
        // Calculate angle for this position
        float angle = Mathf.Lerp(-arcDegrees / 2f, arcDegrees / 2f, progress);
        
        // Ticks should face toward the center of the arc
        // Rotate to face inward
        Quaternion localRotation = Quaternion.Euler(0f, -angle, 0f);
        
        // Convert to world space rotation
        return transform.rotation * localRotation;
    }
    
    void RenderTicks()
    {
        if (tickPrefab == null || instancedMaterial == null)
        {
            if (Time.frameCount % 120 == 0)
                Debug.LogWarning("Tick prefab or instanced material not assigned!");
            return;
        }
        
        // Render each level using GPU instancing
        foreach (var kvp in tickMatricesByLevel)
        {
            int level = kvp.Key;
            List<Matrix4x4> matrices = kvp.Value;
            
            if (matrices.Count == 0) continue;
            
            // Get mesh from prefab
            MeshFilter meshFilter = tickPrefab.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("Tick prefab must have a MeshFilter with a mesh!");
                continue;
            }
            
            // Setup material property block (for future per-level customization)
            if (!propertyBlocksByLevel.ContainsKey(level))
            {
                propertyBlocksByLevel[level] = new MaterialPropertyBlock();
            }
            
            MaterialPropertyBlock props = propertyBlocksByLevel[level];
            // Future: Set different colors per level
            // props.SetColor("_Color", tickLevels[level].color);
            
            // Render using DrawMeshInstanced (max 1023 per call)
            int batchSize = 1023;
            for (int i = 0; i < matrices.Count; i += batchSize)
            {
                int count = Mathf.Min(batchSize, matrices.Count - i);
                List<Matrix4x4> batch = matrices.GetRange(i, count);
                
                Graphics.DrawMeshInstanced(
                    meshFilter.sharedMesh,
                    0,
                    instancedMaterial,
                    batch,
                    props
                );
            }
        }
    }
    
    // Public API for future event markers
    public Vector3 GetWorldPositionForTime(DateTime time)
    {
        // Calculate where a specific time appears on the timeline (inverted: scroll=0 is newest)
        DateTime visibleEnd = timelineStart.AddSeconds(-currentScrollTime);
        DateTime visibleStart = visibleEnd.AddSeconds(-currentVisibleSeconds);
        
        double visibleProgress = (time - visibleStart).TotalSeconds / currentVisibleSeconds;
        
        if (visibleProgress < 0 || visibleProgress > 1)
            return Vector3.zero; // Not visible
        
        return CalculateArcPosition((float)visibleProgress);
    }
    
    public bool IsTimeVisible(DateTime time)
    {
        DateTime visibleEnd = timelineStart.AddSeconds(-currentScrollTime);
        DateTime visibleStart = visibleEnd.AddSeconds(-currentVisibleSeconds);
        return time >= visibleStart && time <= visibleEnd;
    }
    
    bool ShouldSkipLabelDueToLargerLevel(DateTime time, int currentLevel, List<int> visibleLabelLevels)
    {
        // Check if this time coincides with any VISIBLE larger granularity level
        // Only check levels that are actually visible (more efficient and correct)
        foreach (int visibleLevel in visibleLabelLevels)
        {
            // Only check levels larger (coarser) than current level
            if (visibleLevel <= currentLevel) continue;
            
            double largerInterval = tickLevels[visibleLevel].intervalSeconds;
            
            // Check if this time is perfectly aligned to the larger interval
            // by seeing if the time from epoch is divisible by the interval
            long timeTicks = time.Ticks;
            long intervalTicks = (long)(largerInterval * TimeSpan.TicksPerSecond);
            
            // If perfectly divisible, this time coincides with the larger visible level
            if (timeTicks % intervalTicks == 0)
            {
                return true; // Skip this label - the larger level will show it
            }
        }
        
        return false; // No coincidence found with visible larger levels, show the label
    }
    
    string FormatTickLabel(DateTime time, int level)
    {
        switch (level)
        {
            case 0: // Second
                return $"{time.Second}s";
            case 1: // 15 Seconds
                return $"{time.Second}s";
            case 2: // Minute
                return $"{time.Minute}m";
            case 3: // 15 Minutes
                return $"{time.Minute}m";
            case 4: // Hour
                return time.ToString("HH:mm");
            case 5: // Day
                return time.ToString("HH:mm");
            case 6: // Week
                return time.ToString("MMM dd");
            case 7: // Month
                return time.ToString("MMM yyyy");
            case 8: // Year
                return time.ToString("yyyy");
            default:
                return "";
        }
    }
    
    GameObject GetLabelFromPool()
    {
        if (labelPool.Count > 0)
        {
            GameObject label = labelPool.Dequeue();
            label.SetActive(true);
            activeLabels.Add(label);
            return label;
        }
        // Pool exhausted, create new label
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[Timeline] Label pool exhausted! Creating new label. Consider increasing pool size.");
        }
        GameObject newLabel = Instantiate(labelPrefab, transform);
        activeLabels.Add(newLabel);
        return newLabel;
    }
    
    void ReturnAllLabelsToPool()
    {
        foreach (GameObject label in activeLabels)
        {
            label.SetActive(false);
            labelPool.Enqueue(label);
        }
        activeLabels.Clear();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Timeline] {message}");
        }
    }
    
    // Gizmos for editor visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        Gizmos.color = Color.cyan;
        
        // Draw arc path
        for (float t = 0; t <= 1f; t += 0.05f)
        {
            Vector3 pos = CalculateArcPosition(t);
            Gizmos.DrawWireSphere(pos, 0.02f);
        }
    }
}
