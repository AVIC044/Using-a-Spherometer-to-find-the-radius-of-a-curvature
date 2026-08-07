using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIClickableImage : MonoBehaviour, IPointerClickHandler
{
    [Header("Image to Spawn")]
    public Image imageTemplate;

    [Header("Image Position Offset")]
    [Tooltip("X = Right/Left, Y = Up/Down")]
    public Vector2 imageOffset = new Vector2(120f, 0f);

    [Header("Correct / Wrong")]
    public bool isCorrect;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;

    [Header("Navigation")]
    [Tooltip("Enable Next Page when correct answer is clicked")]
    public bool enableNavigationOnCorrect = true;

    [Header("Why")]
    public GameObject whyObject;

    private bool hasClicked;
    private Image spawnedImage;

    private void Start()
    {
        if (whyObject != null)
            whyObject.SetActive(false);
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hasClicked)
            return;

        // Remove previous image if it exists
        if (spawnedImage != null)
        {
            Destroy(spawnedImage.gameObject);
        }

        // Spawn Image
        spawnedImage = Instantiate(imageTemplate, imageTemplate.transform.parent);
        spawnedImage.gameObject.SetActive(true);

        // Show image to the RIGHT of the clicked UI object
        RectTransform clickedRect = transform as RectTransform;
        RectTransform spawnedRect = spawnedImage.rectTransform;
        spawnedRect.position = clickedRect.position + (Vector3)imageOffset;

        // Show WHY only if correct
        if (isCorrect && whyObject != null)
        {
            whyObject.SetActive(true);
        }

        // Play Sound
        AudioClip clip = isCorrect ? correctSound : wrongSound;
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }

        // Enable navigation only on correct answer
        if (isCorrect && enableNavigationOnCorrect)
        {
            PageNavigationController.RequestNavigationUnlock();
        }

        hasClicked = true;
    }

    private void OnPageChanged(int pageIndex)
    {
        if (spawnedImage != null)
        {
            Destroy(spawnedImage.gameObject);
            spawnedImage = null;
        }

        if (whyObject != null)
        {
            whyObject.SetActive(false);
        }

        hasClicked = false;
    }
}