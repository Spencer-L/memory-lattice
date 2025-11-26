using UnityEngine;

/// <summary>
/// Displays a radial fill indicator for selection progress, plus a static concentric ring and reticle lines.
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
    
    // Dynamic Ring Components
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
    private MaterialPropertyBlock propertyBlock;
    
    // Static Ring Components
    private GameObject staticRingObj;
    private MeshRenderer staticRingRenderer;
    private MeshFilter staticRingFilter;
    private MaterialPropertyBlock staticRingProps;
    
    // Reticle Lines Components
    private GameObject linesObj;
    private MeshRenderer linesRenderer;
    private MeshFilter linesFilter;
    private MaterialPropertyBlock linesProps;
    
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
        
        // --- Dynamic Ring Setup ---
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        
        propertyBlock = new MaterialPropertyBlock();
        
        mesh = new Mesh();
        mesh.name = "DynamicFillRing";
        mesh.MarkDynamic();
        meshFilter.mesh = mesh;
        
        // Shared material setup
        Material baseMat = fillMaterial;
        if (baseMat == null) baseMat = new Material(Shader.Find("Sprites/Default"));
        
        meshRenderer.material = baseMat;
        
        // Initialize arrays for dynamic ring
        int vertexCount = (segments + 1) * 2;
        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        triangles = new int[segments * 6];
        
        // Generate triangle indices once
        for (int i = 0; i < segments; i++)
        {
            int nextI = i + 1;
            int triIndex = i * 6;
            triangles[triIndex] = i * 2;
            triangles[triIndex + 1] = i * 2 + 1;
            triangles[triIndex + 2] = nextI * 2;
            triangles[triIndex + 3] = nextI * 2;
            triangles[triIndex + 4] = i * 2 + 1;
            triangles[triIndex + 5] = nextI * 2 + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        
        // --- Static Ring Setup ---
        staticRingObj = new GameObject("StaticRing");
        staticRingObj.transform.SetParent(transform, false);
        staticRingRenderer = staticRingObj.AddComponent<MeshRenderer>();
        staticRingFilter = staticRingObj.AddComponent<MeshFilter>();
        staticRingRenderer.material = baseMat;
        staticRingProps = new MaterialPropertyBlock();
        
        // --- Reticle Lines Setup ---
        linesObj = new GameObject("ReticleLines");
        linesObj.transform.SetParent(transform, false);
        linesRenderer = linesObj.AddComponent<MeshRenderer>();
        linesFilter = linesObj.AddComponent<MeshFilter>();
        linesRenderer.material = baseMat;
        linesProps = new MaterialPropertyBlock();
        
        isInitialized = true;
        
        // Initialize with zero fill
        SetFillAmount(0f);
    }
    
    /// <summary>
    /// Update the fill amount of the radial indicator
    /// </summary>
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
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_FillAmount", progress);
                propertyBlock.SetColor("_Color", fillColor);
                meshRenderer.SetPropertyBlock(propertyBlock);
                
                UpdateMeshGeometry(progress);
            }
        }
    }
    
    private void UpdateMeshGeometry(float progress)
    {
        // Clockwise from top (PI/2)
        float startAngle = Mathf.PI / 2f;
        float totalSweep = progress * Mathf.PI * 2f;
        
        float outerRadius = radius;
        float innerRadius = radius - ringWidth;
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = startAngle - (t * totalSweep); 
            
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            int vIndex = i * 2;
            
            vertices[vIndex].x = cos * outerRadius;
            vertices[vIndex].y = sin * outerRadius;
            vertices[vIndex].z = 0;
            uvs[vIndex] = new Vector2(t, 1);
            
            vertices[vIndex+1].x = cos * innerRadius;
            vertices[vIndex+1].y = sin * innerRadius;
            vertices[vIndex+1].z = 0;
            uvs[vIndex+1] = new Vector2(t, 0);
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
    }
    
    public void SetRadius(float newRadius)
    {
        radius = newRadius;
    }
    
    public void SetColor(Color color)
    {
        fillColor = color;
    }
    
    // --- New Configuration Methods ---
    
    public void ConfigureStaticRing(float radius, float thickness, Color color)
    {
        if (!isInitialized) Initialize();
        
        // Update Color
        staticRingRenderer.GetPropertyBlock(staticRingProps);
        staticRingProps.SetColor("_Color", color);
        staticRingRenderer.SetPropertyBlock(staticRingProps);
        
        // Generate Mesh
        Mesh ringMesh = new Mesh();
        ringMesh.name = "StaticRingMesh";
        
        int segs = 64;
        Vector3[] ringVerts = new Vector3[(segs + 1) * 2];
        int[] ringTris = new int[segs * 6];
        Vector2[] ringUVs = new Vector2[(segs + 1) * 2];
        
        float outer = radius;
        float inner = radius - thickness;
        
        for (int i = 0; i <= segs; i++)
        {
            float angle = (float)i / segs * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            int v = i * 2;
            ringVerts[v] = new Vector3(cos * outer, sin * outer, 0);
            ringVerts[v+1] = new Vector3(cos * inner, sin * inner, 0);
            ringUVs[v] = new Vector2((float)i/segs, 1);
            ringUVs[v+1] = new Vector2((float)i/segs, 0);
            
            if (i < segs)
            {
                int t = i * 6;
                ringTris[t] = v;
                ringTris[t+1] = v+1;
                ringTris[t+2] = (i+1)*2;
                ringTris[t+3] = (i+1)*2;
                ringTris[t+4] = v+1;
                ringTris[t+5] = (i+1)*2+1;
            }
        }
        
        ringMesh.vertices = ringVerts;
        ringMesh.triangles = ringTris;
        ringMesh.uv = ringUVs;
        ringMesh.RecalculateBounds();
        
        staticRingFilter.mesh = ringMesh;
    }
    
    public void ConfigureReticleLines(float startOffset, float length, float width, Color color)
    {
        if (!isInitialized) Initialize();
        
        // Update Color
        linesRenderer.GetPropertyBlock(linesProps);
        linesProps.SetColor("_Color", color);
        linesRenderer.SetPropertyBlock(linesProps);
        
        // Generate Mesh (2 vertical lines)
        Mesh linesMesh = new Mesh();
        linesMesh.name = "ReticleLinesMesh";
        
        // 4 vertices per line = 8 vertices
        Vector3[] lVerts = new Vector3[8];
        int[] lTris = new int[12]; // 2 quads * 6
        Vector2[] lUVs = new Vector2[8];
        
        float halfWidth = width * 0.5f;
        float yStart = startOffset;
        float yEnd = startOffset + length;
        
        // Top Line
        lVerts[0] = new Vector3(-halfWidth, yStart, 0);
        lVerts[1] = new Vector3(halfWidth, yStart, 0);
        lVerts[2] = new Vector3(-halfWidth, yEnd, 0);
        lVerts[3] = new Vector3(halfWidth, yEnd, 0);
        
        lUVs[0] = Vector2.zero; lUVs[1] = Vector2.right;
        lUVs[2] = Vector2.up; lUVs[3] = Vector2.one;
        
        lTris[0] = 0; lTris[1] = 2; lTris[2] = 1;
        lTris[3] = 1; lTris[4] = 2; lTris[5] = 3;
        
        // Bottom Line (mirror Y)
        lVerts[4] = new Vector3(-halfWidth, -yStart, 0);
        lVerts[5] = new Vector3(halfWidth, -yStart, 0);
        lVerts[6] = new Vector3(-halfWidth, -yEnd, 0);
        lVerts[7] = new Vector3(halfWidth, -yEnd, 0);
        
        lUVs[4] = Vector2.zero; lUVs[5] = Vector2.right;
        lUVs[6] = Vector2.up; lUVs[7] = Vector2.one;
        
        lTris[6] = 4; lTris[7] = 5; lTris[8] = 6;
        lTris[9] = 5; lTris[10] = 7; lTris[11] = 6;
        
        linesMesh.vertices = lVerts;
        linesMesh.triangles = lTris;
        linesMesh.uv = lUVs;
        linesMesh.RecalculateBounds();
        
        linesFilter.mesh = linesMesh;
    }
}
