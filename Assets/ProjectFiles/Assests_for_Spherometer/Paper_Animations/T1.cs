#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class TouchDragSnap3D : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera mainCamera;
    [Header("Drag Behaviour")]
    [SerializeField] private float cameraPullDistance = 0.3f;
    [Header("Drag Scale")]
    [SerializeField] private float dragScaleMultiplier = 1.4f;
    [SerializeField] private float scaleSpeed = 8f;
    [Header("Snap Settings")]
    [SerializeField] private Transform snapPoint;
    [SerializeField] private float snapRange = 0.5f;
    [SerializeField] private float snapSpeed = 6f;
    [SerializeField] private float returnSpeed = 6f;
    [SerializeField] private Vector3 snapOffset = Vector3.zero;
    [Header("Snap Rotation")]
    [SerializeField] private bool includeSnapRotation = false;
    [SerializeField] private Vector3 snapRotation = Vector3.zero;
    [Header("Raycast Snap")]
    [SerializeField] private bool useRaycastSnap = true;
    [SerializeField] private float raycastDistance = 1.0f;
    [SerializeField] private LayerMask snapLayerMask = ~0;
    [SerializeField] private bool requireRaycastHit = false;
    [Header("Interaction")]
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private bool startInteractable = true;
    [SerializeField] private bool remainInteractableAfterSnap = false;
    [Header("Highlight")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private bool enableHighlight = true;
    [SerializeField] private float blinkInterval = 0.35f;
    [Header("Gizmo Settings")]
    [SerializeField] private float snapPointGizmoSize = 0.15f;
    [Header("Events")]
    [SerializeField] private UnityEvent onSnapped;
    [SerializeField] private UnityEvent onReturned;
    [SerializeField] private bool showGizmos = true;
    public UnityEvent OnSnapped => onSnapped;
    public bool IsInteractable => isInteractable;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private bool isDragging;
    private float dragDistance;
    private Coroutine moveRoutine;
    // ===================== RAYCAST SNAP STATE =========================
    // Cached from the most recent Release() so the debug gizmo can display it without
    // performing its own Physics query every frame.
    private bool lastRaycastEvaluated;
    private bool lastRaycastValid;
    // ===================== HIGHLIGHT STATE =========================
    private Material[] originalMaterials;
    private Material[] highlightMaterialsCache;
    private bool highlightApplied;
    private Coroutine highlightRoutine;
    private WaitForSeconds blinkWait;
    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
        CacheOriginalMaterials();
        isInteractable = startInteractable;
        if (isInteractable)
            StartHighlightBlinking();
    }
    void Update()
    {
#if UNITY_EDITOR
        HandleMouse();
#else
        HandleTouch();
#endif
        HandleScale();
    }
    void HandleScale()
    {
        Vector3 targetScale = startScale;
        if (isDragging)
            targetScale = startScale * dragScaleMultiplier;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed);
    }
    // ===================== INPUT =========================
    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
            TrySelect(Input.mousePosition);
        if (isDragging && Input.GetMouseButton(0))
            Drag(Input.mousePosition);
        if (isDragging && Input.GetMouseButtonUp(0))
            Release();
    }
    void HandleTouch()
    {
        if (Input.touchCount == 0)
            return;
        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
            TrySelect(touch.position);
        if (isDragging &&
            (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
            Drag(touch.position);
        if (isDragging &&
            (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            Release();
    }
    void TrySelect(Vector2 screenPos)
    {
        if (!isInteractable)
            return;
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                StopMoveRoutine();
                dragDistance =
                    Vector3.Distance(mainCamera.transform.position, hit.point);
                isDragging = true;
            }
        }
    }
    void Drag(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Vector3 worldPoint = ray.GetPoint(dragDistance);
        Vector3 camDir =
            (worldPoint - mainCamera.transform.position).normalized;
        transform.position =
            worldPoint - camDir * cameraPullDistance;
    }
    void Release()
    {
        isDragging = false;
        CheckSnap();
    }
    // ===================== SNAP =========================
    void CheckSnap()
    {
        if (snapPoint == null)
        {
            ReturnToStart();
            return;
        }
        float dist =
            Vector3.Distance(transform.position, snapPoint.position);
        bool radiusHit = dist <= snapRange;

        // Raycast check runs at most once here (on Release), never during dragging.
        bool shouldSnap;
        if (useRaycastSnap)
        {
            bool raycastHit = EvaluateSnapRaycast();
            shouldSnap = requireRaycastHit ? raycastHit : (radiusHit || raycastHit);
        }
        else
        {
            shouldSnap = radiusHit;
        }

        if (shouldSnap)
            moveRoutine = StartCoroutine(SnapToPoint());
        else
            ReturnToStart();
    }
    // Casts one ray from the object toward the snap point, length = raycastDistance. Valid if it
    // hits the snap point's own collider OR any collider on snapLayerMask. Caches the result for
    // the debug gizmo so the gizmo never has to raycast on its own.
    bool EvaluateSnapRaycast()
    {
        lastRaycastEvaluated = true;

        if (snapPoint == null)
        {
            lastRaycastValid = false;
            return false;
        }

        Vector3 origin = transform.position;
        Vector3 direction = snapPoint.position - origin;
        if (direction.sqrMagnitude < 0.0000001f)
        {
            // Already effectively at the snap point; nothing meaningful to raycast toward.
            lastRaycastValid = true;
            return true;
        }
        direction.Normalize();

        bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, raycastDistance);
        bool valid = hit && (hitInfo.transform == snapPoint || IsOnSnapLayer(hitInfo.transform.gameObject));

        lastRaycastValid = valid;
        return valid;
    }
    bool IsOnSnapLayer(GameObject go)
    {
        return (snapLayerMask.value & (1 << go.layer)) != 0;
    }
    IEnumerator SnapToPoint()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos =
            snapPoint.position +
            snapPoint.TransformDirection(snapOffset);
        Quaternion targetRot = startRot;
        if (includeSnapRotation)
            targetRot = Quaternion.Euler(snapRotation);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * snapSpeed;
            transform.position =
                Vector3.Lerp(startPos, targetPos, t);
            transform.rotation =
                Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }
        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.localScale = startScale;
        // Successful snap: stop blinking and restore original materials, then apply the
        // post-snap interaction rule. Blinking must never restart automatically after this.
        StopHighlightBlinking();
        if (!remainInteractableAfterSnap)
            isInteractable = false;
        onSnapped?.Invoke();
    }
    // ===================== RETURN =========================
    void ReturnToStart()
    {
        moveRoutine = StartCoroutine(ReturnRoutine());
    }
    IEnumerator ReturnRoutine()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            transform.position =
                Vector3.Lerp(startPos, startPosition, t);
            transform.rotation =
                Quaternion.Lerp(startRot, startRotation, t);
            yield return null;
        }
        transform.position = startPosition;
        transform.rotation = startRotation;
        transform.localScale = startScale;
        onReturned?.Invoke();
    }
    void StopMoveRoutine()
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);
    }
    // ===================== INTERACTION CONTROL =========================
    public void EnableInteraction()
    {
        isInteractable = true;
        StartHighlightBlinking();
    }
    public void DisableInteraction()
    {
        isInteractable = false;
        if (isDragging)
        {
            isDragging = false;
            ReturnToStart();
        }
        StopHighlightBlinking();
    }
    // ===================== HIGHLIGHT =========================
    // The highlight material is never used to replace anything — it's appended to (and removed
    // from) the renderer's existing material array, so Material 0 / Material 1 / etc. are always
    // preserved exactly as authored.
    void CacheOriginalMaterials()
    {
        if (targetRenderer == null)
            return;
        originalMaterials = targetRenderer.sharedMaterials;
        if (highlightMaterial != null)
        {
            highlightMaterialsCache = new Material[originalMaterials.Length + 1];
            System.Array.Copy(originalMaterials, highlightMaterialsCache, originalMaterials.Length);
            highlightMaterialsCache[originalMaterials.Length] = highlightMaterial;
        }
    }
    void StartHighlightBlinking()
    {
        if (!enableHighlight || targetRenderer == null || highlightMaterial == null)
            return;
        StopHighlightBlinking();
        if (blinkWait == null)
            blinkWait = new WaitForSeconds(blinkInterval);
        highlightRoutine = StartCoroutine(BlinkRoutine());
    }
    void StopHighlightBlinking()
    {
        if (highlightRoutine != null)
        {
            StopCoroutine(highlightRoutine);
            highlightRoutine = null;
        }
        SetHighlightState(false);
    }
    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return blinkWait;
            SetHighlightState(true);
            yield return blinkWait;
            SetHighlightState(false);
        }
    }
    void SetHighlightState(bool on)
    {
        if (targetRenderer == null || originalMaterials == null)
            return;
        if (on == highlightApplied)
            return;
        if (on)
        {
            if (highlightMaterialsCache == null)
                return;
            targetRenderer.sharedMaterials = highlightMaterialsCache;
        }
        else
        {
            targetRenderer.sharedMaterials = originalMaterials;
        }
        highlightApplied = on;
    }
#if UNITY_EDITOR
void OnDrawGizmos()
{
    if (!showGizmos || snapPoint == null)
        return;
    // Snap point
    Gizmos.color = Color.green;
    Gizmos.DrawSphere(snapPoint.position, snapPointGizmoSize);
    // Snap range
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(snapPoint.position, snapRange);
    // Line to snap point
    Gizmos.color = Color.cyan;
    Gizmos.DrawLine(transform.position, snapPoint.position);
    // Offset preview
    Vector3 offsetPos = snapPoint.position + snapPoint.TransformDirection(snapOffset);
    Gizmos.color = Color.magenta;
    Gizmos.DrawSphere(offsetPos, snapPointGizmoSize * 0.8f);
    Gizmos.DrawLine(snapPoint.position, offsetPos);
    // Raycast snap preview: line geometry is always live (cheap vector math, no Physics call),
    // colored using the result cached from the most recent actual Release()-time raycast.
    if (useRaycastSnap)
    {
        Vector3 origin = transform.position;
        Vector3 dir = snapPoint.position - origin;
        if (dir.sqrMagnitude > 0.0000001f)
        {
            dir.Normalize();
            Vector3 rayEnd = origin + dir * raycastDistance;
            Gizmos.color = !lastRaycastEvaluated ? Color.gray : (lastRaycastValid ? Color.green : Color.red);
            Gizmos.DrawLine(origin, rayEnd);
            Gizmos.DrawWireSphere(rayEnd, snapPointGizmoSize * 0.5f);
        }
    }
}
#endif
}
