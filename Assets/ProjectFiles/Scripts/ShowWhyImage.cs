using UnityEngine;

public class ShowWhyImage : MonoBehaviour
{
    [Header("Why UI Image")]
    public GameObject whyImage;

    private void Start()
    {
        if (whyImage != null)
            whyImage.SetActive(false); // Start me image hidden rahegi
    }

    // Is function ko Correct Button ke OnClick me assign karo
    public void ShowWhy()
    {
        if (whyImage != null)
            whyImage.SetActive(true);
    }
}