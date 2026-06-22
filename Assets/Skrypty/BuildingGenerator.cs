using UnityEngine;
using System.Collections.Generic;

public class BuildingGenerator : MonoBehaviour
{
    [Header("Tekstura")]
    public Texture2D defaultMapTexture;
    public float scale = 1.35f;

    [Header("Preprocessing")]
    [Range(1f, 3f)] public float contrast = 2.1f;
    [Range(0, 2)] public int gaussianBlurRadius = 1;
    [Range(0, 3)] public int medianRadius = 1;

    [Header("Binaryzacja (legacy, używane tylko do edgeMap/wysokości)")]
    public bool useAdaptiveBinary = true;
    public int adaptiveWindowSize = 51;
    [Range(0.05f, 0.35f)] public float adaptiveBias = 0.15f;

    [Header("Klasyfikacja koloru (HSV)")]
    [Range(0f, 1f)] public float greenHueMin = 0.18f;
    [Range(0f, 1f)] public float greenHueMax = 0.47f;
    [Range(0f, 1f)] public float greenSatMin = 0.18f;

    [Range(0f, 1f)] public float asphaltSatMax = 0.12f;
    [Range(0f, 1f)] public float asphaltValMax = 0.45f;

    [Range(0f, 1f)] public float darkShadowValMax = 0.15f;

    [Range(0f, 0.5f)] public float redHueMax = 0.08f;
    [Range(0.5f, 1f)] public float redHueMin = 0.92f;
    [Range(0f, 1f)] public float buildingValMin = 0.35f;

    [Header("Debug")]
    public bool exportMaskDebug = false;
    public string maskDebugPath = "Assets/buildingMaskDebug.png";

    [Header("Morfologia i separacja")]
    public int morphCloseRadius = 1;
    public int separationErodeRadius = 1;
    public bool useDistanceSeparation = true;

    [Header("Filtry wysp")]
    public int minIslandSize = 65;
    public int maxIslandSize = 22000;
    public float maxAspectRatio = 9f;
    public int edgeMargin = 25;

    [Header("Wysokość budynków")]
    public float minHeight = 8f;
    public float maxHeight = 52f;
    public float heightVariation = 6f;
    public float edgeHeightBoost = 14f;
    public float brightnessHeightBoost = 0.6f;

    [Header("Prostokąty")]
    [Range(0.65f, 1f)] public float minFillDensity = 0.72f;

    public ParkourGraph graph;

    private Color[,] pixels;
    private Color[,] colorPixels;
    private bool[,] visited;
    private bool[,] buildingMask;
    private float[,] edgeMap;

    void Start()
    {
        Texture2D rawTexture = SelectedPhotoData.selectedTexture != null ? SelectedPhotoData.selectedTexture : defaultMapTexture;
        if (rawTexture == null) { Debug.LogError("Brak tekstury!"); return; }

        Debug.Log($"Start generowania - {rawTexture.width}x{rawTexture.height}");

        int w0 = rawTexture.width, h0 = rawTexture.height;
        Color[] rawColors = rawTexture.GetPixels();
        colorPixels = new Color[w0, h0];
        for (int y = 0; y < h0; y++)
            for (int x = 0; x < w0; x++)
                colorPixels[x, y] = rawColors[x + y * w0];

        Texture2D gray = ConvertToGrayscale(rawTexture);
        Texture2D contrasted = ApplyContrast(gray);

        if (gaussianBlurRadius > 0)
            contrasted = ApplyGaussianBlur(contrasted, gaussianBlurRadius);

        Texture2D binary = useAdaptiveBinary
            ? ApplyAdaptiveBinary(contrasted)
            : ApplyThresholdBinary(contrasted);

        if (medianRadius > 0)
            binary = ApplyMedianFilter(binary, medianRadius);

        int w = binary.width, h = binary.height;
        pixels = new Color[w, h];
        Color[] raw = binary.GetPixels();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[x, y] = raw[x + y * w];

        edgeMap = BuildEdgeMap(gray, w, h);
        visited = new bool[w, h];
        graph.roofPoints.Clear();

        buildingMask = BuildMask(w, h);
        LogMaskStats("Po klasyfikacji kolorów", w, h);

        if (morphCloseRadius > 0)
            buildingMask = MorphClose(buildingMask, w, h, morphCloseRadius);

        if (useDistanceSeparation && separationErodeRadius > 0)
        {
            buildingMask = Erode(buildingMask, w, h, separationErodeRadius);
            LogMaskStats("Po erozji separacyjnej", w, h);
        }

        if (exportMaskDebug)
            ExportMaskDebug(w, h);

        Generate(w, h);
        graph.BuildGraph();

        Debug.Log($"Zakończono! Wygenerowano {graph.roofPoints.Count} budynków.");
    }

    bool IsBuildingPixel(Color c)
    {
        Color.RGBToHSV(c, out float hue, out float sat, out float val);

        bool isGreen = hue > greenHueMin && hue < greenHueMax && sat > greenSatMin;

        bool isAsphalt = sat < asphaltSatMax && val < asphaltValMax;

        bool isDarkShadow = val < darkShadowValMax;

        if (isGreen || isAsphalt || isDarkShadow) return false;

        bool isReddish = hue < redHueMax || hue > redHueMin;
        bool isLightEnough = val > buildingValMin;

        return isReddish || isLightEnough;
    }

    bool[,] BuildMask(int w, int h)
    {
        bool[,] mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = IsBuildingPixel(colorPixels[x, y]);
        return mask;
    }

    void ExportMaskDebug(int w, int h)
    {
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[x + y * w] = buildingMask[x, y] ? Color.white : Color.black;

        Texture2D tex = MakeTexture(px, w, h);
        byte[] png = tex.EncodeToPNG();

#if UNITY_EDITOR
        System.IO.File.WriteAllBytes(maskDebugPath, png);
        Debug.Log($"Zapisano debug maski do: {maskDebugPath}");
#else
        Debug.LogWarning("exportMaskDebug działa tylko w edytorze.");
#endif
    }
    Texture2D ConvertToGrayscale(Texture2D input)
    {
        int w = input.width, h = input.height;
        Color[] px = input.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            float g = px[i].grayscale;
            px[i] = new Color(g, g, g);
        }
        return MakeTexture(px, w, h);
    }

    Texture2D ApplyContrast(Texture2D input)
    {
        int w = input.width, h = input.height;
        Color[] px = input.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = (px[i] - Color.gray) * contrast + Color.gray;
            c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
            px[i] = c;
        }
        return MakeTexture(px, w, h);
    }

    Texture2D ApplyGaussianBlur(Texture2D input, int radius)
    {
        int w = input.width, h = input.height;
        Color[] src = input.GetPixels();
        Color[] dst = new Color[src.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0;
                int count = 0;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, w - 1);
                        int ny = Mathf.Clamp(y + dy, 0, h - 1);
                        Color c = src[nx + ny * w];
                        r += c.r; g += c.g; b += c.b;
                        count++;
                    }
                dst[x + y * w] = new Color(r / count, g / count, b / count);
            }
        return MakeTexture(dst, w, h);
    }

    Texture2D ApplyAdaptiveBinary(Texture2D input)
    {
        int w = input.width, h = input.height;
        Color[] px = input.GetPixels();
        double[,] integral = new double[w + 1, h + 1];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                integral[x + 1, y + 1] = px[x + y * w].grayscale
                    + integral[x, y + 1] + integral[x + 1, y] - integral[x, y];

        int half = adaptiveWindowSize / 2;
        Color[] result = new Color[px.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int x1 = Mathf.Max(0, x - half), y1 = Mathf.Max(0, y - half);
                int x2 = Mathf.Min(w - 1, x + half), y2 = Mathf.Min(h - 1, y + half);
                int count = (x2 - x1 + 1) * (y2 - y1 + 1);

                double sum = integral[x2 + 1, y2 + 1] - integral[x1, y2 + 1]
                           - integral[x2 + 1, y1] + integral[x1, y1];
                double mean = sum / count;

                float val = px[x + y * w].grayscale;
                float bin = val > mean * (1.0f - adaptiveBias) ? 1f : 0f;
                result[x + y * w] = new Color(bin, bin, bin);
            }
        return MakeTexture(result, w, h);
    }

    Texture2D ApplyThresholdBinary(Texture2D input)
    {
        int w = input.width, h = input.height;
        Color[] px = input.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            float v = px[i].grayscale > 0.5f ? 1f : 0f;
            px[i] = new Color(v, v, v);
        }
        return MakeTexture(px, w, h);
    }

    Texture2D ApplyMedianFilter(Texture2D input, int r)
    {
        int w = input.width, h = input.height;
        Color[] src = input.GetPixels();
        Color[] dst = new Color[src.Length];
        List<float> window = new List<float>((2 * r + 1) * (2 * r + 1));

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                window.Clear();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, w - 1);
                        int ny = Mathf.Clamp(y + dy, 0, h - 1);
                        window.Add(src[nx + ny * w].grayscale);
                    }
                window.Sort();
                float med = window[window.Count / 2];
                dst[x + y * w] = new Color(med, med, med);
            }
        return MakeTexture(dst, w, h);
    }

    float[,] BuildEdgeMap(Texture2D grayTex, int w, int h)
    {
        Color[] px = grayTex.GetPixels();
        float[,] edge = new float[w, h];
        float maxVal = 0f;

        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                float gx = -px[(x - 1) + (y - 1) * w].grayscale - 2 * px[(x - 1) + y * w].grayscale - px[(x - 1) + (y + 1) * w].grayscale
                         + px[(x + 1) + (y - 1) * w].grayscale + 2 * px[(x + 1) + y * w].grayscale + px[(x + 1) + (y + 1) * w].grayscale;

                float gy = -px[(x - 1) + (y - 1) * w].grayscale - 2 * px[x + (y - 1) * w].grayscale - px[(x + 1) + (y - 1) * w].grayscale
                         + px[(x - 1) + (y + 1) * w].grayscale + 2 * px[x + (y + 1) * w].grayscale + px[(x + 1) + (y + 1) * w].grayscale;

                edge[x, y] = Mathf.Sqrt(gx * gx + gy * gy);
                if (edge[x, y] > maxVal) maxVal = edge[x, y];
            }

        if (maxVal > 0)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    edge[x, y] /= maxVal;

        return edge;
    }

    bool[,] MorphClose(bool[,] mask, int w, int h, int r)
    {
        mask = Dilate(mask, w, h, r);
        mask = Erode(mask, w, h, r);
        return mask;
    }

    bool[,] Dilate(bool[,] mask, int w, int h, int r)
    {
        bool[,] res = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[nx, ny])
                        {
                            res[x, y] = true;
                            goto next;
                        }
                    }
                res[x, y] = false;
            next:;
            }
        return res;
    }

    bool[,] Erode(bool[,] mask, int w, int h, int r)
    {
        bool[,] res = new bool[w, h];
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
                res[x, y] = all;
            }
        return res;
    }

    void Generate(int w, int h)
    {
        int found = 0, accepted = 0;

        for (int y = edgeMargin; y < h - edgeMargin; y++)
            for (int x = edgeMargin; x < w - edgeMargin; x++)
            {
                if (visited[x, y] || !buildingMask[x, y])
                {
                    visited[x, y] = true;
                    continue;
                }

                var island = FloodFill(x, y, w, h);
                found++;

                if (island.Count < minIslandSize || island.Count > maxIslandSize)
                    continue;

                var aabb = IslandAABB(island);
                float aspect = Mathf.Max(aabb.width, aabb.height) / (float)Mathf.Max(1, Mathf.Min(aabb.width, aabb.height));
                if (aspect > maxAspectRatio) continue;

                SpawnBuilding(island);
                accepted++;
            }

        Debug.Log($"Wyspy: znaleziono={found}, zaakceptowano={accepted}");
    }

    List<Vector2Int> FloodFill(int startX, int startY, int w, int h)
    {
        var result = new List<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            result.Add(cur);

            foreach (var d in dirs)
            {
                var n = cur + d;
                if (n.x < edgeMargin || n.x >= w - edgeMargin ||
                    n.y < edgeMargin || n.y >= h - edgeMargin) continue;
                if (visited[n.x, n.y] || !buildingMask[n.x, n.y]) continue;

                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return result;
    }

    RectInt IslandAABB(List<Vector2Int> island)
    {
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
        int safety = 0;

        while (remaining.Count >= minIslandSize && ++safety < 300)
        {
            var rect = FindBestRect(remaining);
            if (rect.width < 5 || rect.height < 5) break;

            SpawnRectBuilding(rect, material);

            for (int ry = rect.yMin; ry < rect.yMax; ry++)
                for (int rx = rect.xMin; rx < rect.xMax; rx++)
                    remaining.Remove(new Vector2Int(rx, ry));
        }
    }

    RectInt FindBestRect(HashSet<Vector2Int> pixelSet)
    {
        if (pixelSet.Count == 0) return new RectInt(0, 0, 0, 0);

        int xMin = int.MaxValue, xMax = int.MinValue, yMin = int.MaxValue, yMax = int.MinValue;
        foreach (var p in pixelSet)
        {
            xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
        }

        int width = xMax - xMin + 1;
        int height = yMax - yMin + 1;

        int[] histogram = new int[width];
        RectInt bestRect = new RectInt(xMin, yMin, 1, 1);
        float bestScore = -1f;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                var pt = new Vector2Int(xMin + col, yMin + row);
                histogram[col] = pixelSet.Contains(pt) ? histogram[col] + 1 : 0;
            }

            var rect = LargestRectInHistogram(histogram, xMin, yMin + row, width);
            if (rect.width < 5 || rect.height < 5) continue;

            int covered = 0;
            for (int ry = rect.yMin; ry < rect.yMax; ry++)
                for (int rx = rect.xMin; rx < rect.xMax; rx++)
                    if (pixelSet.Contains(new Vector2Int(rx, ry))) covered++;

            float density = covered / (float)(rect.width * rect.height);
            if (density < minFillDensity) continue;

            float squareness = Mathf.Min(rect.width, rect.height) / (float)Mathf.Max(rect.width, rect.height);
            float score = rect.width * rect.height * density * (0.5f + 0.5f * squareness * squareness);

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
                int barH = heights[stack.Pop()];
                int barW = stack.Count == 0 ? i : i - stack.Peek() - 1;
                int area = barH * barW;

                if (area > maxArea)
                {
                    maxArea = area;
                    int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                    best = new RectInt(xOffset + left, bottomRow - barH + 1, barW, barH);
                }
            }
            stack.Push(i);
        }
        return best;
    }

    void SpawnRectBuilding(RectInt rect, Material mat)
    {
        int pw = pixels.GetLength(0), ph = pixels.GetLength(1);
        float brightnessSum = 0f, edgeSum = 0f;
        int count = 0;

        for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                if (x < 0 || x >= pw || y < 0 || y >= ph) continue;
                brightnessSum += pixels[x, y].grayscale;
                edgeSum += edgeMap[x, y];
                count++;
            }

        if (count == 0) return;

        float avgBrightness = brightnessSum / count;
        float avgEdge = edgeSum / count;

        float height = Mathf.Lerp(minHeight, maxHeight, avgBrightness * brightnessHeightBoost)
                     + avgEdge * edgeHeightBoost
                     + Random.Range(-heightVariation, heightVariation);

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
        int pw = pixels.GetLength(0), ph = pixels.GetLength(1);
        float sum = 0f; int cnt = 0;

        foreach (var p in island)
        {
            if (p.x >= 0 && p.x < pw && p.y >= 0 && p.y < ph)
            {
                sum += pixels[p.x, p.y].grayscale;
                cnt++;
            }
        }

        float g = cnt > 0 ? sum / cnt : 0.5f;
        Color baseColor = new Color(g * 0.9f + 0.08f, g * 0.87f + 0.07f, g * 0.82f + 0.06f);

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = baseColor;
        mat.SetFloat("_Glossiness", 0.1f);
        mat.SetFloat("_Metallic", 0.05f);
        return mat;
    }

    Texture2D MakeTexture(Color[] px, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    void LogMaskStats(string label, int w, int h)
    {
        int cnt = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (buildingMask[x, y]) cnt++;
        Debug.Log($"[Maska] {label}: {cnt} px ({100f * cnt / (w * h):F1}%)");
    }
}