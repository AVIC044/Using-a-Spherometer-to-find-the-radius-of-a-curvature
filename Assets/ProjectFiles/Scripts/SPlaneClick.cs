using UnityEngine;

public class SPlaneClick : MonoBehaviour
{
    public GameObject whyButton;

    void Start()
    {
        whyButton.SetActive(false);
    }

    void OnMouseDown()
    {
        whyButton.SetActive(true);
    }
}