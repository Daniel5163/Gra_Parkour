using UnityEngine;
using UnityEngine.UI;

public class OptionsCanvas : MonoBehaviour
{
    public static OptionsCanvas Instance;
    public Button backButton;
    private System.Action onBack;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject); 
        }

        gameObject.SetActive(false); 
    }

    void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            onBack?.Invoke();
            gameObject.SetActive(false);
        });
    }

    public void Show(System.Action backAction)
    {
        onBack = backAction;
        gameObject.SetActive(true);
    }
}