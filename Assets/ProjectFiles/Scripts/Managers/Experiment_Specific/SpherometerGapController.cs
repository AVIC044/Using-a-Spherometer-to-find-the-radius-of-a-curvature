using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Unified spherometer gap detection controller.
/// Handles paper-gap tests for convex lenses, concave lenses, and glass planes.
/// </summary>
public class SpherometerGapController : MonoBehaviour
{
    public enum SurfaceType
    {
        ConvexLens,     // Curved outward: gap exists initially, closes after adjustment
        ConcaveLens,    // Curved inward: different gap behavior
        GlassPlane      // Flat: no gap, blocked immediately
    }

    public enum PaperTestResult
    {
        PassThrough,    // Paper slides through = gap exists
        Blocked         // Paper stops = no gap / gap closed
    }

    public enum PaperPosition
    {
        Start,          // Initial resting position
        TestPosition,   // Where paper is placed for gap test
        PassThroughEnd, // Where paper ends up when the gap test passes through (e.g. Filter_paper_Gap)
        BlockedEnd,     // Where paper ends up when blocked (e.g. Filter_paper_No_Gap)
        Blocked         // Legacy alias for the old blockedPosition field
    }

    [System.Serializable]
    public class SlideConfiguration
    {
        [Header("Page Info")]
        [Tooltip("The page index where this configuration is active.")]
        public int pageIndex;

        [Header("Surface Type")]
        [Tooltip("What kind of surface is being tested?")]
        public SurfaceType surfaceType = SurfaceType.ConvexLens;

        [Header("Paper Transforms")]
        [Tooltip("The paper object that will be animated/moved.")]
        public Transform paperObject;

        [Tooltip("Where the paper starts before any test.")]
        public Transform startPosition;

        [Tooltip("Where the paper is placed for the gap test (starting point of the test, before the result is known).")]
        public Transform testPosition;

        [Tooltip("Where the paper (FilterPaper) ends up when the test result is PassThrough — e.g. Filter_paper_Gap.")]
        public Transform passThroughEndPosition;

        [Tooltip("Where the paper (FilterPaper) ends up when the test result is Blocked — e.g. Filter_paper_No_Gap.")]
        public Transform blockedEndPosition;

        [Tooltip("Legacy fallback position, used only if passThroughEndPosition/blockedEndPosition are not assigned.")]
        public Transform blockedPosition;

        [Header("Test Sequence")]
        [Tooltip("Result of the first paper test (before spherometer adjustment).")]
        public PaperTestResult firstTestResult = PaperTestResult.PassThrough;

        [Tooltip("Result of the second paper test (after spherometer adjustment).")]
        public PaperTestResult secondTestResult = PaperTestResult.Blocked;

        [Tooltip("Does this slide require spherometer adjustment between tests?")]
        public bool requiresSpherometerAdjustment = true;

        [Header("Gap Detection Override (Optional)")]
        [Tooltip("If assigned, uses distance check instead of preset results.")]
        public Transform gapTarget;

        [Tooltip("Distance threshold for physics-based gap detection.")]
        public float gapThreshold = 0.05f;

        [Header("Events")]
        public UnityEvent OnFirstTestComplete;
        public UnityEvent OnSecondTestComplete;
        public UnityEvent OnSequenceComplete;
    }

    [Header("Slide Configurations")]
    [SerializeField] private List<SlideConfiguration> slideConfigurations = new();

    [Header("Animation")]
    [Tooltip("Animator controlling the paper animation.")]
    [SerializeField] private Animator paperAnimator;

    [Tooltip("Trigger name for PassThrough animation.")]
    [SerializeField] private string passThroughTrigger = "PassThrough";

    [Tooltip("Trigger name for Blocked animation.")]
    [SerializeField] private string blockedTrigger = "Blocked";

    [Tooltip("Name of the Animator state the paper animation returns to on reset (e.g. Default_State).")]
    [SerializeField] private string defaultStateName = "Default_State";

    [Header("Interaction")]
    [Tooltip("Collider used to detect clicks on the paper.")]
    [SerializeField] private Collider paperClickCollider;

    [Tooltip("Layer mask for paper raycast detection.")]
    [SerializeField] private LayerMask paperInteractionLayer;

    [Tooltip("Camera for raycasting. Falls back to Camera.main.")]
    [SerializeField] private Camera interactionCamera;

    [Header("Timing")]
    [Tooltip("Extra buffer time after animation before firing events.")]
    [SerializeField] private float animationBufferTime = 0.1f;

    [Header("Global Events")]
    [Tooltip("Invoked when ANY slide sequence completes.")]
    public UnityEvent OnAnySequenceComplete;

    // Runtime state
    private int currentPageIndex;
    private int testCount;
    private bool secondTestUnlocked;
    private bool waitingForPaperRemoval;
    private bool isProcessingTest;
    private SlideConfiguration currentConfig;
    private Dictionary<int, SlideConfiguration> configLookup;

    private void Awake()
    {
        BuildConfigLookup();
    }

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
        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (paperAnimator == null)
            paperAnimator = GetComponent<Animator>();

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryProcessPaperClick(Input.mousePosition);
    }

    #region Initialization

    private void BuildConfigLookup()
    {
        configLookup = new Dictionary<int, SlideConfiguration>();
        foreach (var config in slideConfigurations)
        {
            if (configLookup.ContainsKey(config.pageIndex))
            {
                Debug.LogWarning($"[SpherometerGap] Duplicate page index {config.pageIndex}. Using first entry.");
                continue;
            }
            configLookup[config.pageIndex] = config;
        }
    }

    private SlideConfiguration GetConfiguration(int pageIndex)
    {
        return configLookup.TryGetValue(pageIndex, out var config) ? config : null;
    }

    #endregion

    #region Page Management

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
        ResetSlideState();
        currentConfig = GetConfiguration(pageIndex);

        if (currentConfig == null)
        {
            Debug.Log($"[SpherometerGap] Page {pageIndex}: no configuration.");
            return;
        }

        ResetPaperToStart();
        Debug.Log($"[SpherometerGap] Page {pageIndex}: {currentConfig.surfaceType} ready. " +
                  $"First={currentConfig.firstTestResult}, Second={currentConfig.secondTestResult}");
    }

    private void ResetSlideState()
    {
        testCount = 0;
        secondTestUnlocked = false;
        waitingForPaperRemoval = false;
        isProcessingTest = false;
        currentConfig = null;
    }

    private void ResetPaperToStart()
    {
        if (currentConfig?.paperObject == null || currentConfig.startPosition == null)
            return;

        currentConfig.paperObject.position = currentConfig.startPosition.position;
        // currentConfig.paperObject.rotation = currentConfig.startPosition.rotation;

        // The Animator only drives the child "Paper" object's local transform.
        // Snapping the parent above does nothing to that child, so without this
        // the child can be left visually wherever the last PassThrough/Blocked
        // clip ended. Force it back to the bind pose in the same frame.
        if (paperAnimator != null && !string.IsNullOrEmpty(defaultStateName))
        {
            ResetAllTriggers();
            paperAnimator.Play(defaultStateName, 0, 0f);
            paperAnimator.Update(0f);
        }
    }

    #endregion

    #region Interaction

    private void TryProcessPaperClick(Vector3 screenPosition)
    {
        if (paperClickCollider == null || interactionCamera == null)
            return;

        if (isProcessingTest)
        {
            Debug.Log("[SpherometerGap] Click ignored: test in progress.");
            return;
        }

        Ray ray = interactionCamera.ScreenPointToRay(screenPosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

        bool hitPaper = false;
        foreach (var h in hits)
        {
            if (h.collider == paperClickCollider)
            {
                hitPaper = true;
                break;
            }
        }

        if (hitPaper)
        {
            if (waitingForPaperRemoval)
            {
                RemovePaperAfterFirstTest();
                return;
            }

            ExecutePaperTest();
        }
    }

    #endregion

    #region Test Execution

    /// <summary>
    /// Main entry point for running a paper test.
    /// </summary>
    public void ExecutePaperTest()
    {
        if (isProcessingTest) return;

        currentConfig = GetConfiguration(currentPageIndex);

        if (currentConfig == null)
        {
            Debug.LogWarning($"[SpherometerGap] No config for page {currentPageIndex}.");
            return;
        }

        if (testCount >= 2)
        {
            Debug.Log($"[SpherometerGap] Both tests complete on page {currentPageIndex}.");
            return;
        }

        if (testCount == 1 && currentConfig.requiresSpherometerAdjustment && !secondTestUnlocked)
        {
            Debug.Log("[SpherometerGap] Second test locked. Adjust spherometer first.");
            return;
        }

        if (!ValidateConfiguration(currentConfig))
            return;

        StartCoroutine(RunTestSequence(currentConfig));
    }

    private bool ValidateConfiguration(SlideConfiguration config)
    {
        if (config.paperObject == null)
        {
            Debug.LogError($"[SpherometerGap] Page {currentPageIndex}: Paper Object missing.");
            return false;
        }

        if (config.testPosition == null)
        {
            Debug.LogError($"[SpherometerGap] Page {currentPageIndex}: Test Position missing.");
            return false;
        }

        return true;
    }

    private IEnumerator RunTestSequence(SlideConfiguration config)
    {
        isProcessingTest = true;
        testCount++;

        // Position paper for test
        PositionPaper(config.paperObject, config.testPosition);
        yield return new WaitForSeconds(0.1f);

        // Determine result
        PaperTestResult result = DetermineTestResult(config);
        Debug.Log($"[SpherometerGap] Test #{testCount}: {result}");

        // Snap FilterPaper's own transform to the correct end target for this
        // result BEFORE the animation plays. The animator clip only animates
        // the child "Paper" object's local transform - it does not move the
        // parent - so the parent has to be placed here to match the outcome
        // (Filter_paper_Gap for PassThrough, Filter_paper_No_Gap for Blocked).
        Transform endTarget = GetEndPositionForResult(config, result);
        if (endTarget != null)
            PositionPaper(config.paperObject, endTarget);

        // Play animation and wait
        if (paperAnimator != null)
        {
            yield return PlayAnimationAndWait(result);
        }
        else
        {
            // Fallback positioning (only relevant if no animator is assigned)
            if (endTarget == null && result == PaperTestResult.Blocked && config.blockedPosition != null)
                PositionPaper(config.paperObject, config.blockedPosition);

            yield return new WaitForSeconds(0.5f);
        }

        // Handle completion
        HandleTestCompletion(config);
        isProcessingTest = false;
    }

    private PaperTestResult DetermineTestResult(SlideConfiguration config)
    {
        // Physics-based override
        if (config.gapTarget != null)
        {
            float distance = Vector3.Distance(config.paperObject.position, config.gapTarget.position);
            return distance <= config.gapThreshold ? PaperTestResult.PassThrough : PaperTestResult.Blocked;
        }

        // Preset result based on test count
        return testCount == 1 ? config.firstTestResult : config.secondTestResult;
    }

    /// <summary>
    /// Maps a test result to the Transform that FilterPaper should be snapped
    /// to for that outcome (Filter_paper_Gap for PassThrough, Filter_paper_No_Gap
    /// for Blocked). Falls back to the legacy blockedPosition if the new fields
    /// aren't assigned, and returns null if nothing is set (no repositioning).
    /// </summary>
    private Transform GetEndPositionForResult(SlideConfiguration config, PaperTestResult result)
    {
        if (result == PaperTestResult.PassThrough)
            return config.passThroughEndPosition;

        return config.blockedEndPosition != null ? config.blockedEndPosition : config.blockedPosition;
    }

    private IEnumerator PlayAnimationAndWait(PaperTestResult result)
    {
        ResetAllTriggers();

        string trigger = result == PaperTestResult.PassThrough ? passThroughTrigger : blockedTrigger;
        paperAnimator.SetTrigger(trigger);

        // Wait for animator transition
        yield return null;
        yield return null;

        // Read animation length
        AnimatorStateInfo stateInfo = paperAnimator.GetCurrentAnimatorStateInfo(0);
        float waitTime = stateInfo.length > 0.01f ? stateInfo.length : 1.5f;
        waitTime += animationBufferTime;

        yield return new WaitForSeconds(waitTime);
    }

    private void HandleTestCompletion(SlideConfiguration config)
    {
        if (testCount == 1)
        {
            config.OnFirstTestComplete?.Invoke();

            // Require one additional click on the paper to remove it after
            // the first test, as shown in the experiment flow.
            waitingForPaperRemoval = true;

            if (!config.requiresSpherometerAdjustment)
            {
                Debug.Log("[SpherometerGap] First test done. Paper removal required before continuing.");
            }
            else
            {
                Debug.Log("[SpherometerGap] First test done. Click paper once more to remove it, then adjust the spherometer.");
            }
        }
        else if (testCount == 2)
        {
            config.OnSecondTestComplete?.Invoke();
            config.OnSequenceComplete?.Invoke();
            OnAnySequenceComplete?.Invoke();

            Debug.Log($"[SpherometerGap] Sequence complete on page {currentPageIndex}.");
        }
    }

    #endregion

    /// <summary>
    /// Removes the paper after the first gap test.
    /// This is the extra paper click shown in the experiment sequence.
    /// The paper is restored when the second test is unlocked.
    /// </summary>
    private void RemovePaperAfterFirstTest()
    {
        waitingForPaperRemoval = false;

        if (currentConfig?.paperObject == null)
            return;

        currentConfig.paperObject.gameObject.SetActive(false);

        Debug.Log($"[SpherometerGap] Paper removed after first test on page {currentPageIndex}.");
    }

    #region Positioning

    private void PositionPaper(Transform paper, Transform target)
    {
        if (paper == null || target == null) return;
        paper.position = target.position;
        // paper.rotation = target.rotation;
    }

    /// <summary>
    /// Moves paper to a specific position via script or UI button.
    /// </summary>
    public void MovePaperTo(PaperPosition position)
    {
        var config = GetConfiguration(currentPageIndex);
        if (config?.paperObject == null) return;

        Transform target = position switch
        {
            PaperPosition.Start => config.startPosition,
            PaperPosition.TestPosition => config.testPosition,
            PaperPosition.PassThroughEnd => config.passThroughEndPosition,
            PaperPosition.BlockedEnd => config.blockedEndPosition != null ? config.blockedEndPosition : config.blockedPosition,
            PaperPosition.Blocked => config.blockedPosition,
            _ => null
        };

        if (target != null)
            PositionPaper(config.paperObject, target);
    }

    #endregion

    #region Spherometer Integration

    /// <summary>
    /// Call from SpherometerStepController after screw adjustment.
    /// </summary>
    public void UnlockSecondTest()
    {
        if (testCount != 1)
        {
            Debug.LogWarning($"[SpherometerGap] Unlock failed: expected 1 test, found {testCount}.");
            return;
        }

        secondTestUnlocked = true;

        // Bring the paper back for the second test after the Spherometer adjustment.
        if (currentConfig?.paperObject != null)
        {
            currentConfig.paperObject.gameObject.SetActive(true);
            ResetPaperToStart();
        }

        Debug.Log($"[SpherometerGap] Second test unlocked on page {currentPageIndex}. Paper restored for second test.");
    }

    #endregion

    #region Animation Helpers

    private void ResetAllTriggers()
    {
        if (paperAnimator == null) return;

        if (!string.IsNullOrEmpty(passThroughTrigger))
            paperAnimator.ResetTrigger(passThroughTrigger);

        if (!string.IsNullOrEmpty(blockedTrigger))
            paperAnimator.ResetTrigger(blockedTrigger);
    }

    #endregion

    #region Public Utilities

    public void ResetCurrentSlide()
    {
        HandlePageChanged(currentPageIndex);
    }

    public void UnlockPageNavigation()
    {
        PageNavigationController.RequestNavigationUnlock();
    }

    // State inspectors
    public bool IsTestInProgress => isProcessingTest;
    public int CompletedTestCount => testCount;
    public bool IsSecondTestUnlocked => secondTestUnlocked;
    public SurfaceType CurrentSurfaceType => currentConfig?.surfaceType ?? SurfaceType.ConvexLens;

    #endregion
}