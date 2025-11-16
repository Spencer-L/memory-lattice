using UnityEngine;
using System.Collections.Generic;
using System;
using Normal.Realtime;

public class SyncedObject : MonoBehaviour
{
    private RealtimeView _realtimeView;
    private RealtimeTransform _realtimeTransform;
    
    void Start()
    {
        _realtimeView = GetComponent<RealtimeView>();
        _realtimeTransform = GetComponent<RealtimeTransform>();
    }
    
    // Call this when player grabs/touches the cube
    public void RequestOwnership()
    {
        // Request ownership so this client can move it
        if (_realtimeView != null)
            _realtimeView.RequestOwnership();
        else
            Debug.LogWarning($"[SyncedObject] RealtimeView is null on {gameObject.name}. Object may be disabled.");
            
        if (_realtimeTransform != null)
            _realtimeTransform.RequestOwnership();
        else
            Debug.LogWarning($"[SyncedObject] RealtimeTransform is null on {gameObject.name}. Object may be disabled.");
    }
}