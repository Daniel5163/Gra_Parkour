using UnityEngine;
using TMPro;

public class Scoring : MonoBehaviour
{
    [Header("System Punktacji")]
    public int totalScore = 0;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI lastLandingBonusText;

    [Header("Detekcja Krawêdzi")]
    public float edgeDetectionRadius = 5f;
    public LayerMask buildingLayer = ~0; 

    [Header("System Combo")]
    public int currentCombo = 0;
    public float comboTimeWindow = 3f;
    public float comboMultiplier = 1f;
    public TextMeshProUGUI comboText;

    [Header("Efekty")]
    public ParticleSystem perfectLandParticles;
    public AudioSource landingSound;
    public AudioClip[] landingClips;

    private Moving movementScript;
    private Vector3 lastLandingPosition;
    private float lastLandingTime;
    private float currentComboTimer;
    private bool wasGrounded = true;

    void Start()
    {
        movementScript = GetComponent<Moving>();
       

        UpdateScoreUI();
        UpdateComboUI();
    }

    void Update()
    {
        if (movementScript == null) return;

        bool isGrounded = movementScript.IsGrounded();

        if (!wasGrounded && isGrounded)
        {
            OnPlayerLanded(transform.position);
        }

        wasGrounded = isGrounded;

        if (currentCombo > 0)
        {
            currentComboTimer -= Time.deltaTime;
            if (currentComboTimer <= 0)
            {
                ResetCombo();
            }
            UpdateComboUI();
        }
    }

    public void OnPlayerLanded(Vector3 landingPosition)
    {
        CalculateLandingBonusAtPosition(landingPosition);
    }

    public void CalculateLandingBonusAtPosition(Vector3 position)
    {

        float distanceToEdge = GetDistanceToNearestEdgeFromPosition(position);
      

        int bonus = CalculateBonusFromDistance(distanceToEdge);
      

        if (bonus > 0)
        {
            HandleCombo();

            int finalPoints = Mathf.RoundToInt(bonus * comboMultiplier);

            if (currentCombo > 1)
            {
                int comboBonus = Mathf.RoundToInt(50 * comboMultiplier);
                finalPoints += comboBonus;
            }

            AddScore(finalPoints);
            ShowLandingBonus(finalPoints, distanceToEdge, bonus);
            PlayLandingEffect(bonus);
        }
        else
        {
            ResetCombo();
        }

        lastLandingPosition = position;
    }

    private float GetDistanceToNearestEdgeFromPosition(Vector3 position)
    {
        float minDistance = float.MaxValue;

        Collider[] nearbyColliders = Physics.OverlapSphere(position, edgeDetectionRadius, buildingLayer);
     

        foreach (Collider col in nearbyColliders)
        {
            if (col.gameObject.name.Contains("Cube") || col.gameObject.CompareTag("Building"))
            {
                float distance = GetDistanceToBuildingEdgeFromPosition(col, position);
             
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
        }

        return minDistance == float.MaxValue ? 999f : minDistance;
    }

    private float GetDistanceToBuildingEdgeFromPosition(Collider building, Vector3 playerPosition)
    {
        Bounds bounds = building.bounds;

        float distanceToXEdge = Mathf.Min(
            Mathf.Abs(playerPosition.x - bounds.min.x),
            Mathf.Abs(playerPosition.x - bounds.max.x)
        );

        float distanceToZEdge = Mathf.Min(
            Mathf.Abs(playerPosition.z - bounds.min.z),
            Mathf.Abs(playerPosition.z - bounds.max.z)
        );

        return Mathf.Min(distanceToXEdge, distanceToZEdge);
    }

    private int CalculateBonusFromDistance(float distanceToEdge)
    {
        if (distanceToEdge <= 0.1f) return 500;
        if (distanceToEdge <= 0.3f) return 300;
        if (distanceToEdge <= 0.6f) return 150;
        if (distanceToEdge <= 1.0f) return 50;
        return 0;
    }

    private void HandleCombo()
    {
        float timeSinceLastLanding = Time.time - lastLandingTime;

        if (timeSinceLastLanding <= comboTimeWindow && lastLandingTime > 0)
        {
            currentCombo++;
            currentComboTimer = comboTimeWindow;
            comboMultiplier = 1f + (currentCombo * 0.1f);
            comboMultiplier = Mathf.Min(comboMultiplier, 3f);
        }
        else
        {
            if (timeSinceLastLanding > comboTimeWindow)
            {
                currentCombo = 1;
            }
            else
            {
                currentCombo++;
            }
            currentComboTimer = comboTimeWindow;
            comboMultiplier = 1f + (currentCombo * 0.1f);
            comboMultiplier = Mathf.Min(comboMultiplier, 3f);
        }

        lastLandingTime = Time.time;
    }

    private void ResetCombo()
    {
        currentCombo = 0;
        comboMultiplier = 1f;
        currentComboTimer = 0f;
        if (comboText != null) comboText.text = "";
    }

    private void AddScore(int points)
    {
        totalScore += points;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Wynik: {totalScore}";
        }
    }

    private void UpdateComboUI()
    {
        if (comboText != null && currentCombo > 1)
        {
            comboText.text = $"COMBO x{currentCombo}\n{comboMultiplier:F1}x MNO¯NIK";
        }
        else if (comboText != null)
        {
            comboText.text = "";
        }
    }

    private void ShowLandingBonus(int finalPoints, float distanceToEdge, int baseBonus)
    {
        if (lastLandingBonusText != null)
        {
            string qualityText = "";

            if (baseBonus >= 500)
            {
                qualityText = "PERFEKCJA!";
                lastLandingBonusText.color = Color.yellow;
            }
            else if (baseBonus >= 300)
            {
                qualityText = "DOSKONALE!";
                lastLandingBonusText.color = new Color(1f, 0.5f, 0f);
            }
            else if (baseBonus >= 150)
            {
                qualityText = "DOBRZE!";
                lastLandingBonusText.color = Color.green;
            }
            else
            {
                qualityText = "NIELE";
                lastLandingBonusText.color = Color.cyan;
            }

            string comboTextBonus = "";
            if (currentCombo > 1)
            {
                comboTextBonus = $"\nCOMBO x{currentCombo} (+{Mathf.RoundToInt(50 * comboMultiplier)})";
            }

            lastLandingBonusText.text = $"+{finalPoints} {qualityText}{comboTextBonus}\nOdleg³oœæ: {distanceToEdge:F2}m";

            CancelInvoke(nameof(HideBonusText));
            Invoke(nameof(HideBonusText), 2f);
        }
    }

    private void HideBonusText()
    {
        if (lastLandingBonusText != null)
        {
            lastLandingBonusText.text = "";
        }
    }

    private void PlayLandingEffect(int bonus)
    {
        if (perfectLandParticles != null && bonus >= 300)
        {
            perfectLandParticles.Play();
        }

        if (landingSound != null && landingClips != null && landingClips.Length > 0)
        {
            int clipIndex = 0;
            if (bonus >= 500) clipIndex = 2;
            else if (bonus >= 300) clipIndex = 1;
            else clipIndex = 0;

            clipIndex = Mathf.Clamp(clipIndex, 0, landingClips.Length - 1);
            landingSound.PlayOneShot(landingClips[clipIndex]);
        }
    }
}