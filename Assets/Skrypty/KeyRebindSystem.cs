using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class KeyRebindSystem : MonoBehaviour
{
    [System.Serializable]
    public class ControlEntry
    {
        public string actionName;
        public string defaultKey;
        public KeyCode currentKey;
        public bool isDoubleTap; 
    }

    [Header("Konfiguracja")]
    public bool enableKeyRebind = true;
    public float doubleTapThreshold = 0.3f; 

    private static ControlEntry[] staticControls = new ControlEntry[]
    {
        new ControlEntry { actionName = "Chód w przód", defaultKey = "W", isDoubleTap = false },
        new ControlEntry { actionName = "Chód w ty³", defaultKey = "S", isDoubleTap = false },
        new ControlEntry { actionName = "Chód w lewo", defaultKey = "A", isDoubleTap = false },
        new ControlEntry { actionName = "Chód w prawo", defaultKey = "D", isDoubleTap = false },
        new ControlEntry { actionName = "Bieg", defaultKey = "LeftShift", isDoubleTap = false },
        new ControlEntry { actionName = "Skok", defaultKey = "Space", isDoubleTap = false },
        new ControlEntry { actionName = "Backflip", defaultKey = "Space", isDoubleTap = true }, 
        new ControlEntry { actionName = "Roll", defaultKey = "R", isDoubleTap = false },
        new ControlEntry { actionName = "Kucniêcie", defaultKey = "LeftControl", isDoubleTap = false },
        new ControlEntry { actionName = "Wspinaczka", defaultKey = "C", isDoubleTap = false },
    };

    private static bool keysLoaded = false;
    public static event Action OnKeysChanged;

    [Header("UI Referencje")]
    public GameObject rowPrefab;
    public Transform contentParent;
    public Button resetButton;

    private List<GameObject> rows = new List<GameObject>();
    private List<Button> keyButtons = new List<Button>();
    private List<TextMeshProUGUI> keyTexts = new List<TextMeshProUGUI>();
    private int currentlyRebinding = -1;
    private bool isRebinding = false;

    private KeyCode lastKeyPressed = KeyCode.None;
    private float lastKeyTime = 0f;

    void Awake()
    {
        if (!keysLoaded)
        {
            LoadKeys();
            keysLoaded = true;
        }
    }

    void Start()
    {
        BuildUI();

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToDefaultKeys);
    }

    void Update()
    {
        if (isRebinding && currentlyRebinding >= 0)
            ProcessRebind();
    }

    void ProcessRebind()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelRebind();
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (key >= KeyCode.Mouse0) continue;

            if (Input.GetKeyDown(key))
            {
                float currentTime = Time.realtimeSinceStartup;

                if (key == lastKeyPressed && (currentTime - lastKeyTime) <= doubleTapThreshold)
                {
                    ApplyNewKey(key, true);
                    return;
                }
                else
                {
                    lastKeyPressed = key;
                    lastKeyTime = currentTime;

                    if (currentlyRebinding < keyTexts.Count)
                        keyTexts[currentlyRebinding].text = key.ToString() + "...?";
                }
            }
        }

        if (lastKeyPressed != KeyCode.None && (Time.realtimeSinceStartup - lastKeyTime) > doubleTapThreshold)
        {
            ApplyNewKey(lastKeyPressed, false);
        }
    }

    void ApplyNewKey(KeyCode key, bool isDouble)
    {
        bool alreadyUsed = false;

        for (int i = 0; i < staticControls.Length; i++)
        {
            if (i != currentlyRebinding &&
                staticControls[i].currentKey == key &&
                staticControls[i].isDoubleTap == isDouble)
            {
                alreadyUsed = true;
                string mode = isDouble ? "Double " : "";
                Debug.Log($"Kombinacja {mode}{key} jest ju¿ u¿ywana dla {staticControls[i].actionName}");
                break;
            }
        }

        if (!alreadyUsed)
        {
            staticControls[currentlyRebinding].currentKey = key;
            staticControls[currentlyRebinding].isDoubleTap = isDouble;
            SaveKeys();

            string mode = isDouble ? "Double " : "";
            Debug.Log($"Ustawiono {staticControls[currentlyRebinding].actionName} na {mode}{key}");

            CancelRebind();
            UpdateKeyNames();
        }
        else
        {
            lastKeyPressed = KeyCode.None;
            if (currentlyRebinding < keyTexts.Count)
                keyTexts[currentlyRebinding].text = "Naciœnij klawisz...";
        }
    }

    void LoadKeys()
    {
        staticControls[6].isDoubleTap = true; 

        for (int i = 0; i < staticControls.Length; i++)
        {
            string defaultKey = staticControls[i].defaultKey.Replace(" ", "");
            if (Enum.TryParse(defaultKey, true, out KeyCode defaultKc))
            {
                staticControls[i].currentKey = defaultKc;
                if (i != 6) staticControls[i].isDoubleTap = false; 
            }
        }

        for (int i = 0; i < staticControls.Length; i++)
        {
            string keyName = "Key_" + staticControls[i].actionName;
            string doubleTapName = "Double_" + staticControls[i].actionName;

            if (PlayerPrefs.HasKey(keyName))
            {
                try
                {
                    int savedKeyInt = PlayerPrefs.GetInt(keyName);

                    if (savedKeyInt != 0)
                    {
                        staticControls[i].currentKey = (KeyCode)savedKeyInt;
                        staticControls[i].isDoubleTap = PlayerPrefs.GetInt(doubleTapName, 0) == 1;
                    }
                }
                catch
                {
                    Debug.LogWarning($"B³¹d wczytywania klawisza dla {staticControls[i].actionName}");
                }
            }
        }
    }

    void SaveKeys()
    {
        for (int i = 0; i < staticControls.Length; i++)
        {
            string keyName = "Key_" + staticControls[i].actionName;
            string doubleTapName = "Double_" + staticControls[i].actionName;

            PlayerPrefs.SetInt(keyName, (int)staticControls[i].currentKey);
            PlayerPrefs.SetInt(doubleTapName, staticControls[i].isDoubleTap ? 1 : 0);
        }
        PlayerPrefs.Save();
        OnKeysChanged?.Invoke();
    }

    
    public static bool GetActionDown(string actionName)
    {
        foreach (var c in staticControls)
        {
            if (c.actionName == actionName)
            {
                if (c.isDoubleTap)
                {
                    return false;
                }
                return Input.GetKeyDown(c.currentKey);
            }
        }
        return false;
    }

    public static KeyCode GetKey(string actionName)
    {
        foreach (var c in staticControls)
        {
            if (c.actionName == actionName) return c.currentKey;
        }
        return KeyCode.None;
    }

    public static bool IsDoubleTapAction(string actionName)
    {
        foreach (var c in staticControls)
        {
            if (c.actionName == actionName) return c.isDoubleTap;
        }
        return false;
    }

    public static string GetFormattedKeyName(int index)
    {
        if (index < 0 || index >= staticControls.Length) return "None";
        string prefix = staticControls[index].isDoubleTap ? "Double " : "";
        return prefix + staticControls[index].currentKey.ToString();
    }

    public static void ResetToDefaultKeys()
    {
        staticControls[6].isDoubleTap = true; 

        for (int i = 0; i < staticControls.Length; i++)
        {
            string defaultKey = staticControls[i].defaultKey.Replace(" ", "");
            if (Enum.TryParse(defaultKey, true, out KeyCode defaultKc))
            {
                staticControls[i].currentKey = defaultKc;
                if (i != 6) staticControls[i].isDoubleTap = false;
            }
        }

        for (int i = 0; i < staticControls.Length; i++)
        {
            PlayerPrefs.SetInt("Key_" + staticControls[i].actionName, (int)staticControls[i].currentKey);
            PlayerPrefs.SetInt("Double_" + staticControls[i].actionName, staticControls[i].isDoubleTap ? 1 : 0);
        }
        PlayerPrefs.Save();
        OnKeysChanged?.Invoke();
    }

    public void StartRebind(int index)
    {
        if (!enableKeyRebind || index < 0 || index >= staticControls.Length) return;

        if (currentlyRebinding >= 0) CancelRebind();

        currentlyRebinding = index;
        isRebinding = true;
        lastKeyPressed = KeyCode.None; 

        if (index < keyButtons.Count)
        {
            Button btn = keyButtons[index];
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.yellow;
            btn.colors = colors;

            if (index < keyTexts.Count)
                keyTexts[index].text = "Naciœnij klawisz...";
        }
    }

    void CancelRebind()
    {
        isRebinding = false;
        lastKeyPressed = KeyCode.None;

        if (currentlyRebinding >= 0 && currentlyRebinding < keyButtons.Count)
        {
            Button btn = keyButtons[currentlyRebinding];
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            btn.colors = colors;
        }

        currentlyRebinding = -1;
        UpdateKeyNames();
    }

    void BuildUI()
    {
        if (rowPrefab == null || contentParent == null) return;

        foreach (Transform child in contentParent) Destroy(child.gameObject);

        rows.Clear();
        keyButtons.Clear();
        keyTexts.Clear();

        for (int i = 0; i < staticControls.Length; i++)
        {
            int index = i;
            GameObject row = Instantiate(rowPrefab, contentParent);
            rows.Add(row);

            Button btn = row.GetComponentInChildren<Button>();
            if (btn != null)
            {
                keyButtons.Add(btn);
                TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    keyTexts.Add(btnText);
                    btnText.text = GetFormattedKeyName(i); 
                }
                btn.onClick.AddListener(() => StartRebind(index));
            }

            TextMeshProUGUI[] allTexts = row.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var t in allTexts)
            {
                if (t.transform.parent != btn?.transform)
                {
                    t.text = staticControls[i].actionName;
                    break;
                }
            }
        }
    }

    void UpdateKeyNames()
    {
        if (currentlyRebinding >= 0) return;

        for (int i = 0; i < keyTexts.Count && i < staticControls.Length; i++)
        {
            keyTexts[i].text = GetFormattedKeyName(i);

            if (i < keyButtons.Count)
            {
                ColorBlock colors = keyButtons[i].colors;
                colors.normalColor = Color.white;
                keyButtons[i].colors = colors;
            }
        }
    }

    void OnDestroy()
    {
        foreach (var btn in keyButtons) if (btn != null) btn.onClick.RemoveAllListeners();
        if (resetButton != null) resetButton.onClick.RemoveAllListeners();
    }

    [ContextMenu("Clear All Saved Keys")]
    public void ClearAllSavedKeys()
    {
        for (int i = 0; i < staticControls.Length; i++)
        {
            string keyName = "Key_" + staticControls[i].actionName;
            string doubleTapName = "Double_" + staticControls[i].actionName;
            PlayerPrefs.DeleteKey(keyName);
            PlayerPrefs.DeleteKey(doubleTapName);
        }
        PlayerPrefs.Save();
        LoadKeys();
        UpdateKeyNames();
        Debug.Log("Wyczyszczono wszystkie zapisane klawisze!");
    }
}