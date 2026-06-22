using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    public Canvas pausecanvas;
    public Canvas UIcanvas;
    public Button optionbutton;
    public Button resumebutton;
    public Button cancelbutton;
    public Button pausebutton;

    public GameObject optionsPrefab; 
    private GameObject optionsInstance;

    [Header("Skrypty do zatrzymania")]
    public Moving movingScript; 

    void Start()
    {
        UIcanvas.gameObject.SetActive(true);
        pausecanvas.gameObject.SetActive(false);

        optionbutton.onClick.AddListener(Options);
        resumebutton.onClick.AddListener(Resume);
        cancelbutton.onClick.AddListener(Exit);
        pausebutton.onClick.AddListener(LoadPause);
    }

    void Options()
    {
        pausecanvas.gameObject.SetActive(false);

        OptionsCanvas.Instance.Show(() =>
        {
            pausecanvas.gameObject.SetActive(true);
        });
    }

    void Resume()
    {
        if (optionsInstance != null)
            optionsInstance.SetActive(false);

        UIcanvas.gameObject.SetActive(true);
        pausecanvas.gameObject.SetActive(false);

        if (movingScript != null)
            movingScript.enabled = true;

        Time.timeScale = 1f; 
    }

    void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LoadPause()
    {
        UIcanvas.gameObject.SetActive(false);
        pausecanvas.gameObject.SetActive(true);

        if (movingScript != null)
            movingScript.enabled = false;

        Time.timeScale = 0f; 
    }
}