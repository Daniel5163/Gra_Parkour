using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class Scoring : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI trickText;

    [Header("Ustawienia Gry")]
    public int targetScore = 1000;
    private bool isGameOver = false;

    [Header("Punktacja - SZTYWNE WARTOŒCI")]
    public int backflipPoints = 20;
    public int rollPoints = 5;
    public int edgeClimbPoints = 20;
    public int edgeBonus = 20;

    [Header("Detekcja")]
    public float edgeDetectionRadius = 5f;
    public LayerMask buildingLayer = ~0;

    public int totalScore = 0;
    private Moving movementScript;

    void Start()
    {
        movementScript = GetComponent<Moving>();
        UpdateScoreUI();
    }

    public void RegisterTrick(string trickName)
    {
        if (isGameOver) return;

        int points = 0;
        if (trickName == "roll") points = rollPoints;
        else if (trickName == "backflip") points = backflipPoints;
        else if (trickName == "edge_climb") points = edgeClimbPoints;

        if (points > 0) AddScore(points, trickName.ToUpper());
    }

    public void RegisterLanding(Vector3 landingPos)
    {
        if (isGameOver) return;

        float dist = GetDistanceToNearestEdge(landingPos);
        if (dist <= 0.2f)
        {
            AddScore(edgeBonus, "PERFEKCYJNE L¥DOWANIE");
        }
    }

    void AddScore(int amount, string reason)
    {
        totalScore += amount;
        UpdateScoreUI();
        ShowTrickMessage($"+{amount} {reason}", Color.white);

        if (totalScore >= targetScore)
        {
            GameOver();
        }
    }
    void GameOver()
    {
        isGameOver = true;
        if (scoreText != null) scoreText.text = "CEL OSI¥GNIÊTY: " + totalScore;
        Debug.Log("Gra zakoñczona! Osi¹gniêto 1000 pkt.");
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = isGameOver ? "WYNIK: " + totalScore : $"Wynik: {totalScore} / {targetScore}";
    }

    void ShowTrickMessage(string msg, Color color)
    {
        if (trickText == null) return;
        trickText.text = msg;
        CancelInvoke(nameof(HideTrickText));
        Invoke(nameof(HideTrickText), 1.5f);
    }

    void HideTrickText() { if (trickText != null) trickText.text = ""; }

    public float GetDistanceToNearestEdge(Vector3 position)
    {
        float minDist = 999f;
        Collider[] colliders = Physics.OverlapSphere(position, edgeDetectionRadius, buildingLayer);

        foreach (Collider col in colliders)
        {
            Bounds b = col.bounds;
            float dist = Mathf.Min(Mathf.Abs(position.x - b.min.x), Mathf.Abs(position.x - b.max.x),
                                   Mathf.Abs(position.z - b.min.z), Mathf.Abs(position.z - b.max.z));
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
}