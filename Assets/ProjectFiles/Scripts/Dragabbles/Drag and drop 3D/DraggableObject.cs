using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class DraggableObject : MonoBehaviour
{
   [System.Serializable]
    public class SnapElement
    {
        public int index; // Page/Step Index
        public bool unlocknavigationOnSnap = true;

        [Header("Standard Highlight (Ignored if Is Spherometer is checked)")]
        public GameObject highlightObject;

        [Header("Spherometer Target Mapping")]
        [Tooltip("Index from 'Spherometer Targets' array for this step (0 = Table, 1 = Convex Lens, 2 = Glass Plate, etc.).")]
        public int targetPointIndex = 0;

        public bool restoreToSnapWhenConditionActive = true;
        public UnityEvent OnSnapCompleted;

        [Header("Display Options")]
        [Tooltip("If enabled, first time this index is reached, interaction will be ignored.")]
        public bool enableFirstIgnore = false;

        [HideInInspector] public bool hasVisitedOnce = false;
        [HideInInspector] public Collider highlightCollider;

        // ===============================
        // 🔍 Debugging Only – Do Not Modify
        // ===============================
        [Tooltip("True once snapping is completed. Dragging will be disabled.")]
        public bool snapped;
    }

    [Header("Spherometer Settings")]
    [Tooltip("Check this if this object is a Spherometer requiring mapped target positioning.")]
    [SerializeField] private bool isSpherometer = false;

    // ✅ Single Shared Highlight Object for Spherometer Mode
    [Tooltip("Single shared highlight object used across all spherometer steps.")]
    [SerializeField] private GameObject spherometerHighlightObject;

    // ✅ Central Master Target Array
    [Tooltip("Master list of target Transforms in scene (e.g., Index 0: Table, Index 1: Lens, Index 2: Glass Plate).")]
    [SerializeField] private Transform[] spherometerTargets;

    [Header("Snap Elements")]
    [SerializeField] private List<SnapElement> elements = new List<SnapElement>();

    [Header("Movement")]
    [SerializeField] private float snapSpeed = 8f;
    [SerializeField] private float returnSpeed = 6f;
    [SerializeField] private float snapDistance = 0.01f;

    [Header("Rotation")]
    [SerializeField] private bool snapRotation = false;
    [SerializeField] private float snapRotationThreshold = 0.5f;

    [Header("Mode")]
    [SerializeField] private bool triggerEventOnly = false;

    [Header("Animator Control")]
    [SerializeField] private Animator animator;

    [Header("Drag Events")]
    [SerializeField] private UnityEvent OnDragStart;

    private PageNavigationController pageNavigationController;

    private Camera mainCam;
    private Collider objectCollider;
    private Collider spherometerHighlightCollider;

    private bool isDragging;
    private bool snapping;
    private bool returning;
    private bool canDrag;
    private bool interactionLocked;

    private int activeElementIndex = -1;
    private int lastSnappedElementIndex = -1;
    private Transform lastSnappedTargetTransform;

    private Vector3 offset;
    private float objectScreenZ;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Awake()
    {
        pageNavigationController = FindFirstObjectByType<PageNavigationController>();

        mainCam = Camera.main;
        if (mainCam == null)
            mainCam = FindFirstObjectByType<Camera>();

        objectCollider = GetComponent<Collider>();

        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Cache shared Spherometer Highlight Collider
        if (spherometerHighlightObject != null)
        {
            spherometerHighlightCollider = spherometerHighlightObject.GetComponent<Collider>();
            spherometerHighlightObject.SetActive(false);
        }

        // Cache standard individual Highlight Colliders
        foreach (var element in elements)
        {
            if (element.highlightObject != null)
            {
                element.highlightCollider = element.highlightObject.GetComponent<Collider>();
                element.highlightObject.SetActive(false);
            }
        }
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
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        ResetState();

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].index == pageIndex)
            {
                ActivateElement(i);
                return;
            }
        }

        canDrag = false;
        activeElementIndex = -1;
    }

    void ResetState()
    {
        isDragging = false;
        snapping = false;
        returning = false;
    }

    void ActivateElement(int index)
    {
        activeElementIndex = index;
        interactionLocked = false;

        var element = elements[index];
        GameObject activeHighlight = GetActiveHighlightObject(element);
        Transform targetTransform = GetActiveTargetTransform(element);

        // Reposition highlight object to active target
        if (activeHighlight != null && targetTransform != null)
        {
            activeHighlight.transform.position = targetTransform.position;
            activeHighlight.transform.rotation = targetTransform.rotation;
        }

        // First-time ignore logic
        if (element.enableFirstIgnore && !element.hasVisitedOnce)
        {
            element.hasVisitedOnce = true;
            canDrag = false;
            interactionLocked = true;
            return;
        }

        element.hasVisitedOnce = true;

        if (element.snapped)
        {
            canDrag = false;
            interactionLocked = true;
        }
        else
        {
            canDrag = true;
        }

        if (element.restoreToSnapWhenConditionActive && element.snapped)
        {
            Transform t = (isSpherometer && lastSnappedTargetTransform != null) 
                ? lastSnappedTargetTransform 
                : targetTransform;

            if (t != null)
            {
                transform.position = t.position;
                if (snapRotation)
                    transform.rotation = t.rotation;
            }
        }
    }

    void Update()
    {
        if (returning)
        {
            ReturnToLastValidPosition();
            return;
        }

        if (!triggerEventOnly && snapping)
        {
            SnapToHighlight();
            return;
        }

        if (!canDrag || interactionLocked)
            return;

        HandleInput();
    }

    void HandleInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
            TryStartDrag(Input.mousePosition);

        if (isDragging && Input.GetMouseButton(0))
            Drag(Input.mousePosition);

        if (isDragging && Input.GetMouseButtonUp(0))
            Release();
    }

    void TryStartDrag(Vector3 inputPos)
    {
        if (activeElementIndex < 0) return;

        var element = elements[activeElementIndex];
        if (element.snapped) return;

        Ray ray = mainCam.ScreenPointToRay(inputPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider == objectCollider)
            {
                isDragging = true;
                OnDragStart?.Invoke();

                if (animator != null && animator.enabled)
                    animator.enabled = false;

                objectScreenZ = mainCam.WorldToScreenPoint(transform.position).z;
                offset = transform.position - GetWorldPosition(inputPos);

                GameObject activeHighlight = GetActiveHighlightObject(element);
                Transform targetTransform = GetActiveTargetTransform(element);

                if (activeHighlight != null && targetTransform != null)
                {
                    activeHighlight.transform.position = targetTransform.position;
                    activeHighlight.transform.rotation = targetTransform.rotation;
                    activeHighlight.SetActive(true);
                }
            }
        }
    }

    void Drag(Vector3 inputPos)
    {
        transform.position = GetWorldPosition(inputPos) + offset;
    }

    void Release()
    {
        if (!isDragging) return;

        isDragging = false;

        if (activeElementIndex < 0)
        {
            StartReturn();
            EnableAnimator();
            return;
        }

        var element = elements[activeElementIndex];
        Collider targetCollider = GetActiveHighlightCollider(element);

        if (targetCollider == null)
        {
            StartReturn();
            EnableAnimator();
            return;
        }

        bool inside = objectCollider.bounds.Intersects(targetCollider.bounds);

        if (triggerEventOnly)
        {
            if (inside)
            {
                if (!element.snapped)
                {
                    element.snapped = true;
                    lastSnappedElementIndex = activeElementIndex;
                    lastSnappedTargetTransform = GetActiveTargetTransform(element);

                    GameObject activeHighlight = GetActiveHighlightObject(element);
                    if (activeHighlight != null)
                        activeHighlight.SetActive(false);

                    element.OnSnapCompleted?.Invoke();

                    if (pageNavigationController != null && element.unlocknavigationOnSnap)
                        pageNavigationController.EnableNavigationButtons();

                    canDrag = false;
                    interactionLocked = true;
                }

                EnableAnimator();
            }
            else
            {
                StartReturn();
            }

            return;
        }

        if (inside && !element.snapped)
        {
            snapping = true;
        }
        else
        {
            StartReturn();
        }
    }

    void SnapToHighlight()
    {
        var element = elements[activeElementIndex];
        Transform target = GetActiveTargetTransform(element);

        if (target == null)
        {
            StartReturn();
            return;
        }

        transform.position = Vector3.Lerp(transform.position, target.position, snapSpeed * Time.deltaTime);

        if (snapRotation)
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, snapSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < snapDistance)
        {
            transform.position = target.position;

            if (snapRotation)
                transform.rotation = target.rotation;

            snapping = false;

            element.snapped = true;
            lastSnappedElementIndex = activeElementIndex;
            lastSnappedTargetTransform = target;

            canDrag = false;
            interactionLocked = true;

            GameObject activeHighlight = GetActiveHighlightObject(element);
            if (activeHighlight != null)
                activeHighlight.SetActive(false);

            element.OnSnapCompleted?.Invoke();

            if (pageNavigationController != null && element.unlocknavigationOnSnap)
                pageNavigationController.EnableNavigationButtons();

            EnableAnimator();
        }
    }

    void StartReturn()
    {
        returning = true;

        if (activeElementIndex >= 0)
        {
            var element = elements[activeElementIndex];
            GameObject activeHighlight = GetActiveHighlightObject(element);

            if (activeHighlight != null)
                activeHighlight.SetActive(false);
        }
    }

    void ReturnToLastValidPosition()
    {
        Vector3 targetPos = originalPosition;

        if (lastSnappedElementIndex >= 0)
        {
            if (lastSnappedTargetTransform != null)
            {
                targetPos = lastSnappedTargetTransform.position;
            }
            else if (elements[lastSnappedElementIndex].highlightObject != null)
            {
                targetPos = elements[lastSnappedElementIndex].highlightObject.transform.position;
            }
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < snapDistance)
        {
            transform.position = targetPos;
            returning = false;

            EnableAnimator();
        }
    }

    // ==========================================
    // 🛠️ HELPER METHODS (Spherometer Fallbacks)
    // ==========================================

    private GameObject GetActiveHighlightObject(SnapElement element)
    {
        if (isSpherometer && spherometerHighlightObject != null)
            return spherometerHighlightObject;

        return element.highlightObject;
    }

    private Collider GetActiveHighlightCollider(SnapElement element)
    {
        if (isSpherometer && spherometerHighlightCollider != null)
            return spherometerHighlightCollider;

        return element.highlightCollider;
    }

    private Transform GetActiveTargetTransform(SnapElement element)
    {
        if (isSpherometer && spherometerTargets != null && spherometerTargets.Length > 0)
        {
            if (element.targetPointIndex >= 0 && element.targetPointIndex < spherometerTargets.Length)
            {
                return spherometerTargets[element.targetPointIndex];
            }

            Debug.LogWarning($"[DraggableObject] Index {element.targetPointIndex} out of bounds for spherometerTargets.");
            return null;
        }

        return element.highlightObject != null ? element.highlightObject.transform : null;
    }

    void EnableAnimator()
    {
        if (animator != null && !animator.enabled)
            animator.enabled = true;
    }

    Vector3 GetWorldPosition(Vector3 inputPos)
    {
        inputPos.z = objectScreenZ;
        return mainCam.ScreenToWorldPoint(inputPos);
    }
}