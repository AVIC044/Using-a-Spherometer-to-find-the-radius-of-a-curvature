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

        [Header("Standard Highlight (Ignored if a SpherometerTargetProvider is present)")]
        public GameObject highlightObject;

        [Header("Spherometer Target Mapping")]
        [Tooltip("Index into the SpherometerTargetProvider's target list for this step (0 = Table, 1 = Convex Lens, 2 = Glass Plate, etc.). Only used if a SpherometerTargetProvider is attached.")]
        public int targetPointIndex = 0;

        public bool restoreToSnapWhenConditionActive = true;
        public UnityEvent OnSnapCompleted;

        [Header("Per-Index Rotation")]
        [Tooltip("Additional Euler rotation applied to this object for this page. Use (0, 180, 0) to flip around Y.")]
        public Vector3 rotationOffsetEuler = Vector3.zero;

        [Header("Display Options")]
        [Tooltip("If enabled, first time this index is reached, interaction will be ignored.")]
        public bool enableFirstIgnore = false;

        [HideInInspector] public bool hasVisitedOnce = false;
        [HideInInspector] public Collider highlightCollider;

        // ===============================
        // Debugging Only - Do Not Modify
        // ===============================
        [Tooltip("True once snapping is completed. Dragging will be disabled.")]
        public bool snapped;
    }

    [Header("Snap Elements")]
    [SerializeField] private List<SnapElement> elements = new List<SnapElement>();

    [Header("Movement")]
    [SerializeField] private float snapSpeed = 8f;
    [SerializeField] private float returnSpeed = 6f;
    [SerializeField] private float snapDistance = 0.01f;

    [Header("Rotation")]
    [SerializeField] private bool snapRotation = false;
    // [SerializeField] private float snapRotationThreshold = 0.5f;

    [Header("Mode")]
    [SerializeField] private bool triggerEventOnly = false;

    [Header("Drag Surface")]
    [Tooltip("Layer(s) the object should stay glued to the top of while being dragged (e.g. the table). Put your table's collider on a dedicated layer and select only that layer here.")]
    [SerializeField] private LayerMask dragSurfaceMask = ~0;

    [Tooltip("Small lift above the surface hit point to avoid z-fighting/clipping into the surface mesh.")]
    [SerializeField] private float dragSurfaceOffset = 0.001f;

    [Header("Animator Control")]
    [SerializeField] private Animator animator;

    [Header("Drag Events")]
    [SerializeField] private UnityEvent OnDragStart;

    private PageNavigationController pageNavigationController;

    // Optional - present only on objects that need spherometer-style mapped targets.
    private SpherometerTargetProvider spherometerProvider;

    private Camera mainCam;
    private Collider objectCollider;

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

    // Offset between this object's pivot and the point on the drag surface
    // where it was grabbed - recomputed each drag start, applied each frame.
    private Vector3 surfaceGrabOffset;
    private bool hasSurfaceGrabOffset;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Awake()
    {
        pageNavigationController = FindFirstObjectByType<PageNavigationController>();
        spherometerProvider = GetComponent<SpherometerTargetProvider>();

        mainCam = Camera.main;
        if (mainCam == null)
            mainCam = FindFirstObjectByType<Camera>();

        objectCollider = GetComponent<Collider>();

        originalPosition = transform.position;
        originalRotation = transform.rotation;

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

        // Re-sync every time this object is (re)activated, not just the
        // very first time. Start() only ever runs once per GameObject
        // lifetime, so if something disables/enables this object per page
        // (as "paper" does), relying on Start() alone leaves canDrag stuck
        // at whatever it was before deactivation.
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
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
            Transform t = (spherometerProvider != null && lastSnappedTargetTransform != null)
                ? lastSnappedTargetTransform
                : targetTransform;

            if (t != null)
            {
                transform.position = t.position;
                if (snapRotation)
                    transform.rotation = t.rotation * Quaternion.Euler(element.rotationOffsetEuler);
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

        if (Input.GetMouseButtonDown(0))
            Debug.Log($"[DraggableObject:{name}] canDrag={canDrag} interactionLocked={interactionLocked}");

        if (!canDrag || interactionLocked)
            return;

        HandleInput();
    }

    void HandleInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0))
                Debug.Log($"[DraggableObject:{name}] blocked - pointer is over a UI GameObject");
            return;
        }

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

        // Use RaycastAll instead of Raycast: with multiple draggable objects
        // whose colliders can overlap in screen space, the closest hit isn't
        // necessarily THIS object's collider. Check every hit along the ray
        // for our own collider rather than only the nearest one.
        RaycastHit[] hits = Physics.RaycastAll(ray);
        bool hitSelf = false;
        foreach (var h in hits)
        {
            if (h.collider == objectCollider)
            {
                hitSelf = true;
                break;
            }
        }

        Debug.Log($"[DraggableObject:{name}] raycast hit {hits.Length} collider(s), self hit={hitSelf}");

        if (hitSelf)
        {
            isDragging = true;
            OnDragStart?.Invoke();

            if (animator != null && animator.enabled)
                animator.enabled = false;

            objectScreenZ = mainCam.WorldToScreenPoint(transform.position).z;
            offset = transform.position - GetWorldPosition(inputPos);

            // Also record the offset relative to the drag surface (e.g. the
            // table), if the grab ray hits it. Drag() uses this every frame
            // so the object rides the surface's actual height instead of a
            // camera-facing plane at a fixed depth, which is what let it
            // sink below the table as you dragged.
            hasSurfaceGrabOffset = Physics.Raycast(ray, out RaycastHit surfaceHit, Mathf.Infinity, dragSurfaceMask);
            if (hasSurfaceGrabOffset)
                surfaceGrabOffset = transform.position - surfaceHit.point;

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

    void Drag(Vector3 inputPos)
    {
        Ray ray = mainCam.ScreenPointToRay(inputPos);

        if (hasSurfaceGrabOffset && Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, dragSurfaceMask))
        {
            transform.position = hit.point + surfaceGrabOffset + hit.normal * dragSurfaceOffset;
        }
        else
        {
            // Fallback: no drag surface hit (e.g. dragged past the table's
            // edge, or dragSurfaceMask isn't set up). Keeps dragging usable
            // instead of freezing, at the cost of the old sinking behavior.
            transform.position = GetWorldPosition(inputPos) + offset;
        }
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

        Quaternion targetRotation = target.rotation * Quaternion.Euler(element.rotationOffsetEuler);

        if (snapRotation)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, snapSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < snapDistance)
        {
            transform.position = target.position;

            if (snapRotation)
                transform.rotation = targetRotation;

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
    // HELPER METHODS - delegate to SpherometerTargetProvider when present
    // ==========================================

    private GameObject GetActiveHighlightObject(SnapElement element)
    {
        if (spherometerProvider != null && spherometerProvider.HighlightObject != null)
            return spherometerProvider.HighlightObject;

        return element.highlightObject;
    }

    private Collider GetActiveHighlightCollider(SnapElement element)
    {
        if (spherometerProvider != null && spherometerProvider.HighlightCollider != null)
            return spherometerProvider.HighlightCollider;

        return element.highlightCollider;
    }

    private Transform GetActiveTargetTransform(SnapElement element)
    {
        if (spherometerProvider != null)
            return spherometerProvider.GetTargetTransform(element.targetPointIndex);

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