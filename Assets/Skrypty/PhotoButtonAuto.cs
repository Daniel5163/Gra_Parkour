using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PhotoButtonAuto : MonoBehaviour
{
    public Image imageAbove;

    public void OnClickSelect()
    {
        if (imageAbove == null || imageAbove.sprite == null)
        {
            Debug.LogError("Brak zdjêcia");
            return;
        }

        SelectedPhotoData.photo = imageAbove.sprite.texture;

        SceneManager.LoadScene("Mapa");
    }
}