using UnityEngine;
using System.Collections;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Gracz")]
    public GameObject playerPrefab;
    public float spawnDistance = 8f;
    public int maxAttempts = 200;

    [Header("Warstwy")]
    public string buildingLayerName = "Building";
    public string playerLayerName = "Player";

    private ParkourGraph graph;
    private int buildingLayer;
    private int playerLayer;

    void Start()
    {
        graph = GetComponent<ParkourGraph>();
        if (graph == null)
            graph = FindObjectOfType<ParkourGraph>();

        buildingLayer = LayerMask.NameToLayer(buildingLayerName);
        playerLayer = LayerMask.NameToLayer(playerLayerName);

        StartCoroutine(SpawnAfterDelay());
    }

    IEnumerator SpawnAfterDelay()
    {
        yield return null;
        yield return null;
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: brak prefaba gracza!");
            return;
        }

        if (graph == null || graph.roofPoints.Count == 0)
        {
            Debug.LogWarning("PlayerSpawner: brak punktów budynków, spawning na 0,2,0");
            InstantiatePlayer(new Vector3(0f, 2f, 0f));
            return;
        }

        Vector3 pos = FindSpawnPosition();

        if (pos == Vector3.zero)
        {
            pos = graph.roofPoints[0];
            pos.y = 0f;
            pos.x += spawnDistance;
            Debug.LogWarning($"PlayerSpawner: fallback spawn przy pierwszym budynku: {pos}");
        }

        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 200f, Vector3.down, out hit, 400f))
            pos.y = hit.point.y + 1f;
        else
            pos.y = 1f;

        InstantiatePlayer(pos);
    }

    void InstantiatePlayer(Vector3 pos)
    {
        GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
        player.tag = "Player";

        if (playerLayer >= 0)
        {
            player.layer = playerLayer;
            foreach (Transform child in player.transform)
                child.gameObject.layer = playerLayer;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 scale = player.transform.lossyScale;
            float scaledHeight = cc.height * scale.y;
            float scaledRadius = cc.radius * Mathf.Max(scale.x, scale.z);
            float maxStep = scaledHeight + scaledRadius * 2f - 0.001f;

            if (cc.stepOffset > maxStep)
            {
                Debug.LogWarning($"PlayerSpawner: stepOffset ({cc.stepOffset}) za du¿y, ustawiam na {maxStep:F3}");
                cc.stepOffset = maxStep;
            }
        }

        Debug.Log($"PlayerSpawner: gracz spawniêty na {pos}");
    }

    Vector3 FindSpawnPosition()
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int idx = Random.Range(0, graph.roofPoints.Count);
            Vector3 roof = graph.roofPoints[idx];

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = spawnDistance + attempt * 0.3f;

            Vector3 candidate = new Vector3(
                roof.x + Mathf.Cos(angle) * dist,
                0f,
                roof.z + Mathf.Sin(angle) * dist
            );

            if (IsValid(candidate))
                return candidate;
        }

        Vector3 center = Vector3.zero;
        foreach (var p in graph.roofPoints) center += p;
        center /= graph.roofPoints.Count;
        center.y = 0f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = spawnDistance + attempt * 0.5f;

            Vector3 candidate = new Vector3(
                center.x + Mathf.Cos(angle) * dist,
                0f,
                center.z + Mathf.Sin(angle) * dist
            );

            if (IsValid(candidate))
                return candidate;
        }

        return Vector3.zero;
    }

    bool IsValid(Vector3 pos)
    {
        if (buildingLayer >= 0)
        {
            int mask = 1 << buildingLayer;
            if (Physics.OverlapSphere(pos, 3f, mask).Length > 0)
                return false;
        }

        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out hit, 200f))
            return false;

        if (buildingLayer >= 0 && hit.collider.gameObject.layer == buildingLayer)
            return false;

        if (hit.normal.y < 0.866f)
            return false;

        if (buildingLayer >= 0)
        {
            int mask = 1 << buildingLayer;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.up, 8f, mask))
                return false;
        }

        return true;
    }

    void Update()
    {
            if (Input.GetKeyDown(KeyCode.F5))  
            {
                GameObject existing = GameObject.FindGameObjectWithTag("Player");
                if (existing != null) Destroy(existing);
                StartCoroutine(SpawnAfterDelay());
            }
        
    }
}