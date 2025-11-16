using UnityEngine;
using Oculus.Interaction.Input;
using UnityEngine.Events;

public class GestureDetection : MonoBehaviour
{
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;
    
    [SerializeField] private float clapDistance = 0.05f; // 5cm threshold
    [SerializeField] private float clapCooldown = 1f;
    [SerializeField] private float poseAngle = 0.7f;
    
    [SerializeField] private UnityEvent onClap;
    
    private float lastClapTime;

    void Update()
    {
        if (!leftHand.IsTracked || !rightHand.IsTracked)
            return;

        // Get palm positions
        Vector3 leftPalm = leftHand.PointerPose.position;
        Vector3 rightPalm = rightHand.PointerPose.position;
        float distance = Vector3.Distance(leftPalm, rightPalm);
        
        // Check if palms are facing each other
        Vector3 leftPalmNormal = leftHand.PointerPose.forward;
        Vector3 rightPalmNormal = rightHand.PointerPose.forward;
        float facingDot = Vector3.Dot(leftPalmNormal, -rightPalmNormal);

        if (distance < clapDistance && Time.time - lastClapTime > clapCooldown)
        {
            OnClap();
            lastClapTime = Time.time;
        }
    }
    
    void OnClap()
    {
        Debug.Log("Clap detected!");
        onClap?.Invoke();
    }
}
