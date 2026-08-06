using UnityEngine;

public class PageCanvasHide : MonoBehaviour
{
    [Header("Canvas to Hide")]
    [SerializeField] private GameObject canvasToHide;

    [Header("Hide When Reaching This Page")]
    [Tooltip("Page number starts from 0. Example: Page 3 = Index 2")]
    [SerializeField] private int hideOnPageIndex = 2;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void OnPageChanged(int currentPage)
    {
        if (currentPage == hideOnPageIndex)
        {
            if (canvasToHide != null)
                canvasToHide.SetActive(false);
        }
    }
}