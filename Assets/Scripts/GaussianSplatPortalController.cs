using UnityEngine;

/// <summary>
/// Controls portal fade effect for Gaussian Splat objects.
/// This script sets global shader properties that the Gaussian Splatting shader uses.
/// </summary>
public class GaussianSplatPortalController : MonoBehaviour
{
    [Header("Portal Fade Settings")]
    [Tooltip("Enable or disable the portal fade effect")]
    public bool enablePortalFade = true;
    
    [Tooltip("Center of the portal in object-space coordinates")]
    public Vector3 portalCenter = Vector3.zero;
    
    [Tooltip("Inner radius - no fade within this distance from center")]
    [Range(0f, 5f)]
    public float innerRadius = 0.5f;
    
    [Tooltip("Outer radius - fully transparent at this distance from center")]
    [Range(0f, 10f)]
    public float outerRadius = 1.0f;
    
    [Tooltip("Falloff curve - higher values create sharper fade transitions")]
    [Range(0.1f, 5f)]
    public float falloff = 2.0f;

    void Update()
    {
        UpdatePortalProperties();
    }

    void UpdatePortalProperties()
    {
        // Set global shader properties
        // These will be picked up by the Gaussian Splatting shader
        Shader.SetGlobalFloat("_PortalFadeEnabled", enablePortalFade ? 1.0f : 0.0f);
        Shader.SetGlobalVector("_PortalCenter", portalCenter);
        Shader.SetGlobalFloat("_PortalInnerRadius", innerRadius);
        Shader.SetGlobalFloat("_PortalOuterRadius", outerRadius);
        Shader.SetGlobalFloat("_PortalFalloff", falloff);
    }

    void OnValidate()
    {
        // Ensure outer radius is always greater than inner radius
        if (outerRadius < innerRadius)
        {
            outerRadius = innerRadius + 0.1f;
        }
        
        // Update immediately in editor
        UpdatePortalProperties();
    }

    // Visualize the portal radii in the scene view
    void OnDrawGizmosSelected()
    {
        if (!enablePortalFade) return;

        // Transform portal center to world space
        Vector3 worldCenter = transform.TransformPoint(portalCenter);

        // Draw inner radius (green sphere)
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        DrawWireSphere(worldCenter, innerRadius, 16);

        // Draw outer radius (red sphere)
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        DrawWireSphere(worldCenter, outerRadius, 24);

        // Draw center point
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(worldCenter, 0.02f);
        
        // Draw fade zone
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4.0f;
            float radius = Mathf.Lerp(innerRadius, outerRadius, t);
            DrawWireSphere(worldCenter, radius, 12);
        }
    }
    
    // Helper method to draw wire sphere with custom resolution
    void DrawWireSphere(Vector3 center, float radius, int segments)
    {
        DrawCircle(center, radius, Vector3.up, segments);
        DrawCircle(center, radius, Vector3.right, segments);
        DrawCircle(center, radius, Vector3.forward, segments);
    }
    
    void DrawCircle(Vector3 center, float radius, Vector3 normal, int segments)
    {
        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        Vector3 right = Vector3.Cross(normal, forward).normalized;
        forward = Vector3.Cross(right, normal).normalized;
        
        Vector3 prevPoint = center + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2;
            Vector3 nextPoint = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}
