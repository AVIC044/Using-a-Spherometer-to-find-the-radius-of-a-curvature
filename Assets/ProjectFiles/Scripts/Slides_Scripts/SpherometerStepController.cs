using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;

public enum MoveDirection { None, Up, Down }
public enum RotationDirection { None, Clockwise, CounterClockwise }

[System.Serializable]
public class SpherometerStepMapping
{
    [Header("Step Info")]
    public string stepName;
    public int stepIndex;

    [Header("Movement Control for this Index")]
    public MoveDirection moveDirection = MoveDirection.Up;
    public float customMoveDistance = 0.01f;

    [Header("Rotation Control for this Index")]
    public RotationDirection rotationDirection = RotationDirection.CounterClockwise;
    public float customRotationAngle = 360f; // e.g., 360 for a full spin, or 45 for a slight turn
    public float rotationDuration = 1f;

    [Header("Events")]
    public UnityEvent onStepTriggered; // Triggered when the rotation starts
    public UnityEvent onStepCompleted; // Triggered when the rotation finishes
}

[System.Serializable]
public class SpherometerSlideData
{
    [Header("Page Settings")]
    public int pageIndex;
    public int maxRotationsCount = 3;

    [Header("Step Mappings (Configure direction per index here)")]
    public List<SpherometerStepMapping> stepMappings = new();

    [Header("Slide Events")]
    public UnityEvent OnSlideCheckPassed;
}

public class SpherometerStepController : MonoBehaviour
{
    [Header("General References")]
    [SerializeField] private MeshRenderer _spehrometerRotationObject;
    [SerializeField] private TMP_Text _countText;

    [Header("Slide Configurations")]
    [SerializeField] private List<SpherometerSlideData> slideDataList = new();

    [Header("Global Events")]
    public UnityEvent OnFinalSequenceCompleted;

    [Header("Interaction")]
    [Tooltip("Collider(s) that count as 'clicking the spherometer' to trigger the next rotation step. Leave empty to automatically use every collider found in this object and its children, so clicking anywhere on the whole model works - not just one small handle mesh.")]
    [SerializeField] private Collider[] interactionColliders;

    [Header("Step Highlight")]
    [Tooltip("Highlight shown on the clickable screw/handle while the current step is waiting for input.")]
    [SerializeField] private GameObject stepHighlight;

    private int currentPageIndex = 0;
    private int currentRotationCount = 0;
    private bool isAnimating = false;

    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
        if (mainCam == null)
            mainCam = FindFirstObjectByType<Camera>();

        if (interactionColliders == null || interactionColliders.Length == 0)
            interactionColliders = GetComponentsInChildren<Collider>(true);

        SetStepHighlight(false);
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
        if (_countText != null)
        {
            _countText.gameObject.SetActive(false);
        }

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryHandleClick(Input.mousePosition);
    }

    /// <summary>
    /// Replaces Unity's built-in OnMouseDown(), which only fires for the
    /// single CLOSEST collider hit by the click - if some other collider
    /// (the spherometer's own drag collider, the table, another prop)
    /// happens to be nearer to the camera at that pixel, OnMouseDown would
    /// silently never fire at all. This instead checks every collider hit
    /// along the ray against the full set of colliders that belong to this
    /// spherometer, so any click landing anywhere on the model registers.
    /// </summary>
    private void TryHandleClick(Vector3 screenPos)
    {
        if (mainCam == null || interactionColliders == null || interactionColliders.Length == 0)
            return;

        Ray ray = mainCam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        foreach (var hit in hits)
        {
            foreach (var col in interactionColliders)
            {
                if (col != null && hit.collider == col)
                {
                    // The player has interacted with the current step, so
                    // remove the prompt highlight before starting the motion.
                    SetStepHighlight(false);
                    TriggerNextRotation();
                    return;
                }
            }
        }
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
        currentRotationCount = 0;
        isAnimating = false;

        if (_countText != null)
        {
            _countText.gameObject.SetActive(false);
        }

        // A new page starts a new spherometer sequence. Show the highlight
        // only when there is actually a configured step waiting for input.
        SetStepHighlight(HasStepWaitingForInput());
    }

    public void TriggerNextRotation()
    {
        SpherometerSlideData currentData = GetCurrentSlideData();
        if (currentData != null && currentRotationCount < currentData.maxRotationsCount)
        {
            TriggerRotationByIndex(currentRotationCount);
        }
    }

    public void TriggerRotationByIndex(int index)
    {
        SpherometerSlideData currentData = GetCurrentSlideData();
        if (currentData == null) return;

        SpherometerStepMapping stepMapping = currentData.stepMappings.Find(x => x.stepIndex == index);

        if (!isAnimating && index == currentRotationCount && index < currentData.maxRotationsCount)
        {
            StartCoroutine(RotateSpherometerScaleSingleStep(stepMapping, index));
        }
    }

    private SpherometerSlideData GetCurrentSlideData()
    {
        return slideDataList.Find(x => x.pageIndex == currentPageIndex);
    }

    private IEnumerator RotateSpherometerScaleSingleStep(SpherometerStepMapping mapping, int stepIndex)
    {
        isAnimating = true;

        // Fallback default mapping if none configured for this index
        float moveDist = mapping != null ? mapping.customMoveDistance : 0.01f;
        MoveDirection moveDir = mapping != null ? mapping.moveDirection : MoveDirection.Up;
        RotationDirection rotDir = mapping != null ? mapping.rotationDirection : RotationDirection.CounterClockwise;
        float rotAngle = mapping != null ? mapping.customRotationAngle : 360f;
        float duration = mapping != null ? mapping.rotationDuration : 1f;

        // 1. Fire start events
        if (mapping != null) mapping.onStepTriggered?.Invoke();

        float elapsed = 0f;
        Vector3 rotationStartPos = _spehrometerRotationObject.transform.localPosition;

        // Determine translation direction vector
        Vector3 directionVector = Vector3.zero;
        if (moveDir == MoveDirection.Up) directionVector = Vector3.up;
        else if (moveDir == MoveDirection.Down) directionVector = Vector3.down;

        Vector3 rotationEndPos = rotationStartPos + (directionVector * moveDist);

        // Determine sign for rotation angle (Clockwise vs CounterClockwise)
        float angleMultiplier = (rotDir == RotationDirection.Clockwise) ? 1f : -1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Calculate rotation and position based on step settings
            float currentAngle = t * rotAngle * angleMultiplier;
            _spehrometerRotationObject.transform.localRotation = Quaternion.Euler(0, currentAngle, 0);
            _spehrometerRotationObject.transform.localPosition = Vector3.Lerp(rotationStartPos, rotationEndPos, t);

            yield return null;
        }

        // Snap precisely to target to prevent drift
        _spehrometerRotationObject.transform.localPosition = rotationEndPos;
        _spehrometerRotationObject.transform.localRotation = Quaternion.Euler(0, rotAngle * angleMultiplier, 0);

        currentRotationCount++;

        if (_countText != null)
        {
            _countText.text = currentRotationCount.ToString();
            _countText.gameObject.SetActive(true);
        }

        // 2. Fire completion events
        if (mapping != null) mapping.onStepCompleted?.Invoke();

        isAnimating = false;

        SpherometerSlideData currentData = GetCurrentSlideData();
        if (currentData != null && currentRotationCount >= currentData.maxRotationsCount)
        {
            // Final step: there is no more interaction to prompt.
            SetStepHighlight(false);
            currentData.OnSlideCheckPassed?.Invoke();
            OnFinalSequenceCompleted?.Invoke();
        }
        else
        {
            // Another step is waiting, so show the highlight again.
            SetStepHighlight(HasStepWaitingForInput());
        }
    }

    private bool HasStepWaitingForInput()
    {
        SpherometerSlideData currentData = GetCurrentSlideData();

        if (currentData == null || currentRotationCount >= currentData.maxRotationsCount)
            return false;

        // Only show the highlight when the current rotation index has a
        // configured step mapping. This keeps the prompt synchronized with
        // the step sequence rather than merely the page.
        return currentData.stepMappings.Exists(x => x != null && x.stepIndex == currentRotationCount);
    }

    private void SetStepHighlight(bool active)
    {
        if (stepHighlight != null)
            stepHighlight.SetActive(active);
    }

    public void UnlockPageNavigation()
    {
        PageNavigationController.RequestNavigationUnlock();
    }
}