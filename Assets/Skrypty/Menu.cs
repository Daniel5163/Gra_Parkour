using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    public Button playbutton;
    public Button optionbutton;
    public Button selectbutton;
    public Button select1button;
    public Button select2button;
    public Button select3button;
    public Canvas menucanvas;
    public Canvas selectlevelcanvas;
    void Start()
    {
        menucanvas.gameObject.SetActive(true);
        selectlevelcanvas.gameObject.SetActive(false);
        playbutton.gameObject.SetActive(true);
        optionbutton.gameObject.SetActive(true);
       

        playbutton.onClick.AddListener(Play);
        optionbutton.onClick.AddListener(Option);
        selectbutton.onClick.AddListener(SelectLevel);
        select1button.onClick.AddListener(SelectLevel);
        select2button.onClick.AddListener(SelectLevel);
        select3button.onClick.AddListener(SelectLevel);
    }

   public void Play()
    {
        menucanvas.gameObject.SetActive(false);
        selectlevelcanvas.gameObject.SetActive(true);
    }

    public void Option()
    {

    }

    public void SelectLevel()
    {
        SceneManager.LoadScene("Mapa");
    }
}
