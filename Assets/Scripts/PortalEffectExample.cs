using UnityEngine;

/// <summary>
/// Example script showing how to animate the portal effect programmatically.
/// Attach this to your Gaussian Splat object alongside GaussianSplatPortalController.
/// </summary>
public class PortalEffectExample : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Animate the portal radii")]
    public bool animateRadii = false;
    
    [Tooltip("Speed of radius animation")]
    public float animationSpeed = 1.0f;
    
    [Tooltip("Move the portal center")]
    public bool animateCenter = false;
    
    [Tooltip("Radius of center movement")]
    public float centerMoveRadius = 0.2f;

    private GaussianSplatPortalController portalController;
    private float innerRadiusBase;
    private float outerRadiusBase;

    void Start()
    {
        portalController = GetComponent<GaussianSplatPortalController>();
        
        if (portalController != null)
        {
            innerRadiusBase = portalController.innerRadius;
            outerRadiusBase = portalController.outerRadius;
        }
        else
        {
            Debug.LogWarning("GaussianSplatPortalController not found on this GameObject!");
        }
    }

    void Update()
    {
        if (portalController == null) return;

        // Animate radii with pulsing effect
        if (animateRadii)
        {
            float pulse = Mathf.Sin(Time.time * animationSpeed) * 0.5f + 0.5f;
            portalController.innerRadius = Mathf.Lerp(innerRadiusBase * 0.5f, innerRadiusBase * 1.5f, pulse);
            portalController.outerRadius = Mathf.Lerp(outerRadiusBase * 0.8f, outerRadiusBase * 1.2f, pulse);
        }

        // Animate portal center in a circular motion
        if (animateCenter)
        {
            float angle = Time.time * animationSpeed;
            portalController.portalCenter = new Vector3(
                Mathf.Cos(angle) * centerMoveRadius,
                Mathf.Sin(angle) * centerMoveRadius,
                0
            );
        }
    }

    // Example: Fade in the portal effect over time
    public void FadeInPortal(float duration)
    {
        StartCoroutine(FadeInCoroutine(duration));
    }

    private System.Collections.IEnumerator FadeInCoroutine(float duration)
    {
        if (portalController == null) yield break;

        float startInner = 0f;
        float startOuter = 0f;
        float targetInner = innerRadiusBase;
        float targetOuter = outerRadiusBase;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            portalController.innerRadius = Mathf.Lerp(startInner, targetInner, t);
            portalController.outerRadius = Mathf.Lerp(startOuter, targetOuter, t);
            
            yield return null;
        }

        portalController.innerRadius = targetInner;
        portalController.outerRadius = targetOuter;
    }

    // Example: Quickly open/close the portal
    public void TogglePortal(bool open, float speed = 2.0f)
    {
        StopAllCoroutines();
        StartCoroutine(TogglePortalCoroutine(open, speed));
    }

    private System.Collections.IEnumerator TogglePortalCoroutine(bool open, float speed)
    {
        if (portalController == null) yield break;

        float targetInner = open ? innerRadiusBase : 0f;
        float targetOuter = open ? outerRadiusBase : 0.01f;

        while (Mathf.Abs(portalController.outerRadius - targetOuter) > 0.01f)
        {
            portalController.innerRadius = Mathf.Lerp(
                portalController.innerRadius, 
                targetInner, 
                Time.deltaTime * speed
            );
            
            portalController.outerRadius = Mathf.Lerp(
                portalController.outerRadius, 
                targetOuter, 
                Time.deltaTime * speed
            );
            
            yield return null;
        }

        portalController.innerRadius = targetInner;
        portalController.outerRadius = targetOuter;
    }
}

