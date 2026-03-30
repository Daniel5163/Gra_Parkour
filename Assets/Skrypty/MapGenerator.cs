using UnityEngine;

public class CityGenerator : MonoBehaviour
{
    [Header("Map")]
    public Texture2D mapTexture;

    [Header("World")]
    public Vector2 worldSize = new Vector2(25f, 25f);

    [Header("Buildings")]
    public float heightMultiplier = 10f;
    public float threshold = 0.5f;
    public int maxBuildings = 15;

    [Header("Ledges")]
    public float ledgeStep = 1.2f;

    [Header("Floor")]
    public bool createFloor = true;

    public Material[] buildingMaterials;

    void Start()
    {
        if (createFloor)
            CreateFloor();

        GenerateCity();
    }

    void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);

        floor.name = "Floor";

        floor.transform.position = new Vector3(
            worldSize.x / 2f,
            -0.5f,
            worldSize.y / 2f
        );

        floor.transform.localScale = new Vector3(
            worldSize.x,
            1f,
            worldSize.y
        );

        floor.GetComponent<Renderer>().material.color = Color.gray;
    }
    void GenerateCity()
    {
        int created = 0;
        int attempts = 0;

        while (created < maxBuildings && attempts < 2000)
        {
            attempts++;

            int x = Random.Range(0, mapTexture.width);
            int y = Random.Range(0, mapTexture.height);

            Color pixel = mapTexture.GetPixel(x, y);

            if (pixel.grayscale > threshold)
            {
                CreateBuilding(x, y);
                created++;
            }
        }
    }

    void CreateBuilding(int x, int y)
    {
        float normX = (float)x / mapTexture.width;
        float normY = (float)y / mapTexture.height;

        Vector3 basePos = new Vector3(
            normX * worldSize.x,
            0,
            normY * worldSize.y
        );

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);

        float height = Random.Range(3f, heightMultiplier);

        building.transform.localScale = new Vector3(1f, height, 1f);

        building.transform.position = basePos + Vector3.up * (height / 2f);

        building.transform.parent = transform;

        if (buildingMaterials.Length > 0)
        {
            building.GetComponent<Renderer>().material =
                buildingMaterials[Random.Range(0, buildingMaterials.Length)];
        }

        CreateLedges(building.transform.position, height, building.transform);
    }

    void CreateLedges(Vector3 buildingCenter, float height, Transform parent)
    {
        float stepY = ledgeStep;

        float baseY = buildingCenter.y - (height / 2f);

        int steps = Mathf.FloorToInt(height / stepY);

        for (int i = 0; i < steps; i++)
        {
            GameObject ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);

            float y = baseY + (i * stepY);

            float side = (i % 2 == 0) ? 0.7f : -0.7f;

            ledge.transform.position = new Vector3(
                buildingCenter.x + side,
                y,
                buildingCenter.z
            );

            ledge.transform.localScale = new Vector3(0.8f, 0.15f, 0.4f);

            ledge.transform.parent = parent;
        }
    }
}