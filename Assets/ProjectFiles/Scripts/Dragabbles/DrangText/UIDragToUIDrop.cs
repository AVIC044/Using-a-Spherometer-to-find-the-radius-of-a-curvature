using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class UIDragToUIDrop : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Canvas")]
    public Canvas canvas;

    [Header("Correct Drop Target")]
    public RectTransform targetSlot; // The correct drop slot

    [Header("Objects To Enable On Correct Drop")]
    [Tooltip("These objects/images will be enabled when this item is dropped correctly.")]
    public GameObject[] enableOnCorrectDrop;

    [Header("Return Animation")]
    public float returnSpeed = 10f;

    [Header("Snap Sound")]
    public AudioSource audioSource;
    public AudioClip correctAudioClip;
    public AudioClip wrongAudioClip;

    [Header("Events")]
    public UnityEvent onCorrectDrop;

    // Public property
    public bool IsCorrect => droppedCorrectly;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalAnchoredPosition;
    private Transform originalParent;

    private bool droppedCorrectly = false;
    private bool hasCapturedOriginal = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalParent = rectTransform.parent;

        // Make sure assigned objects start disabled
        if (enableOnCorrectDrop != null)
        {
            foreach (GameObject obj in enableOnCorrectDrop)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (droppedCorrectly)
            return;

        if (!hasCapturedOriginal)
        {
            originalAnchoredPosition = rectTransform.anchoredPosition;
            hasCapturedOriginal = true;
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.8f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (droppedCorrectly)
            return;

        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (droppedCorrectly)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        GameObject hitObject = eventData.pointerCurrentRaycast.gameObject;

        bool isCorrect = hitObject != null &&
                         (hitObject.transform == targetSlot ||
                          hitObject.transform.IsChildOf(targetSlot));

        if (isCorrect)
        {
            droppedCorrectly = true;

            if (audioSource != null && correctAudioClip != null)
                audioSource.PlayOneShot(correctAudioClip);

            // Snap into the target slot
            rectTransform.SetParent(targetSlot, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // Disable further dragging
            canvasGroup.blocksRaycasts = false;

            // Enable assigned objects/images
            if (enableOnCorrectDrop != null)
            {
                foreach (GameObject obj in enableOnCorrectDrop)
                {
                    if (obj != null)
                        obj.SetActive(true);
                }
            }

            // Invoke additional events
            onCorrectDrop?.Invoke();
            return;
        }

        // Wrong drop
        if (audioSource != null && wrongAudioClip != null)
            audioSource.PlayOneShot(wrongAudioClip);

        StopAllCoroutines();
        StartCoroutine(ReturnToOrigin());
    }

    private IEnumerator ReturnToOrigin()
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, originalAnchoredPosition) > 0.1f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                originalAnchoredPosition,
                Time.deltaTime * returnSpeed);

            yield return null;
        }

        rectTransform.anchoredPosition = originalAnchoredPosition;
    }

    public void ResetDraggable()
    {
        droppedCorrectly = false;
        hasCapturedOriginal = false;

        gameObject.SetActive(true);

        rectTransform.SetParent(originalParent, false);

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // Hide assigned objects/images again
        if (enableOnCorrectDrop != null)
        {
            foreach (GameObject obj in enableOnCorrectDrop)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
    }
}