using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public Texture2D defaultMapTexture;
    public float scale = 1.35f;

    [Header("Ground")]
   
    public float groundOffset = -0.15f;

    void Start()
    {
        Texture2D mapTexture = SelectedPhotoData.selectedTexture != null
            ? SelectedPhotoData.selectedTexture
            : defaultMapTexture;

        if (mapTexture == null)
        {
            Debug.LogError("Brak tekstury mapy!");
            return;
        }

        CreateColoredGround(mapTexture);
    }

    void CreateColoredGround(Texture2D mapTexture)
    {
        int w = mapTexture.width;
        int h = mapTexture.height;
        float totalW = w * scale;
        float totalH = h * scale;

        int divX = Mathf.Min(w, 256);
        int divZ = Mathf.Min(h, 256);

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] verts = new Vector3[(divX + 1) * (divZ + 1)];
        Vector2[] uvs = new Vector2[(divX + 1) * (divZ + 1)];
        int[] tris = new int[divX * divZ * 6];

        for (int z = 0; z <= divZ; z++)
            for (int x = 0; x <= divX; x++)
            {
                int idx = z * (divX + 1) + x;
                verts[idx] = new Vector3(x * totalW / divX, 0f, z * totalH / divZ);
                uvs[idx] = new Vector2((float)x / divX, (float)z / divZ);
            }

        int t = 0;
        for (int z = 0; z < divZ; z++)
            for (int x = 0; x < divX; x++)
            {
                int i0 = z * (divX + 1) + x;
                tris[t++] = i0;
                tris[t++] = i0 + divX + 1;
                tris[t++] = i0 + 1;
                tris[t++] = i0 + 1;
                tris[t++] = i0 + divX + 1;
                tris[t++] = i0 + divX + 2;
            }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = mapTexture;
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Metallic", 0f);

        GameObject ground = new GameObject("Ground");
        ground.AddComponent<MeshFilter>().mesh = mesh;
        ground.AddComponent<MeshRenderer>().material = mat;
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;

        ground.transform.position = new Vector3(0f, groundOffset, 0f);
    }
}