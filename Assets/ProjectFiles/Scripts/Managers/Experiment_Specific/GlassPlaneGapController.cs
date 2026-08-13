using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GlassPlaneGapController : MonoBehaviour
{
    public enum GlassGapResult
    {
        PassThrough,
        Blocked
    }

    [System.Serializable]
    public class GlassSlideData
    {
        [Header("Page Info")]
        [Tooltip("The page index where this glass/paper-gap sequence is active.")]
        public int pageIndex;

        [Header("Selectable Objects")]
        [Tooltip("Objects (like the glass plane or paper) allowed to interact on this slide.")]
        public List<GameObject> selectableObjects = new();

        [Header("Paper Position")]
        [Tooltip("The paper parent/anchor that is repositioned before the animation starts.")]
        public Transform paperParent;

        [Tooltip("Where the paper parent/anchor should be placed for the gap test.")]
        public Transform paperTestPosition;

        [Header("Gap Sequence Results")]
        [Tooltip("Result of the first test, before the adjustment.")]
        public GlassGapResult firstGapResult = GlassGapResult.PassThrough;

        [Tooltip("Result of the second test, after the adjustment.")]
        public GlassGapResult secondGapResult = GlassGapResult.Blocked;

        [Header("Slide Events")]
        public UnityEvent OnSlideCheckPassed;
    }

    [Header("Slide Configurations")]
    [SerializeField] private List<GlassSlideData> slideDataList = new();

    [Header("Paper Animation")]
    [Tooltip("Animator on the paper parent. The animated child is controlled by this Animator.")]
    [SerializeField] private Animator paperAnimator;

    [Tooltip("Trigger that plays the PassThrough animation.")]
    [SerializeField] private string passThroughTrigger = "PassThrough";

    [Tooltip("Trigger that plays the Blocked animation.")]
    [SerializeField] private string blockedTrigger = "Blocked";

    [Header("Paper Click Interaction")]
    [Tooltip("Collider used to detect a click on the paper/object.")]
    [SerializeField] private Collider paperInteractionCollider;

    [Tooltip("Camera used for clicks. If empty, Camera.main is used automatically.")]
    [SerializeField] private Camera mainCamera;

    [Header("Global Events")]
    [Tooltip("Invoked after the second test has completed.")]
    public UnityEvent OnFinalGapCheckPassed;

    private int currentPageIndex;
    private int testCount;
    private bool secondTestUnlocked;

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
            TryHandleClick(Input.mousePosition);
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
        testCount = 0;
        secondTestUnlocked = false;

        GlassSlideData data = GetCurrentSlideData();

        if (data == null)
        {
            Debug.Log($"[GlassPlane] Page {pageIndex}: no gap sequence configured.");
            return;
        }

        Debug.Log($"[GlassPlane] Page {pageIndex}: sequence ready. First = {data.firstGapResult}, Second = {data.secondGapResult}.");
    }

    private GlassSlideData GetCurrentSlideData()
    {
        return slideDataList.Find(x => x.pageIndex == currentPageIndex);
    }

    private void TryHandleClick(Vector3 screenPosition)
    {
        if (paperInteractionCollider == null)
            return;

        if (mainCamera == null)
        {
            Debug.LogWarning("[GlassPlane] Click ignored: no camera is available.");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != paperInteractionCollider)
                continue;

            Debug.Log($"[GlassPlane] Object clicked: {hit.collider.name}.");
            OnObjectClicked();
            return;
        }
    }

    public void OnObjectClicked()
    {
        GlassSlideData data = GetCurrentSlideData();

        if (data == null)
        {
            Debug.LogWarning($"[GlassPlane] Click ignored on page {currentPageIndex}: no sequence configured.");
            return;
        }

        if (testCount >= 2)
        {
            Debug.Log($"[GlassPlane] Page {currentPageIndex}: both tests are already complete.");
            return;
        }

        if (testCount == 1 && !secondTestUnlocked)
        {
            Debug.Log($"[GlassPlane] Page {currentPageIndex}: second test is locked until the adjustment is complete.");
            return;
        }

        if (data.paperParent == null)
        {
            Debug.LogWarning($"[GlassPlane] Page {currentPageIndex}: Paper Parent is not assigned.");
            return;
        }

        if (data.paperTestPosition == null)
        {
            Debug.LogWarning($"[GlassPlane] Page {currentPageIndex}: Paper Test Position is not assigned.");
            return;
        }

        data.paperParent.position = data.paperTestPosition.position;
        data.paperParent.rotation = data.paperTestPosition.rotation;

        GlassGapResult result = testCount == 0
            ? data.firstGapResult
            : data.secondGapResult;

        Debug.Log($"[GlassPlane] Page {currentPageIndex}: starting test #{testCount + 1} -> {result}.");

        testCount++;
        PlayAnimation(result);

        if (testCount == 1)
        {
            Debug.Log($"[GlassPlane] Page {currentPageIndex}: first test complete. Waiting for adjustment.");
        }
        else
        {
            Debug.Log($"[GlassPlane] Page {currentPageIndex}: second test complete. Sequence finished.");
            data.OnSlideCheckPassed?.Invoke();
            OnFinalGapCheckPassed?.Invoke();
            Debug.Log($"[GlassPlane] Page {currentPageIndex}: completion events invoked.");
        }
    }

    private void PlayAnimation(GlassGapResult result)
    {
        if (paperAnimator == null)
        {
            Debug.LogWarning("[GlassPlane] Cannot play animation: Animator is not assigned.");
            return;
        }

        switch (result)
        {
            case GlassGapResult.PassThrough:
                ResetSequenceTriggers();
                paperAnimator.SetTrigger(passThroughTrigger);
                Debug.Log($"[GlassPlane] Animation triggered: PassThrough ({passThroughTrigger}).");
                break;

            case GlassGapResult.Blocked:
                ResetSequenceTriggers();
                paperAnimator.SetTrigger(blockedTrigger);
                Debug.Log($"[GlassPlane] Animation triggered: Blocked ({blockedTrigger}).");
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
    /// Called externally after the adjustment screw is completed to unlock the second test.
    /// </summary>
    public void AllowSecondTest()
    {
        if (testCount != 1)
        {
            Debug.LogWarning($"[GlassPlane] Cannot unlock second test on page {currentPageIndex}: expected exactly one completed test, current count = {testCount}.");
            return;
        }

        secondTestUnlocked = true;
        Debug.Log($"[GlassPlane] Page {currentPageIndex}: second test unlocked after adjustment.");
    }

    /// <summary>
    /// Optional Inspector event target for unlocking page navigation after this sequence.
    /// </summary>
    public void UnlockPageNavigation()
    {
        Debug.Log($"[GlassPlane] Page {currentPageIndex}: requesting navigation unlock.");
        PageNavigationController.RequestNavigationUnlock();
    }
}