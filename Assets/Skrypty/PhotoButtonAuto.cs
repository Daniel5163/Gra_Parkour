using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PhotoButtonAuto : MonoBehaviour
{
    public Image imageAbove; 
    public Button button;    

    void Start()
    {
        if (button == null)
            button = GetComponent<Button>();

        button.onClick.AddListener(OnClickSelect);
    }

    public void OnClickSelect()
    {
        if (imageAbove == null || imageAbove.sprite == null)
        {
            Debug.LogError("Brak zdjêcia dla tego poziomu!");
            return;
        }

        SelectedPhotoData.selectedTexture = imageAbove.sprite.texture;

        SceneManager.LoadScene("Mapa");
    }
}