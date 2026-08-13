using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ComvexLensGapController : MonoBehaviour
{
    public enum PaperGapResult
    {
        PassThrough,
        Blocked
    }

    [System.Serializable]
    public class LensSlideData
    {
        [Header("Page Info")]
        [Tooltip("The page index where this paper-gap sequence is active.")]
        public int pageIndex;

        [Header("Paper Position")]
        [Tooltip("The FilterPaper parent/anchor that is repositioned before the paper animation starts.")]
        public Transform paperParent;

        [Tooltip("Where the FilterPaper parent/anchor should be placed for the gap test.")]
        public Transform paperTestPosition;

        [Header("Paper Gap Sequence")]
        [Tooltip("Result of the first paper test, before the spherometer is adjusted.")]
        public PaperGapResult firstPaperGapResult = PaperGapResult.PassThrough;

        [Tooltip("Result of the second paper test, after the spherometer adjustment.")]
        public PaperGapResult secondPaperGapResult = PaperGapResult.Blocked;

        [Header("Slide Events")]
        public UnityEvent OnSlideCheckPassed;
    }

    [Header("Slide Configurations")]
    [SerializeField] private List<LensSlideData> slideDataList = new();

    [Header("Paper Animation")]
    [Tooltip("Animator on the FilterPaper parent. The animated Paper child is controlled by this Animator.")]
    [SerializeField] private Animator paperAnimator;

    [Tooltip("Trigger that plays the paper PassThrough animation.")]
    [SerializeField] private string passThroughTrigger = "PassThrough";

    [Tooltip("Trigger that plays the paper Blocked animation.")]
    [SerializeField] private string blockedTrigger = "Blocked";

    [Header("Paper Click Interaction")]
    [Tooltip("Collider used to detect a click on the paper. Usually a BoxCollider on the FilterPaper parent.")]
    [SerializeField] private Collider paperInteractionCollider;

    [Tooltip("Camera used for paper clicks. If empty, Camera.main is used automatically.")]
    [SerializeField] private Camera mainCamera;

    [Header("Global Events")]
    [Tooltip("Invoked after the second paper test has completed.")]
    public UnityEvent OnFinalGapCheckPassed;

    private int currentPageIndex;
    private int paperTestCount;
    private bool secondPaperTestUnlocked;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryHandlePaperClick(Input.mousePosition);
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
        paperTestCount = 0;
        secondPaperTestUnlocked = false;

        LensSlideData data = GetCurrentSlideData();

        if (data == null)
        {
            Debug.Log($"[ConvexLens] Page {pageIndex}: no paper-gap sequence configured.");
            return;
        }

        Debug.Log($"[ConvexLens] Page {pageIndex}: sequence ready. First = {data.firstPaperGapResult}, Second = {data.secondPaperGapResult}.");
    }

    private LensSlideData GetCurrentSlideData()
    {
        return slideDataList.Find(x => x.pageIndex == currentPageIndex);
    }

    private void TryHandlePaperClick(Vector3 screenPosition)
    {
        if (paperInteractionCollider == null)
            return;

        if (mainCamera == null)
        {
            Debug.LogWarning("[ConvexLens] Paper click ignored: no camera is available.");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != paperInteractionCollider)
                continue;

            Debug.Log($"[ConvexLens] Paper clicked: {hit.collider.name}.");
            OnPaperClicked();
            return;
        }
    }

    public void OnPaperClicked()
    {
        LensSlideData data = GetCurrentSlideData();

        if (data == null)
        {
            Debug.LogWarning($"[ConvexLens] Paper click ignored on page {currentPageIndex}: no sequence configured.");
            return;
        }

        if (paperTestCount >= 2)
        {
            Debug.Log($"[ConvexLens] Page {currentPageIndex}: both paper tests are already complete.");
            return;
        }

        if (paperTestCount == 1 && !secondPaperTestUnlocked)
        {
            Debug.Log($"[ConvexLens] Page {currentPageIndex}: second paper test is locked until the spherometer adjustment is complete.");
            return;
        }

        if (data.paperParent == null)
        {
            Debug.LogWarning($"[ConvexLens] Page {currentPageIndex}: Paper Parent is not assigned.");
            return;
        }

        if (data.paperTestPosition == null)
        {
            Debug.LogWarning($"[ConvexLens] Page {currentPageIndex}: Paper Test Position is not assigned.");
            return;
        }

        data.paperParent.position = data.paperTestPosition.position;
        data.paperParent.rotation = data.paperTestPosition.rotation;

        PaperGapResult result = paperTestCount == 0
            ? data.firstPaperGapResult
            : data.secondPaperGapResult;

        Debug.Log($"[ConvexLens] Page {currentPageIndex}: starting paper test #{paperTestCount + 1} -> {result}.");

        paperTestCount++;
        PlayPaperAnimation(result);

        if (paperTestCount == 1)
        {
            Debug.Log($"[ConvexLens] Page {currentPageIndex}: first test complete. Waiting for spherometer adjustment.");
        }
        else
        {
            Debug.Log($"[ConvexLens] Page {currentPageIndex}: second test complete. Sequence finished.");
            data.OnSlideCheckPassed?.Invoke();
            OnFinalGapCheckPassed?.Invoke();
            Debug.Log($"[ConvexLens] Page {currentPageIndex}: completion events invoked.");
        }
    }

    private void PlayPaperAnimation(PaperGapResult result)
    {
        if (paperAnimator == null)
        {
            Debug.LogWarning("[ConvexLens] Cannot play paper animation: Paper Animator is not assigned.");
            return;
        }

        switch (result)
        {
            case PaperGapResult.PassThrough:
                ResetSequenceTriggers();
                paperAnimator.SetTrigger(passThroughTrigger);
                Debug.Log($"[ConvexLens] Paper animation triggered: PassThrough ({passThroughTrigger}).");
                break;

            case PaperGapResult.Blocked:
                ResetSequenceTriggers();
                paperAnimator.SetTrigger(blockedTrigger);
                Debug.Log($"[ConvexLens] Paper animation triggered: Blocked ({blockedTrigger}).");
                break;
        }
    }

    private void ResetSequenceTriggers()
    {
        if (!string.IsNullOrEmpty(passThroughTrigger))
            paperAnimator.ResetTrigger(passThroughTrigger);

        if (!string.IsNullOrEmpty(blockedTrigger))
            paperAnimator.ResetTrigger(blockedTrigger);
    }

    /// <summary>
    /// Called by SpherometerStepController.OnFinalSequenceCompleted after the screw adjustment.
    /// This unlocks the second paper test on the same page.
    /// </summary>
    public void AllowSecondPaperTest()
    {
        if (paperTestCount != 1)
        {
            Debug.LogWarning($"[ConvexLens] Cannot unlock second paper test on page {currentPageIndex}: expected exactly one completed paper test, current count = {paperTestCount}.");
            return;
        }

        secondPaperTestUnlocked = true;
        Debug.Log($"[ConvexLens] Page {currentPageIndex}: second paper test unlocked after spherometer adjustment.");
    }

    /// <summary>
    /// Optional Inspector event target for unlocking page navigation after this sequence.
    /// </summary>
    public void UnlockPageNavigation()
    {
        Debug.Log($"[ConvexLens] Page {currentPageIndex}: requesting navigation unlock.");
        PageNavigationController.RequestNavigationUnlock();
    }
}
