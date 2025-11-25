using UnityEngine;

/// <summary>
/// Displays a radial fill indicator for selection progress.
/// Updates mesh geometry dynamically to support fill animation without custom shaders.
/// </summary>
public class SelectionRadialFill : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField, Tooltip("Material for the radial fill")]
    private Material fillMaterial;
    
    [SerializeField, Tooltip("Radius of the radial fill indicator")]
    private float radius = 0.05f;
    
    [SerializeField, Tooltip("Color of the fill indicator")]
    private Color fillColor = new Color(0f, 1f, 1f, 0.8f);
    
    [SerializeField, Tooltip("Width of the ring")]
    private float ringWidth = 0.01f;
    
    [SerializeField, Tooltip("Number of segments for the ring mesh")]
    private int segments = 64;
    
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private MaterialPropertyBlock propertyBlock;
    
    // Cached arrays to reduce GC
    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] triangles;
    private bool isInitialized = false;
    
    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;
        
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        
        propertyBlock = new MaterialPropertyBlock();
        
        // Initialize mesh
        mesh = new Mesh();
        mesh.name = "RadialFillRing";
        mesh.MarkDynamic(); // Optimize for frequent updates
        meshFilter.mesh = mesh;
        
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
        
        // Initialize arrays
        int vertexCount = (segments + 1) * 2;
        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        triangles = new int[segments * 6];
        
        // Generate triangle indices once (they remain constant as we just move vertices)
        for (int i = 0; i < segments; i++)
        {
            int nextI = i + 1;
            int triIndex = i * 6;
            
            // Vertices are arranged: 2 per segment index (outer, inner)
            // v[i*2] = outer, v[i*2+1] = inner
            
            triangles[triIndex] = i * 2;
            triangles[triIndex + 1] = i * 2 + 1;
            triangles[triIndex + 2] = nextI * 2;
            
            triangles[triIndex + 3] = nextI * 2;
            triangles[triIndex + 4] = i * 2 + 1;
            triangles[triIndex + 5] = nextI * 2 + 1;
        }
        
        mesh.vertices = vertices; // Just to size it
        mesh.triangles = triangles;
        
        isInitialized = true;
        
        // Initialize with zero fill
        SetFillAmount(0f);
    }
    
    /// <summary>
    /// Update the fill amount of the radial indicator
    /// </summary>
    /// <param name="progress">Fill amount from 0 to 1</param>
    public void SetFillAmount(float progress)
    {
        if (!isInitialized) Initialize();
        
        progress = Mathf.Clamp01(progress);
        
        // Control visibility
        if (meshRenderer != null)
        {
            meshRenderer.enabled = progress > 0.001f;
            
            if (progress > 0.001f)
            {
                // Update properties
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_FillAmount", progress);
                propertyBlock.SetColor("_Color", fillColor);
                meshRenderer.SetPropertyBlock(propertyBlock);
                
                // Update geometry
                UpdateMeshGeometry(progress);
            }
        }
    }
    
    private void UpdateMeshGeometry(float progress)
    {
        // Clockwise from top (12 o'clock)
        // Top corresponds to +Y in local space (assuming Z is forward/normal)
        // Angle = PI/2 is Top.
        // Clockwise means angle decreases.
        
        float startAngle = Mathf.PI / 2f;
        float totalSweep = progress * Mathf.PI * 2f;
        
        float outerRadius = radius;
        float innerRadius = radius - ringWidth;
        
        // Distribute segments across the current sweep angle
        // This ensures the arc is always smooth regardless of length
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = startAngle - (t * totalSweep); 
            
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            int vIndex = i * 2;
            
            // Outer vertex
            vertices[vIndex].x = cos * outerRadius;
            vertices[vIndex].y = sin * outerRadius;
            vertices[vIndex].z = 0;
            
            uvs[vIndex].x = t;
            uvs[vIndex].y = 1;
            
            // Inner vertex
            vertices[vIndex+1].x = cos * innerRadius;
            vertices[vIndex+1].y = sin * innerRadius;
            vertices[vIndex+1].z = 0;
            
            uvs[vIndex+1].x = t;
            uvs[vIndex+1].y = 0;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        
        // Recalculate bounds to ensure no culling issues
        mesh.RecalculateBounds();
    }
    
    /// <summary>
    /// Set the radius of the ring
    /// </summary>
    public void SetRadius(float newRadius)
    {
        radius = newRadius;
        // Trigger update if we have content
        if (meshRenderer != null && meshRenderer.enabled)
        {
            // We can't know the current progress easily unless we store it, 
            // but usually SetRadius is called on init.
            // Let's just force a full rebuild if needed or rely on next update.
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
