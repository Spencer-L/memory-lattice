using UnityEngine;
using Oculus.Interaction.Input;
using Oculus.Interaction;

public class HandPinchInteractionManager : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;
    
    [Header("Hand Anchor References - IMPORTANT!")]
    [Tooltip("Drag LeftHandAnchor from OVRCameraRig/TrackingSpace here")]
    [SerializeField] private Transform leftHandAnchor;
    [Tooltip("Drag RightHandAnchor from OVRCameraRig/TrackingSpace here")]
    [SerializeField] private Transform rightHandAnchor;
    
    [Header("Interaction Values")]
    public float scrollValue = 0f;
    public float zoomValue = 1f;
    
    [Header("Scroll Settings")]
    [SerializeField] private float scrollSensitivity = 2f;
    [SerializeField] private float scrollMin = -10f;
    [SerializeField] private float scrollMax = 10f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSensitivity = 0.5f;
    [SerializeField] private float zoomMin = 0.5f;
    [SerializeField] private float zoomMax = 3f;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private float debugLogInterval = 0.5f;
    
    // Internal tracking
    private bool wasLeftPinching = false;
    private bool wasRightPinching = false;
    private bool wasTwoHandPinching = false;
    
    private Vector3 lastLeftPinchPosition;
    private Vector3 lastRightPinchPosition;
    private float lastTwoHandDistance;
    
    private OVRHand activePinchHand = null;
    
    // Debug tracking
    private float lastDebugLogTime = 0f;
    
    void Start()
    {
        DebugLog("=== Hand Pinch Interaction Manager Started ===");
        
        // Check hand references
        if (leftHand == null)
            Debug.LogError("LEFT HAND (OVRHand) REFERENCE IS NULL! Please assign in Inspector.");
        else
            DebugLog($"Left Hand Found: {leftHand.gameObject.name}");
        
        if (rightHand == null)
            Debug.LogError("RIGHT HAND (OVRHand) REFERENCE IS NULL! Please assign in Inspector.");
        else
            DebugLog($"Right Hand Found: {rightHand.gameObject.name}");
        
        // Check anchor references - CRITICAL FOR POSITION TRACKING
        if (leftHandAnchor == null)
        {
            Debug.LogError("LEFT HAND ANCHOR IS NULL! Positions will be incorrect. Please assign LeftHandAnchor from TrackingSpace.");
            // Try to auto-find it
            TryAutoFindAnchors();
        }
        else
        {
            DebugLog($"Left Hand Anchor Found: {leftHandAnchor.gameObject.name}");
        }
        
        if (rightHandAnchor == null)
        {
            Debug.LogError("RIGHT HAND ANCHOR IS NULL! Positions will be incorrect. Please assign RightHandAnchor from TrackingSpace.");
            // Try to auto-find it
            TryAutoFindAnchors();
        }
        else
        {
            DebugLog($"Right Hand Anchor Found: {rightHandAnchor.gameObject.name}");
        }
        
        if (leftHand != null && rightHand != null && leftHandAnchor != null && rightHandAnchor != null)
        {
            DebugLog("✓ All references assigned correctly!");
        }
    }
    
    // Auto-find anchors if not assigned
    private void TryAutoFindAnchors()
    {
        DebugLog("Attempting to auto-find hand anchors...");
        
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
        if (cameraRig != null)
        {
            if (leftHandAnchor == null)
            {
                leftHandAnchor = cameraRig.leftHandAnchor;
                if (leftHandAnchor != null)
                    DebugLog($"Auto-found Left Hand Anchor: {leftHandAnchor.name}");
            }
            
            if (rightHandAnchor == null)
            {
                rightHandAnchor = cameraRig.rightHandAnchor;
                if (rightHandAnchor != null)
                    DebugLog($"Auto-found Right Hand Anchor: {rightHandAnchor.name}");
            }
        }
        else
        {
            Debug.LogError("Could not find OVRCameraRig in scene!");
        }
    }
    
    void Update()
    {
        if (leftHand == null || rightHand == null || leftHandAnchor == null || rightHandAnchor == null)
        {
            if (Time.time - lastDebugLogTime > 2f)
            {
                Debug.LogError("Missing required references! Check Inspector.");
                lastDebugLogTime = Time.time;
            }
            return;
        }
        
        // Periodic tracking debug
        bool shouldLogThisFrame = logEveryFrame || (Time.time - lastDebugLogTime > debugLogInterval);
        
        if (shouldLogThisFrame)
        {
            LogHandTrackingStatus();
            lastDebugLogTime = Time.time;
        }
        
        bool leftPinching = IsHandPinching(leftHand);
        bool rightPinching = IsHandPinching(rightHand);
        
        // Log pinch state changes
        if (leftPinching != wasLeftPinching)
        {
            DebugLog($"LEFT HAND PINCH: {(leftPinching ? "STARTED" : "RELEASED")} at position {leftHandAnchor.position}");
        }
        
        if (rightPinching != wasRightPinching)
        {
            DebugLog($"RIGHT HAND PINCH: {(rightPinching ? "STARTED" : "RELEASED")} at position {rightHandAnchor.position}");
        }
        
        // Two-handed pinch zoom takes priority
        if (leftPinching && rightPinching)
        {
            if (!wasTwoHandPinching)
            {
                DebugLog(">>> TWO-HAND PINCH ZOOM STARTED <<<");
            }
            HandleTwoHandPinch();
            activePinchHand = null;
        }
        // Single hand pinch scroll
        else if (leftPinching || rightPinching)
        {
            OVRHand pinchingHand = leftPinching ? leftHand : rightHand;
            string handName = leftPinching ? "LEFT" : "RIGHT";
            
            if (activePinchHand != pinchingHand)
            {
                DebugLog($">>> SINGLE-HAND PINCH SCROLL STARTED ({handName} HAND) <<<");
            }
            
            HandleSingleHandPinch(pinchingHand);
        }
        else
        {
            if (wasTwoHandPinching)
            {
                DebugLog(">>> TWO-HAND PINCH ZOOM ENDED <<<");
            }
            wasTwoHandPinching = false;
            activePinchHand = null;
        }
        
        wasLeftPinching = leftPinching;
        wasRightPinching = rightPinching;
    }
    
    private void LogHandTrackingStatus()
    {
        DebugLog("--- Hand Tracking Status ---");
        
        // Left hand
        bool leftTracked = leftHand.IsTracked;
        Vector3 leftAnchorPos = leftHandAnchor.position;
        Vector3 leftHandPos = leftHand.transform.position; // This will be 0,0,0 - showing for comparison
        float leftPinchStrength = leftTracked ? leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;
        
        DebugLog($"Left Hand - Tracked: {leftTracked}");
        DebugLog($"  Anchor Position (CORRECT): {leftAnchorPos}");
        DebugLog($"  OVRHand Position (WRONG): {leftHandPos}");
        DebugLog($"  Pinch Strength: {leftPinchStrength:F3}");
        
        // Right hand
        bool rightTracked = rightHand.IsTracked;
        Vector3 rightAnchorPos = rightHandAnchor.position;
        Vector3 rightHandPos = rightHand.transform.position;
        float rightPinchStrength = rightTracked ? rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;
        
        DebugLog($"Right Hand - Tracked: {rightTracked}");
        DebugLog($"  Anchor Position (CORRECT): {rightAnchorPos}");
        DebugLog($"  OVRHand Position (WRONG): {rightHandPos}");
        DebugLog($"  Pinch Strength: {rightPinchStrength:F3}");
        
        // Current values
        DebugLog($"Current Values - Scroll: {scrollValue:F3} | Zoom: {zoomValue:F3}");
        DebugLog("---------------------------");
    }
    
    private bool IsHandPinching(OVRHand hand)
    {
        if (!hand.IsTracked) return false;
        
        float pinchStrength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        return pinchStrength > 0.7f;
    }
    
    private Vector3 GetPinchPosition(OVRHand hand)
    {
        // Determine which anchor to use
        Transform handAnchor = (hand == leftHand) ? leftHandAnchor : rightHandAnchor;
        
        // Try to get precise pinch position from skeleton
        if (hand.IsTracked)
        {
            OVRSkeleton skeleton = hand.GetComponent<OVRSkeleton>();
            if (skeleton != null && skeleton.Bones != null && skeleton.Bones.Count > 0)
            {
                Transform thumbTip = null;
                Transform indexTip = null;
                
                foreach (var bone in skeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip)
                        thumbTip = bone.Transform;
                    if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                        indexTip = bone.Transform;
                }
                
                if (thumbTip != null && indexTip != null)
                {
                    Vector3 pinchPos = (thumbTip.position + indexTip.position) / 2f;
                    return pinchPos;
                }
            }
        }
        
        // Fallback to hand anchor position (THIS IS THE KEY FIX)
        return handAnchor.position;
    }
    
    private void HandleSingleHandPinch(OVRHand hand)
    {
        Vector3 currentPinchPos = GetPinchPosition(hand);
        string handName = (hand == leftHand) ? "LEFT" : "RIGHT";
        
        if (activePinchHand != hand)
        {
            activePinchHand = hand;
            lastLeftPinchPosition = currentPinchPos;
            lastRightPinchPosition = currentPinchPos;
            
            DebugLog($"{handName} hand pinch position initialized at: {currentPinchPos}");
        }
        else
        {
            Vector3 lastPos = (hand == leftHand) ? lastLeftPinchPosition : lastRightPinchPosition;
            Vector3 delta = currentPinchPos - lastPos;
            
            // Project delta onto horizontal plane relative to camera
            Transform cameraTransform = Camera.main.transform;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            float horizontalDelta = Vector3.Dot(delta, right);
            
            float oldScrollValue = scrollValue;
            
            scrollValue += horizontalDelta * scrollSensitivity;
            scrollValue = Mathf.Clamp(scrollValue, scrollMin, scrollMax);
            
            if (Mathf.Abs(scrollValue - oldScrollValue) > 0.01f)
            {
                DebugLog($"SCROLL UPDATE ({handName}) - Pos: {currentPinchPos} | Delta: {horizontalDelta:F4} | {oldScrollValue:F3} → {scrollValue:F3}");
            }
        }
        
        if (hand == leftHand)
            lastLeftPinchPosition = currentPinchPos;
        else
            lastRightPinchPosition = currentPinchPos;
    }
    
    private void HandleTwoHandPinch()
    {
        Vector3 leftPinchPos = GetPinchPosition(leftHand);
        Vector3 rightPinchPos = GetPinchPosition(rightHand);
        
        float currentDistance = Vector3.Distance(leftPinchPos, rightPinchPos);
        
        if (!wasTwoHandPinching)
        {
            lastTwoHandDistance = currentDistance;
            wasTwoHandPinching = true;
            
            DebugLog($"Two-hand pinch initialized - Distance: {currentDistance:F3}m");
            DebugLog($"Left Pos: {leftPinchPos} | Right Pos: {rightPinchPos}");
        }
        else
        {
            float distanceDelta = currentDistance - lastTwoHandDistance;
            float zoomDelta = distanceDelta * zoomSensitivity;
            
            float oldZoomValue = zoomValue;
            
            zoomValue += zoomDelta;
            zoomValue = Mathf.Clamp(zoomValue, zoomMin, zoomMax);
            
            if (Mathf.Abs(zoomValue - oldZoomValue) > 0.01f)
            {
                string zoomDirection = distanceDelta > 0 ? "OUT (apart)" : "IN (together)";
                DebugLog($"ZOOM {zoomDirection} - Dist: {currentDistance:F3}m | Delta: {distanceDelta:F4} | {oldZoomValue:F3} → {zoomValue:F3}");
            }
            
            lastTwoHandDistance = currentDistance;
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HandPinch] {message}");
        }
    }
    
    void OnGUI()
    {
        GUILayout.Label($"Scroll Value: {scrollValue:F2}");
        GUILayout.Label($"Zoom Value: {zoomValue:F2}");
        
        if (leftHand != null && leftHandAnchor != null)
        {
            GUILayout.Label($"Left: Pinch={IsHandPinching(leftHand)} | Pos={leftHandAnchor.position}");
        }
        
        if (rightHand != null && rightHandAnchor != null)
        {
            GUILayout.Label($"Right: Pinch={IsHandPinching(rightHand)} | Pos={rightHandAnchor.position}");
        }
    }
}