using UnityEngine;

/// <summary>
/// Controls the foveated fade effect for Gaussian Splats, allowing the edges to fade into passthrough AR.
/// Attach this to a GameObject with a Renderer that uses the "Gaussian Splatting/Render Splats" shader.
/// </summary>
public class FoveatedSplatController : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField, Range(0f, 1f), Tooltip("Distance from center where fade begins (0 = center, 1 = edge)")]
    private float fadeStart = 0.6f;

    [SerializeField, Range(0f, 1f), Tooltip("Distance from center where splat is fully transparent (0 = center, 1 = edge)")]
    private float fadeEnd = 0.9f;

    [Header("Optional: Override Material")]
    [SerializeField, Tooltip("If not set, will use the material from the Renderer component")]
    private Material targetMaterial;

    private Material runtimeMaterial;
    private Renderer targetRenderer;

    // Shader property IDs for performance
    private static readonly int FadeStartPropertyID = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEndPropertyID = Shader.PropertyToID("_FadeEnd");

    void Start()
    {
        InitializeMaterial();
        UpdateShaderProperties();
    }

    void OnValidate()
    {
        // Ensure fadeEnd is always greater than or equal to fadeStart
        if (fadeEnd < fadeStart)
        {
            fadeEnd = fadeStart;
        }

        // Update in editor
        if (Application.isPlaying)
        {
            UpdateShaderProperties();
        }
    }

    void InitializeMaterial()
    {
        if (targetMaterial != null)
        {
            // Use the explicitly assigned material
            runtimeMaterial = targetMaterial;
        }
        else
        {
            // Try to find a renderer on this GameObject
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                runtimeMaterial = targetRenderer.material;
            }
            else
            {
                Debug.LogError($"FoveatedSplatController on {gameObject.name}: No Renderer found and no material assigned!", this);
            }
        }
    }

    void UpdateShaderProperties()
    {
        if (runtimeMaterial == null)
        {
            InitializeMaterial();
            if (runtimeMaterial == null) return;
        }

        runtimeMaterial.SetFloat(FadeStartPropertyID, fadeStart);
        runtimeMaterial.SetFloat(FadeEndPropertyID, fadeEnd);
    }

    /// <summary>
    /// Set the fade start distance programmatically
    /// </summary>
    public void SetFadeStart(float value)
    {
        fadeStart = Mathf.Clamp01(value);
        if (fadeEnd < fadeStart)
        {
            fadeEnd = fadeStart;
        }
        UpdateShaderProperties();
    }

    /// <summary>
    /// Set the fade end distance programmatically
    /// </summary>
    public void SetFadeEnd(float value)
    {
        fadeEnd = Mathf.Clamp01(value);
        if (fadeEnd < fadeStart)
        {
            fadeStart = fadeEnd;
        }
        UpdateShaderProperties();
    }

    /// <summary>
    /// Set both fade parameters at once
    /// </summary>
    public void SetFadeParameters(float start, float end)
    {
        fadeStart = Mathf.Clamp01(start);
        fadeEnd = Mathf.Clamp01(end);
        if (fadeEnd < fadeStart)
        {
            fadeEnd = fadeStart;
        }
        UpdateShaderProperties();
    }

    /// <summary>
    /// Disable the foveated effect (full visibility)
    /// </summary>
    public void DisableFade()
    {
        SetFadeParameters(1f, 1f);
    }

    /// <summary>
    /// Enable a default foveated effect
    /// </summary>
    public void EnableFade()
    {
        SetFadeParameters(0.6f, 0.9f);
    }
}

