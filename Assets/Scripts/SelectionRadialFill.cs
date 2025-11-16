using UnityEngine;

/// <summary>
/// Displays a radial fill indicator for selection progress.
/// Can be used with a material that supports fill amount or by scaling ring geometry.
/// </summary>
public class SelectionRadialFill : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField, Tooltip("Material for the radial fill (should support _FillAmount property)")]
    private Material fillMaterial;
    
    [SerializeField, Tooltip("Radius of the radial fill indicator")]
    private float radius = 0.05f;
    
    [SerializeField, Tooltip("Color of the fill indicator")]
    private Color fillColor = new Color(1f, 1f, 1f, 0.8f);
    
    [SerializeField, Tooltip("Width of the ring")]
    private float ringWidth = 0.01f;
    
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    
    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        propertyBlock = new MaterialPropertyBlock();
        
        // Create a simple ring mesh if none exists
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateRingMesh(radius, ringWidth);
        }
        
        // Apply material
        if (fillMaterial != null)
        {
            meshRenderer.material = fillMaterial;
        }
        else
        {
            // Use a simple unlit shader if no material provided
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        
        // Initialize with zero fill
        SetFillAmount(0f);
    }
    
    /// <summary>
    /// Update the fill amount of the radial indicator
    /// </summary>
    /// <param name="progress">Fill amount from 0 to 1</param>
    public void SetFillAmount(float progress)
    {
        progress = Mathf.Clamp01(progress);
        
        // Try to set shader property if supported
        if (meshRenderer != null)
        {
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_FillAmount", progress);
            propertyBlock.SetColor("_Color", fillColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        
        // Also control visibility - hide when empty
        if (meshRenderer != null)
        {
            meshRenderer.enabled = progress > 0.001f;
        }
    }
    
    /// <summary>
    /// Create a simple ring mesh for the radial fill indicator
    /// </summary>
    private Mesh CreateRingMesh(float outerRadius, float thickness)
    {
        Mesh mesh = new Mesh();
        mesh.name = "RadialFillRing";
        
        int segments = 32;
        float innerRadius = outerRadius - thickness;
        
        Vector3[] vertices = new Vector3[segments * 2];
        Vector2[] uvs = new Vector2[segments * 2];
        int[] triangles = new int[segments * 6];
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            // Outer vertex
            vertices[i * 2] = new Vector3(cos * outerRadius, sin * outerRadius, 0);
            uvs[i * 2] = new Vector2((float)i / segments, 1);
            
            // Inner vertex
            vertices[i * 2 + 1] = new Vector3(cos * innerRadius, sin * innerRadius, 0);
            uvs[i * 2 + 1] = new Vector2((float)i / segments, 0);
            
            // Triangles
            int nextI = (i + 1) % segments;
            int triIndex = i * 6;
            
            triangles[triIndex] = i * 2;
            triangles[triIndex + 1] = i * 2 + 1;
            triangles[triIndex + 2] = nextI * 2;
            
            triangles[triIndex + 3] = nextI * 2;
            triangles[triIndex + 4] = i * 2 + 1;
            triangles[triIndex + 5] = nextI * 2 + 1;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Set the radius of the ring
    /// </summary>
    public void SetRadius(float newRadius)
    {
        radius = newRadius;
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.mesh = CreateRingMesh(radius, ringWidth);
        }
    }
    
    /// <summary>
    /// Set the color of the fill
    /// </summary>
    public void SetColor(Color color)
    {
        fillColor = color;
    }
}

