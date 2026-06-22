using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    public Button playbutton;
    public Button optionbutton;
    public Canvas menucanvas;
    
    public Canvas selectlevelcanvas;

    void Start()
    {
        menucanvas.gameObject.SetActive(true);
        selectlevelcanvas.gameObject.SetActive(false);
        playbutton.gameObject.SetActive(true);
        optionbutton.gameObject.SetActive(true);
       

        playbutton.onClick.AddListener(Play);
        optionbutton.onClick.AddListener(Options);
     
    }

   public void Play()
    {
        menucanvas.gameObject.SetActive(false);
        selectlevelcanvas.gameObject.SetActive(true);
    }

    void Options()
    {
        menucanvas.gameObject.SetActive(false);

        OptionsCanvas.Instance.Show(() =>
        {
            menucanvas.gameObject.SetActive(true);
        });
    }




}
