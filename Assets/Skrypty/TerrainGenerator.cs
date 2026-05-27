using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public Texture2D defaultMapTexture;
    public float scale = 1f;

    [Header("Ground")]
    public Material groundMaterial;
    public float groundOffset = -0.1f;

    void Start()
    {
        Texture2D mapTexture = SelectedPhotoData.selectedTexture != null ?
                               SelectedPhotoData.selectedTexture :
                               defaultMapTexture;

        if (mapTexture == null)
        {
            Debug.LogError("Brak tekstury mapy dla terenu!");
            return;
        }

        GenerateTerrain(mapTexture);
        CreateGround(mapTexture);
    }

    void GenerateTerrain(Texture2D mapTexture)
    {
        int w = mapTexture.width;
        int h = mapTexture.height;

        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[w * h];
        Vector2[] uv = new Vector2[w * h];
        int[] triangles = new int[(w - 1) * (h - 1) * 6];

        int i = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                vertices[i] = new Vector3(x * scale, 0, y * scale);
                uv[i] = new Vector2((float)x / w, (float)y / h);
                i++;
            }

        int t = 0;
        for (int y = 0; y < h - 1; y++)
            for (int x = 0; x < w - 1; x++)
            {
                int i0 = x + y * w;
                int i1 = i0 + 1;
                int i2 = i0 + w;
                int i3 = i2 + 1;

                triangles[t++] = i0;
                triangles[t++] = i2;
                triangles[t++] = i1;

                triangles[t++] = i1;
                triangles[t++] = i2;
                triangles[t++] = i3;
            }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject terrainObj = new GameObject("Terrain");
        terrainObj.AddComponent<MeshFilter>().mesh = mesh;
        terrainObj.AddComponent<MeshRenderer>().material = groundMaterial;
    }

    void CreateGround(Texture2D mapTexture)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        float width = mapTexture.width * scale;
        float height = mapTexture.height * scale;

        ground.transform.position = new Vector3(width / 2f, groundOffset, height / 2f);
        ground.transform.localScale = new Vector3(width / 10f, 1f, height / 10f);
        ground.name = "Ground";

        var renderer = ground.GetComponent<Renderer>();
        if (groundMaterial != null)
            renderer.material = groundMaterial;
        else
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.2f, 0.25f, 0.2f);
        }
    }
}