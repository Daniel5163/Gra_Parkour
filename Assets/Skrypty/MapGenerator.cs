using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [Header("INPUT")]
    public Texture2D fallbackPhoto;

    [Header("WORLD")]
    public float worldSize = 180f;

    [Header("PERFORMANCE")]
    public int objectsPerFrame = 60;

    [Header("BUILDINGS")]
    public float minRegionSize = 150;
    public float minBuildingSize = 3f;
    public float maxAspectRatio = 5f;
    public float minHeight = 6f;
    public float maxHeight = 50f;

    [Header("COLOR")]
    public float similarityThreshold = 55f;
    public float greenThreshold = 0.25f;

    [Header("SPAWN")]
    public float minSpawnRadius = 8f; 
    public int maxSpawnAttempts = 1000;

    private int width, height;
    private Color32[] pixels;
    private bool[,] visited;
    private List<Bounds> spawnAreas = new List<Bounds>();
    private List<Bounds> buildingBounds = new List<Bounds>();
    private List<Bounds> safeSpawnZones = new List<Bounds>(); 

    enum Type { Building, Tree, Road, Grass }

    void Start()
    {
        Texture2D src = SelectedPhotoData.photo ?? fallbackPhoto;
        if (src == null)
        {
            Debug.LogError("Brak zdjęcia");
            return;
        }

        StartCoroutine(Generate(src));
    }

    IEnumerator Generate(Texture2D src)
    {
        Texture2D tex = MakeReadable(src);
        width = tex.width;
        height = tex.height;
        pixels = tex.GetPixels32();
        visited = new bool[width, height];

        CreateGround();

        Debug.Log("Generowanie budynków");
        int batch = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;

                Color32 baseColor = GetBlur(x, y);
                List<Vector2Int> region = FloodFill(x, y, baseColor);
                if (region.Count < minRegionSize) continue;

                Bounds b = GetBounds(region);
                Type t = ClassifyRegion(region);

                if (t == Type.Building && ValidBuilding(b))
                {
                    b = ResolveOverlap(b);

                    Create(region, b, t);
                    buildingBounds.Add(b);

                    MarkRegionAsVisited(region);
                    batch++;
                    if (batch >= objectsPerFrame)
                    {
                        batch = 0;
                        yield return null;
                    }
                }
            }
        }

        Debug.Log($"Wygenerowano {buildingBounds.Count} budynków");

        // ========================
        Debug.Log("Generowanie drzew, dróg i trawy");
        batch = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;

                Color32 baseColor = GetBlur(x, y);
                List<Vector2Int> region = FloodFill(x, y, baseColor);
                if (region.Count < minRegionSize) continue;

                Bounds b = GetBounds(region);
                Type t = ClassifyRegion(region);

                if (t != Type.Building)
                {
                    if (t == Type.Grass)
                    {
                        safeSpawnZones.Add(b);
                        spawnAreas.Add(b);
                    }
                    else if (t == Type.Road)
                    {
                        spawnAreas.Add(b);
                    }

                    Create(region, b, t);
                    batch++;
                    if (batch >= objectsPerFrame)
                    {
                        batch = 0;
                        yield return null;
                    }
                }
            }
        }


        yield return new WaitForSeconds(0.5f); 

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 spawnPos = FindSafeSpawn();
            player.transform.position = spawnPos;
        }
        else
        {
            Debug.LogError("Nie znaleziono gracza z tagiem 'Player'!");
        }
    }

    void MarkRegionAsVisited(List<Vector2Int> region)
    {
        foreach (var p in region)
        {
            if (p.x >= 0 && p.x < width && p.y >= 0 && p.y < height)
                visited[p.x, p.y] = true;
        }
    }

    Vector3 FindSafeSpawn()
    {
        if (safeSpawnZones.Count > 0)
        {

            var shuffledZones = safeSpawnZones.OrderBy(x => Random.value).ToList();

            foreach (var zone in shuffledZones)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    float offsetX = Random.Range(-zone.sx * 0.4f, zone.sx * 0.4f);
                    float offsetY = Random.Range(-zone.sy * 0.4f, zone.sy * 0.4f);

                    float worldX = ((zone.cx + offsetX) / width - 0.5f) * worldSize;
                    float worldZ = ((zone.cy + offsetY) / height - 0.5f) * worldSize;

                    Vector3 pos = new Vector3(worldX, 150f, worldZ);

                    if (!Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 300f))
                        continue;

                    pos = hit.point + Vector3.up * 2.5f;

                    if (IsValidSpawnPosition(pos))
                    {
                       
                        return pos;
                    }
                }
            }
        }

        if (spawnAreas.Count > 0)
        {

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Bounds b = spawnAreas[Random.Range(0, spawnAreas.Count)];

                float offsetX = Random.Range(-b.sx * 0.45f, b.sx * 0.45f);
                float offsetY = Random.Range(-b.sy * 0.45f, b.sy * 0.45f);

                float worldX = ((b.cx + offsetX) / width - 0.5f) * worldSize;
                float worldZ = ((b.cy + offsetY) / height - 0.5f) * worldSize;

                Vector3 pos = new Vector3(worldX, 150f, worldZ);

                if (!Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 300f))
                    continue;

                pos = hit.point + Vector3.up * 2.5f;

                if (IsValidSpawnPosition(pos))
                {
                    return pos;
                }
            }
        }

        return GenerateEdgeSpawn();
    }

    bool IsValidSpawnPosition(Vector3 pos)
    {
        Collider[] nearbyBuildings = Physics.OverlapSphere(pos, minSpawnRadius);

        foreach (var col in nearbyBuildings)
        {
            if (col.CompareTag("Building"))
                return false;
        }

        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 15f))
        {
            if (hit.collider.CompareTag("Building"))
                return false;
        }

        Vector3 checkRadius = new Vector3(2f, 1f, 2f);
        Collider[] groundCheck = Physics.OverlapBox(pos, checkRadius);

        int buildingCount = 0;
        foreach (var col in groundCheck)
        {
            if (col.CompareTag("Building"))
                buildingCount++;
        }

        if (buildingCount > 0)
            return false;

        return true;
    }

    Vector3 GenerateEdgeSpawn()
    {
        float margin = 15f;
        Vector3[] edgePositions = new Vector3[]
        {
            new Vector3(-worldSize/2 + margin, 150f, 0),  
            new Vector3(worldSize/2 - margin, 150f, 0),   
            new Vector3(0, 150f, -worldSize/2 + margin),  
            new Vector3(0, 150f, worldSize/2 - margin),   
            new Vector3(-worldSize/2 + margin, 150f, -worldSize/2 + margin), 
            new Vector3(worldSize/2 - margin, 150f, -worldSize/2 + margin),  
            new Vector3(-worldSize/2 + margin, 150f, worldSize/2 - margin),  
            new Vector3(worldSize/2 - margin, 150f, worldSize/2 - margin)   
        };

        foreach (var pos in edgePositions)
        {
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 300f))
            {
                Vector3 groundPos = hit.point + Vector3.up * 2.5f;

                Collider[] colCheck = Physics.OverlapSphere(groundPos, 3f);
                bool isBuilding = false;
                foreach (var col in colCheck)
                {
                    if (col.CompareTag("Building"))
                    {
                        isBuilding = true;
                        break;
                    }
                }

                if (!isBuilding)
                {
                    return groundPos;
                }
            }
        }

        return new Vector3(0, 50f, 0);
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying && safeSpawnZones != null)
        {
            Gizmos.color = Color.green;
            foreach (var zone in safeSpawnZones)
            {
                Vector3 worldPos = new Vector3(
                    (zone.cx / width - 0.5f) * worldSize,
                    0.1f,
                    (zone.cy / height - 0.5f) * worldSize
                );
                Vector3 worldSize3D = new Vector3(
                    (zone.sx / width) * worldSize,
                    0.1f,
                    (zone.sy / height) * worldSize
                );
                Gizmos.DrawWireCube(worldPos, worldSize3D);
            }

            Gizmos.color = Color.blue;
            foreach (var zone in spawnAreas)
            {
                if (!safeSpawnZones.Contains(zone))
                {
                    Vector3 worldPos = new Vector3(
                        (zone.cx / width - 0.5f) * worldSize,
                        0.1f,
                        (zone.cy / height - 0.5f) * worldSize
                    );
                    Vector3 worldSize3D = new Vector3(
                        (zone.sx / width) * worldSize,
                        0.1f,
                        (zone.sy / height) * worldSize
                    );
                    Gizmos.DrawWireCube(worldPos, worldSize3D);
                }
            }
        }
    }


    Texture2D MakeReadable(Texture2D src)
    {
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(src.GetPixels32());
        tex.Apply();
        return tex;
    }

    Color32 GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return new Color32(0, 0, 0, 255);
        return pixels[y * width + x];
    }

    Color32 GetBlur(int x, int y)
    {
        int r = 0, g = 0, b = 0, count = 0;
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                Color32 c = GetPixel(x + dx, y + dy);
                r += c.r; g += c.g; b += c.b;
                count++;
            }
        return new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
    }

    float Brightness(Color32 c) => (c.r + c.g + c.b) / 765f;

    bool IsGreen(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        return g > r + greenThreshold && g > b + greenThreshold;
    }

    bool Similar(Color32 a, Color32 b)
    {
        int dr = Mathf.Abs(a.r - b.r);
        int dg = Mathf.Abs(a.g - b.g);
        int db = Mathf.Abs(a.b - b.b);
        return (dr + dg + db) < similarityThreshold;
    }

    List<Vector2Int> FloodFill(int sx, int sy, Color32 baseColor)
    {
        List<Vector2Int> region = new List<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(new Vector2Int(sx, sy));
        visited[sx, sy] = true;

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            region.Add(p);

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = p.x + dx, ny = p.y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny]) continue;
                    if (!Similar(GetBlur(nx, ny), baseColor)) continue;

                    visited[nx, ny] = true;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
        }
        return region;
    }

    struct Bounds
    {
        public float cx, cy, sx, sy;
    }

    Bounds GetBounds(List<Vector2Int> r)
    {
        int minX = int.MaxValue, maxX = 0, minY = int.MaxValue, maxY = 0;
        foreach (var p in r)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        return new Bounds
        {
            cx = (minX + maxX) / 2f,
            cy = (minY + maxY) / 2f,
            sx = maxX - minX,
            sy = maxY - minY
        };
    }

    Type ClassifyRegion(List<Vector2Int> r)
    {
        int green = 0;
        float bright = 0f;
        foreach (var p in r)
        {
            Color32 c = GetPixel(p.x, p.y);
            if (IsGreen(c)) green++;
            bright += Brightness(c);
        }
        bright /= r.Count;

        if (green > r.Count * 0.4f) return Type.Tree;
        if (bright < 0.35f) return Type.Road;
        if (bright > 0.5f) return Type.Building;
        return Type.Grass;
    }

    bool ValidBuilding(Bounds b)
    {
        float sx = (b.sx / width) * worldSize;
        float sz = (b.sy / height) * worldSize;
        if (sx < minBuildingSize || sz < minBuildingSize) return false;

        float ratio = Mathf.Max(sx, sz) / Mathf.Max(1f, Mathf.Min(sx, sz));
        return ratio < maxAspectRatio;
    }

    Bounds ResolveOverlap(Bounds b)
    {
        Vector2 pos = new Vector2(b.cx, b.cy);

        for (int i = 0; i < 6; i++)
        {
            bool moved = false;

            foreach (var other in buildingBounds)
            {
                float dx = pos.x - other.cx;
                float dy = pos.y - other.cy;

                float minX = (b.sx + other.sx) * 0.5f;
                float minY = (b.sy + other.sy) * 0.5f;

                if (Mathf.Abs(dx) < minX && Mathf.Abs(dy) < minY)
                {
                    Vector2 dir = new Vector2(dx, dy);

                    if (dir == Vector2.zero)
                        dir = Random.insideUnitCircle;

                    dir.Normalize();

                    pos += dir * 2f;
                    moved = true;
                }
            }

            if (!moved)
                break;
        }

        b.cx = pos.x;
        b.cy = pos.y;

        return b;
    }

    Color Avg(List<Vector2Int> r)
    {
        float rC = 0, gC = 0, bC = 0;
        foreach (var p in r)
        {
            Color32 c = GetPixel(p.x, p.y);
            rC += c.r; gC += c.g; bC += c.b;
        }
        float n = r.Count;
        return new Color(rC / (n * 255f), gC / (n * 255f), bC / (n * 255f));
    }

    void Create(List<Vector2Int> region, Bounds b, Type t)
    {
        Vector3 pos = new Vector3(
            (b.cx / width - 0.5f) * worldSize,
            0,
            (b.cy / height - 0.5f) * worldSize
        );

        float sx = Mathf.Clamp((b.sx / width) * worldSize, 1.5f, 30f);
        float sz = Mathf.Clamp((b.sy / height) * worldSize, 1.5f, 30f);

        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.transform.position = pos;

        Color avg = Avg(region);

        if (t == Type.Building)
        {
            o.tag = "Building";
            o.name = "Building";
            float h = Mathf.Lerp(minHeight, maxHeight, Brightness(avg));
            o.transform.localScale = new Vector3(sx, h, sz);
            o.transform.position += Vector3.up * (h * 0.5f);
            SetColor(o, avg * 0.85f);
        }
        else if (t == Type.Tree)
        {
            float h = Random.Range(4f, 9f);
            o.transform.localScale = new Vector3(sx * 0.6f, h, sz * 0.6f);
            o.transform.position += Vector3.up * (h * 0.5f);
            SetColor(o, avg);
            Destroy(o.GetComponent<Collider>());
        }
        else if (t == Type.Road)
        {
            o.transform.localScale = new Vector3(sx, 0.2f, sz);
            o.transform.position += Vector3.up * 0.1f;
            SetColor(o, avg * 0.6f);
            Destroy(o.GetComponent<Collider>());
        }
        else // Grass
        {
            o.transform.localScale = new Vector3(sx, 0.1f, sz);
            o.transform.position += Vector3.up * 0.05f;
            SetColor(o, new Color(0.2f, 0.42f, 0.22f));
            Destroy(o.GetComponent<Collider>());
        }
    }

    void CreateGround()
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.transform.localScale = Vector3.one * (worldSize / 10f);
        SetColor(g, new Color(0.25f, 0.35f, 0.2f));
        g.name = "Ground";
    }

    void SetColor(GameObject o, Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        o.GetComponent<Renderer>().material = m;
    }
}

public static class SelectedPhotoData
{
    public static Texture2D photo;
}