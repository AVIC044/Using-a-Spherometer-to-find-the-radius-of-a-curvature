using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Pose-based scale measurement controller.
/// Tap the scale to cycle through measurement poses. Each pose can reveal a text label.
/// </summary>
public class ScaleWithTextController : MonoBehaviour
{
    // =========================================================
    // DATA
    // =========================================================

    [System.Serializable]
    public class MeasurementPose
    {
        [Tooltip("Name for debugging.")]
        public string poseName = "Pose";

        [Tooltip("Target transform in the scene.")]
        public Transform targetTransform;

        [Tooltip("Text object to enable when this pose is reached. Can be null.")]
        public GameObject measurementText;

        [Tooltip("Event fired when this pose is reached and movement completes.")]
        public UnityEvent onPoseReached;
    }

    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("Scale Object")]
    [SerializeField] private GameObject scaleObject;
    [Tooltip("Transform target used when active on configured page index (e.g. Scale_Slide_pos).")]
    [SerializeField] private Transform originalTransform;

    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Measurement Poses (cycled on tap)")]
    [Tooltip("Add poses in order. Last tap returns to active transform position.")]
    public List<MeasurementPose> measurementPoses = new List<MeasurementPose>();

    [Header("Movement Settings")]
    [SerializeField] private float moveDuration = 1.5f;

    [Header("Page Sync")]
    [Tooltip("Page indices on which this controller accepts input and applies its poses.")]
    [SerializeField] private List<int> activePageIndices = new List<int>();

    [Tooltip("If true, snaps scale to originalTransform (Scale_Slide_pos) when entering an active page index.")]
    [SerializeField] private bool applyOriginalOnPageEnter = true;

    [Tooltip("If true, resets scale to initial scene position (near pencil) when leaving or on inactive pages.")]
    [SerializeField] private bool resetOnPageLeave = true;

    [Header("Events")]
    [Tooltip("Fires when all poses have been visited and the scale returns to active position.")]
    public UnityEvent onAllMeasurementsComplete;

    // =========================================================
    // STATE
    // =========================================================

    private bool isMoving = false;
    private int currentPoseIndex = -1; // -1 = start position
    private bool isActiveOnCurrentPage = false;

    // Initial Scene Transform (Near Pencil in Editor)
    private Vector3 initialScenePosition;
    private Quaternion initialSceneRotation;
    private Vector3 initialSceneScale;

    // Active Page Transform (Scale_Slide_pos)
    private Vector3 activePagePosition;
    private Quaternion activePageRotation;
    private Vector3 activePageScale;
    private bool hasActiveTransform;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        // 1. Capture initial scene placement (Near pencil)
        if (scaleObject != null)
        {
            initialScenePosition = scaleObject.transform.position;
            initialSceneRotation = scaleObject.transform.rotation;
            initialSceneScale = scaleObject.transform.localScale;
        }

        // 2. Capture target transform for active slide index (Scale_Slide_pos)
        Transform targetSource = originalTransform != null ? originalTransform : (scaleObject != null ? scaleObject.transform : null);
        if (targetSource != null)
        {
            activePagePosition = targetSource.position;
            activePageRotation = targetSource.rotation;
            activePageScale = targetSource.localScale;
            hasActiveTransform = true;
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        HideAllTexts();
    }

    private void Update()
    {
        if (!isActiveOnCurrentPage)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        HandleMouse();
        HandleTouch();
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void HandleMouse()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryClickScale(Mouse.current.position.ReadValue());
    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null) return;

        var touch = Touchscreen.current.primaryTouch;
        if (touch.press.wasPressedThisFrame)
            TryClickScale(touch.position.ReadValue());
    }

    private void TryClickScale(Vector2 screenPosition)
    {
        if (scaleObject == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsPartOfObject(hit.collider.gameObject, scaleObject))
            {
                if (!isMoving)
                {
                    StartCoroutine(AdvancePoseRoutine());
                }
                return;
            }
        }
    }

    // =========================================================
    // POSE CYCLING
    // =========================================================

    private IEnumerator AdvancePoseRoutine()
    {
        isMoving = true;

        currentPoseIndex++;

        // Past last pose -> return to active transform (Scale_Slide_pos)
        if (currentPoseIndex >= measurementPoses.Count)
        {
            currentPoseIndex = -1;

            if (hasActiveTransform)
            {
                yield return StartCoroutine(AnimateScaleTo(activePagePosition, activePageRotation, activePageScale));
            }

            onAllMeasurementsComplete?.Invoke();
            isMoving = false;
            yield break;
        }

        MeasurementPose pose = measurementPoses[currentPoseIndex];

        if (pose.targetTransform == null)
        {
            Debug.LogWarning($"ScaleWithTextController: Pose '{pose.poseName}' has no target transform. Skipping.");
            isMoving = false;
            yield break;
        }

        yield return StartCoroutine(AnimateScaleTo(
            pose.targetTransform.position,
            pose.targetTransform.rotation,
            pose.targetTransform.localScale
        ));

        if (pose.measurementText != null)
        {
            pose.measurementText.SetActive(true);

            pose.measurementText.transform.localScale = Vector3.zero;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                pose.measurementText.transform.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t / 0.25f);
                yield return null;
            }
            pose.measurementText.transform.localScale = Vector3.one;
        }

        pose.onPoseReached?.Invoke();

        isMoving = false;
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private IEnumerator AnimateScaleTo(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        if (scaleObject == null) yield break;

        Vector3 startPos = scaleObject.transform.position;
        Quaternion startRot = scaleObject.transform.rotation;
        Vector3 startScale = scaleObject.transform.localScale;

        float time = 0f;
        while (time < moveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / moveDuration));

            scaleObject.transform.position = Vector3.Lerp(startPos, targetPos, t);
            scaleObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            scaleObject.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        scaleObject.transform.position = targetPos;
        scaleObject.transform.rotation = targetRot;
        scaleObject.transform.localScale = targetScale;
    }

    // =========================================================
    // PAGE SYNC
    // =========================================================

    private void HandlePageChanged(int pageIndex)
    {
        bool isMatchingPage = activePageIndices.Count == 0 || activePageIndices.Contains(pageIndex);

        if (isMatchingPage)
        {
            isActiveOnCurrentPage = true;
            if (applyOriginalOnPageEnter)
            {
                ApplyActivePageTransform();
            }
        }
        else
        {
            isActiveOnCurrentPage = false;
            if (resetOnPageLeave)
            {
                ResetToInitialSceneTransform();
            }
        }
    }

    // =========================================================
    // RESET & UTILS
    // =========================================================

    /// <summary>
    /// Snaps scale object to the target transform configured for Index 10 (Scale_Slide_pos).
    /// </summary>
    public void ApplyActivePageTransform()
    {
        if (scaleObject == null || !hasActiveTransform) return;

        StopAllCoroutines();
        isMoving = false;
        currentPoseIndex = -1;

        scaleObject.transform.position = activePagePosition;
        scaleObject.transform.rotation = activePageRotation;
        scaleObject.transform.localScale = activePageScale;

        HideAllTexts();
    }

    /// <summary>
    /// Resets scale object back to its scene starting position (near pencil).
    /// </summary>
    public void ResetToInitialSceneTransform()
    {
        if (scaleObject == null) return;

        StopAllCoroutines();
        isMoving = false;
        currentPoseIndex = -1;

        scaleObject.transform.position = initialScenePosition;
        scaleObject.transform.rotation = initialSceneRotation;
        scaleObject.transform.localScale = initialSceneScale;

        HideAllTexts();
    }

    public void ResetController()
    {
        ResetToInitialSceneTransform();
    }

    private void HideAllTexts()
    {
        foreach (var pose in measurementPoses)
        {
            if (pose.measurementText != null)
                pose.measurementText.SetActive(false);
        }
    }

    private bool IsPartOfObject(GameObject hitObject, GameObject targetObject)
    {
        if (hitObject == targetObject) return true;

        Transform current = hitObject.transform;
        while (current != null)
        {
            if (current.gameObject == targetObject) return true;
            current = current.parent;
        }
        return false;
    }

    // =========================================================
    // PUBLIC HELPERS
    // =========================================================

    public void NextPose()
    {
        if (!isMoving && isActiveOnCurrentPage)
            StartCoroutine(AdvancePoseRoutine());
    }

    public int GetCurrentPoseIndex() => currentPoseIndex;
}