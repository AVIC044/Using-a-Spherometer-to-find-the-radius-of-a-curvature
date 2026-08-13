using UnityEngine;

public class ShowCanvasOnSpherometerHide : MonoBehaviour
{
    [Header("Canvas To Show")]
    [SerializeField] private GameObject canvasToShow;

    public void ShowCanvas()
    {
        if (canvasToShow == null)
        {
            Debug.LogWarning("Canvas To Show is not assigned!");
            return;
        }

        // Canvas ON
        canvasToShow.SetActive(true);

        Debug.Log("CANVAS SHOWN ON SCREEN");
    }
}