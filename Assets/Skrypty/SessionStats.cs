using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

[Serializable]
public class AllTimeStats
{
    public float longestJumpEver = 0f;
    public int highestScoreEver = 0;
    public int mostBuildingsVisited = 0;
    public int mostTricksInSession = 0;
    public int totalSessionsPlayed = 0;
    public float totalPlaytimeSeconds = 0f;

    public int totalBackflips = 0;
    public int totalRolls = 0;
    public int totalEdgeClimbs = 0;
    public int totalPerfectLandings = 0;
}

[Serializable]
public class SessionData
{
    public string sessionDate = "";
    public int score = 0;
    public float longestJump = 0f;
    public int buildingsVisited = 0;
    public int backflips = 0;
    public int rolls = 0;
    public int edgeClimbs = 0;
    public int perfectLandings = 0;
    public float playtimeSeconds = 0f;
    public int maxCombo = 0;
}

public class SessionStats : MonoBehaviour
{
    [Header("Próg punktów")]
    public int scoreThreshold = 1000;
    public bool showOncePerThreshold = true;

    [Header("UI - Canvas statystyk")]
    public Canvas statsCanvas;
    public float autoHideAfterSeconds = 0f;
    public Canvas UIcanvas;

    [Header("UI - Scroll View")]
    public GameObject rowPrefab;
    public Transform contentParent;

    [Header("UI - Przycisk zamknięcia panelu")]
    public Button btnClose;

    [Header("Zapis danych")]
    public string saveFileName = "parkour_stats.json";

    private Scoring scoringSystem;
    private Moving movingScript;

    private SessionData session = new SessionData();
    private AllTimeStats allTime = new AllTimeStats();

    private float sessionStartTime;
    private bool panelShownForCurrentThreshold = false;
    private int lastThresholdReached = 0;

    private HashSet<int> visitedBuildingIDs = new HashSet<int>();

    private bool trackingJump = false;
    private Vector3 jumpOrigin = Vector3.zero;
    private bool wasGroundedPrev = true;

    void Awake()
    {
        scoringSystem = GetComponent<Scoring>();
        movingScript = GetComponent<Moving>();
    }

    void Start()
    {
        LoadStats();

        session = new SessionData();
        session.sessionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        sessionStartTime = Time.time;

        if (statsCanvas != null)
            statsCanvas.gameObject.SetActive(false);

        if (UIcanvas != null)
            UIcanvas.gameObject.SetActive(true);

        if (btnClose != null)
            btnClose.onClick.AddListener(HidePanel);

        if (movingScript != null)
            movingScript.OnTrickPerformed += OnTrickPerformed;

        Debug.Log($"[SessionStats] Start. Plik: {SaveFilePath}");
    }

    void Update()
    {
        if (scoringSystem == null || movingScript == null) return;

        session.playtimeSeconds = Time.time - sessionStartTime;
        CheckScoreThreshold(scoringSystem.totalScore);
        TrackJumpsAndBuildings();
    }

    void OnDestroy()
    {
        if (movingScript != null)
            movingScript.OnTrickPerformed -= OnTrickPerformed;

        FinalizeSession();
        SaveStats();
    }

    void OnApplicationQuit()
    {
        FinalizeSession();
        SaveStats();
    }

    void TrackJumpsAndBuildings()
    {
        bool isGrounded = movingScript.IsGrounded();

        if (wasGroundedPrev && !isGrounded)
        {
            jumpOrigin = transform.position;
            trackingJump = true;
        }

        if (!wasGroundedPrev && isGrounded)
        {
            if (trackingJump)
            {
                float horizontal = Vector3.Distance(
                    new Vector3(jumpOrigin.x, 0, jumpOrigin.z),
                    new Vector3(transform.position.x, 0, transform.position.z)
                );
                if (horizontal > session.longestJump)
                    session.longestJump = horizontal;
                trackingJump = false;
            }
            CheckBuildingUnderFeet();
        }

        wasGroundedPrev = isGrounded;
    }

    void CheckBuildingUnderFeet()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 1.5f))
        {
            GameObject go = hit.collider.gameObject;
            if (go.CompareTag("Building") || go.name.Contains("Cube"))
            {
                int id = go.GetInstanceID();
                if (!visitedBuildingIDs.Contains(id))
                {
                    visitedBuildingIDs.Add(id);
                    session.buildingsVisited = visitedBuildingIDs.Count;
                }
            }
        }
    }

    void OnTrickPerformed(string trickName)
    {
        switch (trickName)
        {
            case "backflip": session.backflips++; break;
            case "roll": session.rolls++; break;
            case "edge_climb": session.edgeClimbs++; break;
        }
    }

    public void RegisterPerfectLanding() => session.perfectLandings++;

    void CheckScoreThreshold(int currentScore)
    {
        int currentThreshold = (currentScore / scoreThreshold) * scoreThreshold;

        if (currentThreshold > lastThresholdReached)
        {
            lastThresholdReached = currentThreshold;
            panelShownForCurrentThreshold = false;
        }

        if (!panelShownForCurrentThreshold && currentScore >= lastThresholdReached && lastThresholdReached > 0)
        {
            panelShownForCurrentThreshold = true;
            ShowPanel();
        }
    }

    public void ShowPanel()
    {
        session.score = scoringSystem != null ? scoringSystem.totalScore : 0;

        if (statsCanvas != null)
            statsCanvas.gameObject.SetActive(true);

        if (UIcanvas != null)
            UIcanvas.gameObject.SetActive(false);

        BuildStatsRows();

        if (autoHideAfterSeconds > 0f)
            StartCoroutine(AutoHide(autoHideAfterSeconds));

        Debug.Log($"[SessionStats] Panel wyświetlony przy {session.score} pkt");

        Cursor.lockState = CursorLockMode.None;
    }

    public void HidePanel()
    {
        if (statsCanvas != null)
            statsCanvas.gameObject.SetActive(false);

        SceneManager.LoadScene("Menu");
    }

    void BuildStatsRows()
    {
        if (rowPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[SessionStats] Brak rowPrefab lub contentParent!");
            return;
        }

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        float playtime = session.playtimeSeconds;
        string playtimeStr = $"{Mathf.FloorToInt(playtime / 60f):00}:{Mathf.FloorToInt(playtime % 60f):00}";

        float allTimePlaytime = allTime.totalPlaytimeSeconds + session.playtimeSeconds;
        string allTimePlaytimeStr = $"{Mathf.FloorToInt(allTimePlaytime / 3600f)}h {Mathf.FloorToInt((allTimePlaytime % 3600f) / 60f):00}m";

        float bestJump = Mathf.Max(allTime.longestJumpEver, session.longestJump);
        int bestScore = Mathf.Max(allTime.highestScoreEver, session.score);
        int bestBuildings = Mathf.Max(allTime.mostBuildingsVisited, session.buildingsVisited);

        var lines = new List<(string label, string value)>
        {
            ($"{session.sessionDate} ", ""),

            ("Wynik",                    $"{session.score}"),
            ("Najdłuższy skok",          $"{session.longestJump:F1} m"),
            ("Odwiedzone budynki",        $"{session.buildingsVisited}"),
            ("Backflip",                 $"{session.backflips}"),
            ("Roll",                     $"{session.rolls}"),
            ("Edge Climb",               $"{session.edgeClimbs}"),
            ("Perfekcyjne lądowania",    $"{session.perfectLandings}"),
            ("Maks. combo",              $"{session.maxCombo}"),
            ("Czas gry",                 playtimeStr),

            ("Najlepszy wynik", ""),

            ("Rekord wyniku",            $"{bestScore}"),
            ("Rekord skoku",             $"{bestJump:F1} m"),
            ("Rekord budynków",          $"{bestBuildings}"),
            ("Sesji rozegranych",        $"{allTime.totalSessionsPlayed + 1}"),
            ("Łączny czas",              allTimePlaytimeStr),
        };

        foreach (var (label, value) in lines)
        {
            GameObject row = Instantiate(rowPrefab, contentParent);

            TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

            if (value == "")
            {
                if (texts.Length > 0)
                    texts[0].text = label;
            }
            else if (texts.Length >= 2)
            {
                texts[0].text = label;
                texts[1].text = value;
            }
            else if (texts.Length == 1)
            {
                texts[0].text = $"{label}:  {value}";
            }
        }
    }

    IEnumerator AutoHide(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePanel();
    }

    string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    [Serializable]
    private class SaveFile
    {
        public AllTimeStats allTime = new AllTimeStats();
        public List<SessionData> sessions = new List<SessionData>();
    }

    void LoadStats()
    {
        string path = SaveFilePath;
        if (!File.Exists(path))
        {
            allTime = new AllTimeStats();
            Debug.Log("[SessionStats] Brak pliku zapisu — tworzę nowy.");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var save = JsonUtility.FromJson<SaveFile>(json);
            allTime = save.allTime ?? new AllTimeStats();
            Debug.Log($"[SessionStats] Załadowano. Rekord: {allTime.highestScoreEver} pkt");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionStats] Błąd wczytywania: {e.Message}");
            allTime = new AllTimeStats();
        }
    }

    void FinalizeSession()
    {
        session.score = scoringSystem != null ? scoringSystem.totalScore : session.score;
        session.playtimeSeconds = Time.time - sessionStartTime;

        if (session.score > allTime.highestScoreEver) allTime.highestScoreEver = session.score;
        if (session.longestJump > allTime.longestJumpEver) allTime.longestJumpEver = session.longestJump;
        if (session.buildingsVisited > allTime.mostBuildingsVisited) allTime.mostBuildingsVisited = session.buildingsVisited;

        int totalTricks = session.backflips + session.rolls + session.edgeClimbs;
        if (totalTricks > allTime.mostTricksInSession) allTime.mostTricksInSession = totalTricks;

        allTime.totalSessionsPlayed++;
        allTime.totalPlaytimeSeconds += session.playtimeSeconds;
        allTime.totalBackflips += session.backflips;
        allTime.totalRolls += session.rolls;
        allTime.totalEdgeClimbs += session.edgeClimbs;
        allTime.totalPerfectLandings += session.perfectLandings;
    }

    void SaveStats()
    {
        string path = SaveFilePath;

        SaveFile save = new SaveFile();
        if (File.Exists(path))
        {
            try
            {
                string existing = File.ReadAllText(path);
                save = JsonUtility.FromJson<SaveFile>(existing) ?? new SaveFile();
            }
            catch { save = new SaveFile(); }
        }

        save.allTime = allTime;
        save.sessions.Add(session);
        if (save.sessions.Count > 50)
            save.sessions.RemoveAt(0);

        try
        {
            string json = JsonUtility.ToJson(save, prettyPrint: true);
            File.WriteAllText(path, json);
            Debug.Log($"[SessionStats] Zapisano: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionStats] Błąd zapisu: {e.Message}");
        }
    }

    public void ForceSave()
    {
        FinalizeSession();
        SaveStats();
    }

    public void ReportCombo(int combo)
    {
        if (combo > session.maxCombo)
            session.maxCombo = combo;
    }
}