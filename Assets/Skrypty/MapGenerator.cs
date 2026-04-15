using UnityEngine;
using System.Collections;

public class MapGenerator : MonoBehaviour
{
    [Header("=== Mapa ===")]
    public int resolution = 120;
    public float worldScale = 1.3f;

    [Header("=== Budynki ===")]
    public GameObject buildingPrefab;
    public float buildingThreshold = 0.45f;
    public float minHeight = 10f;
    public float maxHeight = 60f;

    public int buildingsPerFrame = 100;

    [Header("=== Wygląd ===")]
    public bool useColors = true;
    public float colorBoost = 1.2f;
    public float gap = 0.9f;

    private Texture2D processedTex;

    private void Start()
    {
        if (SelectedPhotoData.photo == null)
        {
            Debug.LogError("Nie wybrano zdjęcia w menu!");
            return;
        }

        StartCoroutine(Generate(SelectedPhotoData.photo));
    }

    private IEnumerator Generate(Texture2D inputPhoto)
    {
        processedTex = Resize(inputPhoto, resolution, resolution);

        int w = processedTex.width;
        int h = processedTex.height;

        CreateGround(w, h);

        int batch = 0;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float brightness = processedTex.GetPixel(x, y).grayscale;

                if (brightness < buildingThreshold)
                    continue;

                float factor = (brightness - buildingThreshold) / (1f - buildingThreshold);
                float height = Mathf.Lerp(minHeight, maxHeight, Mathf.Pow(factor, 1.3f));

                Vector3 pos = new Vector3(
                    (x - w / 2f) * worldScale,
                    0f,
                    (y - h / 2f) * worldScale
                );

                GameObject b = Instantiate(buildingPrefab, pos, Quaternion.identity, transform);

                b.transform.localScale = new Vector3(
                    worldScale * gap,
                    height,
                    worldScale * gap
                );

                b.transform.position += Vector3.up * height / 2f;

                if (useColors && b.TryGetComponent<Renderer>(out var r))
                    r.material.color = processedTex.GetPixel(x, y) * colorBoost;

                batch++;

                if (batch >= buildingsPerFrame)
                {
                    batch = 0;
                    yield return null;
                }
            }
        }
    }

    private void CreateGround(int w, int h)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Ground";
        g.transform.parent = transform;

        g.transform.localScale = new Vector3(w * worldScale, 1f, h * worldScale);
        g.transform.position = Vector3.zero;
        g.GetComponent<Renderer>().material.color = Color.gray * 0.3f;
    }

    private Texture2D Resize(Texture2D src, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h);
        Graphics.Blit(src, rt);

        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(w, h);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }
}

public static class SelectedPhotoData
{
    public static Texture2D photo;
}