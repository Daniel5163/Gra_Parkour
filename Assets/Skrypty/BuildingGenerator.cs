using UnityEngine;
using System.Collections.Generic;

public class BuildingGenerator : MonoBehaviour
{
    [Header("Tekstura")]
    public Texture2D defaultMapTexture;
    public float scale = 1.35f;

    [Header("Wysokość")]
    public float minHeight = 8f;
    public float maxHeight = 48f;
    public float heightVariation = 6f;

    [Header("Filtry")]
    public int minIslandSize = 38;
    public int maxIslandSize = 18000;
    public float maxAspectRatio = 12f;

    [Header("Preprocessing")]
    public int blurRadius = 2;
    public float contrast = 1.85f;
    public float edgeStrength = 1.55f;

    [Header("Morfologia")]
    public int morphRadius = 3;

    [Header("Prostokąty")]
    [Range(0.5f, 1f)] public float minFillDensity = 0.73f;

    public ParkourGraph graph;

    private Color[,] pixels;
    private bool[,] visited;
    private bool[,] buildingMask;

    void Start()
    {
        Texture2D rawTexture = SelectedPhotoData.selectedTexture != null
            ? SelectedPhotoData.selectedTexture
            : defaultMapTexture;

        if (rawTexture == null)
        {
            Debug.LogError("Brak tekstury!");
            return;
        }

        Texture2D processed = PreprocessTexture(rawTexture);

        int w = processed.width;
        int h = processed.height;
        Color[] raw = processed.GetPixels();

        pixels = new Color[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[x, y] = raw[x + y * w];

        visited = new bool[w, h];
        graph.roofPoints.Clear();

        buildingMask = BuildMask(w, h);
        buildingMask = MorphClose(buildingMask, w, h, morphRadius);

        Generate(w, h);
        graph.BuildGraph();
    }

    Texture2D PreprocessTexture(Texture2D input)
    {
        int w = input.width;
        int h = input.height;
        Color[] pixels = input.GetPixels();
        Color[] result = new Color[pixels.Length];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = x + y * w;
                Color c = pixels[idx];

                c = (c - Color.gray) * contrast + Color.gray;

                if (blurRadius > 0)
                {
                    Color avg = Color.black;
                    int count = 0;
                    for (int dy = -blurRadius; dy <= blurRadius; dy++)
                        for (int dx = -blurRadius; dx <= blurRadius; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            {
                                avg += pixels[nx + ny * w];
                                count++;
                            }
                        }
                    c = Color.Lerp(c, avg / count, 0.4f);
                }

                if (x > 1 && x < w - 2 && y > 1 && y < h - 2)
                {
                    float edge = Mathf.Abs(pixels[idx - 1].grayscale - pixels[idx + 1].grayscale) +
                                 Mathf.Abs(pixels[idx - w].grayscale - pixels[idx + w].grayscale);
                    c *= (1f + edge * edgeStrength);
                }

                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                result[idx] = c;
            }
        }

        Texture2D output = new Texture2D(w, h, TextureFormat.RGB24, false);
        output.SetPixels(result);
        output.Apply();
        return output;
    }

    bool[,] BuildMask(int w, int h)
    {
        bool[,] mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = IsBuilding(pixels[x, y]);
        return mask;
    }

    bool[,] MorphClose(bool[,] mask, int w, int h, int r)
    {
        mask = Dilate(mask, w, h, r);
        mask = Erode(mask, w, h, r);
        mask = Dilate(mask, w, h, r);
        return mask;
    }

    bool[,] Dilate(bool[,] mask, int w, int h, int r)
    {
        bool[,] result = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool found = false;
                for (int dy = -r; dy <= r && !found; dy++)
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[nx, ny])
                            found = true;
                    }
                result[x, y] = found;
            }
        return result;
    }

    bool[,] Erode(bool[,] mask, int w, int h, int r)
    {
        bool[,] result = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool all = true;
                for (int dy = -r; dy <= r && all; dy++)
                    for (int dx = -r; dx <= r && all; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h || !mask[nx, ny])
                            all = false;
                    }
                result[x, y] = all;
            }
        return result;
    }

    void Generate(int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (visited[x, y] || !buildingMask[x, y])
                {
                    visited[x, y] = true;
                    continue;
                }

                var island = FloodFill(x, y, w, h);
                if (island.Count < minIslandSize || island.Count > maxIslandSize) continue;

                var aabb = IslandAABB(island);
                float aspect = (float)Mathf.Max(aabb.width, aabb.height) / Mathf.Max(1, Mathf.Min(aabb.width, aabb.height));
                if (aspect > maxAspectRatio) continue;

                SpawnBuilding(island);
            }
    }

    List<Vector2Int> FloodFill(int startX, int startY, int w, int h)
    {
        var result = new List<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            result.Add(cur);

            foreach (var d in new Vector2Int[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) })
            {
                var n = new Vector2Int(cur.x + d.x, cur.y + d.y);
                if (n.x < 0 || n.x >= w || n.y < 0 || n.y >= h) continue;
                if (visited[n.x, n.y] || !buildingMask[n.x, n.y]) continue;

                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return result;
    }

    RectInt IslandAABB(List<Vector2Int> island)
    {
        if (island.Count == 0) return new RectInt(0, 0, 0, 0);

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var p in island)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    void SpawnBuilding(List<Vector2Int> island)
    {
        var remaining = new HashSet<Vector2Int>(island);
        var material = MakeMaterial(island);

        while (remaining.Count >= minIslandSize)
        {
            var rect = FindBestRect(remaining);
            if (rect.width < 5 || rect.height < 5) break;

            SpawnRectBuilding(rect, material);

            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                    remaining.Remove(new Vector2Int(x, y));
        }
    }

    RectInt FindBestRect(HashSet<Vector2Int> pixels)
    {
        if (pixels.Count == 0) return new RectInt(0, 0, 0, 0);

        int xMin = int.MaxValue, xMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;

        foreach (var p in pixels)
        {
            xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
        }

        int width = xMax - xMin + 1;
        int height = yMax - yMin + 1;
        int[] histogram = new int[width];
        RectInt bestRect = new RectInt(xMin, yMin, width, height);
        float bestScore = -1f;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                var pt = new Vector2Int(xMin + col, yMin + row);
                histogram[col] = pixels.Contains(pt) ? histogram[col] + 1 : 0;
            }

            var rect = LargestRectInHistogram(histogram, xMin, yMin + row, width);
            if (rect.width < 5 || rect.height < 5) continue;

            int covered = 0;
            for (int ry = rect.yMin; ry < rect.yMax; ry++)
                for (int rx = rect.xMin; rx < rect.xMax; rx++)
                    if (pixels.Contains(new Vector2Int(rx, ry))) covered++;

            float density = covered / (float)(rect.width * rect.height);
            if (density < minFillDensity) continue;

            float score = rect.width * rect.height * density;
            if (score > bestScore)
            {
                bestScore = score;
                bestRect = rect;
            }
        }
        return bestRect;
    }

    RectInt LargestRectInHistogram(int[] heights, int xOffset, int bottomRow, int w)
    {
        Stack<int> stack = new Stack<int>();
        RectInt best = new RectInt(xOffset, bottomRow, 1, 1);
        int maxArea = 0;

        for (int i = 0; i <= w; i++)
        {
            int h = (i == w) ? 0 : heights[i];
            while (stack.Count > 0 && heights[stack.Peek()] > h)
            {
                int height = heights[stack.Pop()];
                int width = stack.Count == 0 ? i : i - stack.Peek() - 1;
                int area = height * width;

                if (area > maxArea)
                {
                    maxArea = area;
                    int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                    best = new RectInt(xOffset + left, bottomRow - height + 1, width, height);
                }
            }
            stack.Push(i);
        }
        return best;
    }

    void SpawnRectBuilding(RectInt rect, Material mat)
    {
        float rSum = 0, gSum = 0, bSum = 0;
        int count = 0;

        for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                if (x < 0 || x >= pixels.GetLength(0) || y < 0 || y >= pixels.GetLength(1)) continue;
                Color c = pixels[x, y];
                rSum += c.r; gSum += c.g; bSum += c.b;
                count++;
            }

        if (count == 0) return;

        Color avgColor = new Color(rSum / count, gSum / count, bSum / count);
        float brightness = avgColor.grayscale;

        float height = Mathf.Lerp(minHeight, maxHeight, brightness);
        height += Random.Range(-heightVariation, heightVariation);
        height = Mathf.Max(height, minHeight);

        float cx = (rect.xMin + rect.width * 0.5f) * scale;
        float cz = (rect.yMin + rect.height * 0.5f) * scale;

        float padding = 0.86f; 
        float sizeX = rect.width * scale * padding;
        float sizeZ = rect.height * scale * padding;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = new Vector3(cx, height * 0.5f, cz);
        go.transform.localScale = new Vector3(sizeX, height, sizeZ);
        go.GetComponent<Renderer>().material = mat;

        graph.roofPoints.Add(new Vector3(cx, height, cz));
    }

    Material MakeMaterial(List<Vector2Int> island)
    {
        if (island.Count == 0)
            return new Material(Shader.Find("Standard"));

        float rSum = 0, gSum = 0, bSum = 0;
        foreach (var p in island)
        {
            Color c = pixels[p.x, p.y];
            rSum += c.r; gSum += c.g; bSum += c.b;
        }

        Color avg = new Color(rSum / island.Count, gSum / island.Count, bSum / island.Count);

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = avg * 0.9f;
        mat.SetFloat("_Glossiness", 0.1f);
        return mat;
    }

    bool IsBuilding(Color c)
    {
        float brightness = (c.r + c.g + c.b) / 3f;
        float sat = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);

        if (brightness < 0.33f || brightness > 0.87f) return false;
        if (sat < 0.1f) return false;

        if (c.g > 0.42f && c.g > c.r * 1.25f) return false;
        if (c.b > 0.52f && c.b > c.r * 1.2f) return false;
        if (c.r > 0.6f && brightness < 0.52f) return false;

        return true;
    }
}