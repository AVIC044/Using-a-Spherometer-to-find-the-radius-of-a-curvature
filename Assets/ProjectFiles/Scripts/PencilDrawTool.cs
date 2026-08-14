using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class PencilDrawTool : MonoBehaviour
{
    // =========================================================
    // LINE TYPE
    // =========================================================

    public enum LineType
    {
        Full,
        Dotted
    }


    // =========================================================
    // DRAW SEGMENT (one start->end pair)
    // =========================================================

    [System.Serializable]
    public class DrawSegment
    {
        [Header("Page")]
        [Tooltip("Only used when 'All Segments On Same Page' is UNCHECKED. " +
                 "This segment becomes active only when this page index is shown.")]
        public int pageIndex;

        [Header("Points")]
        public Transform startPoint;
        public Transform endPoint;

        [Header("Line Type")]
        public LineType lineType = LineType.Full;

        [Header("Drawing Settings")]
        [Tooltip("Distance allowed from start point when beginning.")]
        public float startPointRadius = 0.01f;

        [Tooltip("Distance from end point required to complete.")]
        public float endPointRadius = 0.01f;

        [Header("Dotted Line Settings")]
        public float dotLength = 0.01f;
        public float dotSpacing = 0.01f;

        [Header("Camera On Complete")]
        [Tooltip("Camera moves to match this transform's position & rotation when this segment completes. Leave empty to skip camera move.")]
        public Transform cameraTarget;

        [Tooltip("Seconds to freeze (pencil locked, line locked) at the end point BEFORE the camera starts moving.")]
        public float holdAtEndDuration = 0.3f;

        [Tooltip("Seconds for the camera move.")]
        public float cameraMoveDuration = 1f;

        [Header("Events")]
        public UnityEvent onSegmentComplete;

        [Header("Persistence")]
        [Tooltip("After this segment is completed, its line stays visible through this page index (inclusive, 0-based). Set to -1 to keep old behavior (visible only while its own page is active).")]
        public int visibleUntilPageIndex = -1;


        // ---- runtime state (not shown in inspector) ----

        [System.NonSerialized] public bool completed;
        [System.NonSerialized] public float currentDistance;
        [System.NonSerialized] public GameObject lineObject;
        [System.NonSerialized] public LineRenderer lineRenderer;
        [System.NonSerialized] public Vector3 direction;
        [System.NonSerialized] public float length;
        [System.NonSerialized] public float surfaceY;
        [System.NonSerialized] public bool dataCalculated;
    }


    // =========================================================
    // PENCIL REFERENCES
    // =========================================================

    [Header("Pencil References")]
    public Transform pencilObject;
    public Transform pencilTip;


    // =========================================================
    // INPUT SETTINGS
    // =========================================================

    [Header("Input Settings")]
    [Tooltip("If enabled, the user must touch/tap directly on the pencil to begin dragging it.")]
    public bool requirePencilClick = true;
    public LayerMask pencilLayer = ~0;


    // =========================================================
    // PAGE MODE
    // =========================================================

    [Header("Page Mode")]

    [Tooltip("TRUE  = all segments below live on ONE page (set that page in 'Common Page Index'). " +
             "FALSE = each segment uses its OWN 'Page Index' field, so segments can be spread across different pages.")]
    public bool allSegmentsOnSamePage = true;

    [Tooltip("Used only when 'All Segments On Same Page' is TRUE.")]
    public int commonPageIndex = 0;


    // =========================================================
    // SEGMENTS (in completion order)
    // =========================================================

    [Header("Draw Segments (in completion order)")]
    public List<DrawSegment> segments = new List<DrawSegment>();


    // =========================================================
    // LINE SETTINGS
    // =========================================================

    [Header("Line Settings")]
    public float lineWidth = 0.004f;
    public float surfaceOffset = 0.003f;
    public Color lineColor = Color.black;


    // =========================================================
    // CAMERA SETTINGS
    // =========================================================

    [Header("Camera")]
    [Tooltip("Camera that gets moved on segment completion. Defaults to Camera.main if left empty.")]
    public Camera targetCamera;

    public AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    // =========================================================
    // DOT SETTINGS
    // =========================================================

    [Header("Start Dot Settings")]
    public bool showDots = false;
    public char dotCharacter = 'P';
    public Vector3 charOffset = new Vector3(0.002f, 0f, 0f);


    // =========================================================
    // EVENTS
    // =========================================================

    [Header("Events")]
    [Tooltip("Fires once after the last segment for the CURRENT page's sequence completes (and its camera move finishes).")]
    public UnityEvent onAllSegmentsComplete;


    // =========================================================
    // PUBLIC RUNTIME VALUE
    // =========================================================

    [HideInInspector]
    public float currentDistance;

    [HideInInspector]
    public int currentSegmentIndex = -1; // index into activePageSegments


    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool isDragging = false;
    private bool cameraMoving = false;

    private GameObject dotObj;
    private GameObject charObj;

    private Camera mainCamera;

    private int currentPageIndex = -1;

    // Ordered list of segments that belong to the currently shown page.
    private List<DrawSegment> activePageSegments = new List<DrawSegment>();

    // Remembers how far each page had progressed (segment index) so revisiting a page resumes correctly.
    private Dictionary<int, int> pageProgress = new Dictionary<int, int>();


    // =========================================================
    // CURRENT SEGMENT HELPER
    // =========================================================

    private DrawSegment CurrentSegment
    {
        get
        {
            if (currentSegmentIndex >= 0 && currentSegmentIndex < activePageSegments.Count)
                return activePageSegments[currentSegmentIndex];

            return null;
        }
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        mainCamera = Camera.main;

        if (targetCamera == null)
            targetCamera = mainCamera;


        if (pencilObject == null)
        {
            Debug.LogError("PencilDrawTool: Pencil Object is not assigned.", this);
            return;
        }

        if (pencilTip == null)
        {
            Debug.LogError("PencilDrawTool: Pencil Tip is not assigned.", this);
            return;
        }
        // new method for line persistence: if a segment has a visibleUntilPageIndex >= 0, it will remain visible until that page index is reached. This allows for more complex drawing sequences across multiple pages.


        // -----------------------------------------------------
        // Subscribe to page navigation
        // -----------------------------------------------------

        PageNavigationController.OnPageChanged += OnPageChanged;


        // -----------------------------------------------------
        // Load whatever page is currently shown
        // -----------------------------------------------------

        OnPageChanged(PageNavigationController.CurrentIndex);
    }


    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!requirePencilClick)
            return;

        if (cameraMoving)
            return; // block pencil input while camera is transitioning

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        if (activePageSegments.Count == 0)
            return; // nothing to draw on this page


        // -----------------------------------------------------
        // Ignore input if the finger/mouse is over UI
        // (prevents drag starting through buttons/panels).
        // -----------------------------------------------------

        if (IsPointerOverUI())
        {
            if (isDragging)
                StopDrawing();

            return;
        }


        if (GetPointerDown())
        {
            TryStartDrawing();
        }

        if (GetPointerUp())
        {
            StopDrawing();
        }

        if (GetPointerHeld() && isDragging)
        {
            ContinueDrawing();
        }
    }


    // =========================================================
    // POINTER INPUT (touch on device, mouse fallback in editor)
    // =========================================================

    private bool GetPointerDown()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.touchCount == 0)
            return Input.GetMouseButtonDown(0);
#endif

        if (Input.touchCount > 0)
            return Input.GetTouch(0).phase == TouchPhase.Began;

        return false;
    }


    private bool GetPointerUp()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.touchCount == 0)
            return Input.GetMouseButtonUp(0);
#endif

        if (Input.touchCount > 0)
        {
            TouchPhase phase = Input.GetTouch(0).phase;

            return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
        }

        // Finger lifted completely (no touches at all) while we were dragging.
        return isDragging && Input.touchCount == 0;
    }


    private bool GetPointerHeld()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.touchCount == 0)
            return Input.GetMouseButton(0);
#endif

        if (Input.touchCount > 0)
        {
            TouchPhase phase = Input.GetTouch(0).phase;

            return phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
        }

        return false;
    }


    private Vector3 GetPointerScreenPosition()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;

        return Input.mousePosition;
    }


    private bool IsPointerOverUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;

        if (Input.touchCount > 0)
        {
            int fingerId = Input.GetTouch(0).fingerId;

            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }


    // =========================================================
    // GET SEGMENT'S PAGE INDEX
    // =========================================================

    private int GetSegmentPageIndex(DrawSegment seg)
    {
        if (allSegmentsOnSamePage)
            return commonPageIndex;

        return seg.pageIndex;
    }

    private void UpdateAllLineVisibility(int pageIndex)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            DrawSegment seg = segments[i];

            if (seg == null || seg.lineObject == null)
                continue;

            int segPage = GetSegmentPageIndex(seg);

            bool isOwnPageActive = (segPage == pageIndex);

            bool isPersisted =
                seg.completed &&
                seg.visibleUntilPageIndex >= pageIndex &&
                pageIndex >= segPage;

            seg.lineObject.SetActive(isOwnPageActive || isPersisted);
        }
    }
    // =========================================================
    // PAGE CHANGED
    // =========================================================

    private void OnPageChanged(int pageIndex)
    {
        // -----------------------------------------------------
        // Save progress of the page we're leaving
        // -----------------------------------------------------

        if (currentPageIndex != -1)
        {
            pageProgress[currentPageIndex] = currentSegmentIndex;

        }


        isDragging = false;

        currentPageIndex = pageIndex;


        // -----------------------------------------------------
        // Build the ordered segment list for this page
        // -----------------------------------------------------

        activePageSegments.Clear();

        for (int i = 0; i < segments.Count; i++)
        {
            DrawSegment seg = segments[i];

            if (seg == null)
                continue;

            if (seg.startPoint == null || seg.endPoint == null)
            {
                Debug.LogError("PencilDrawTool: Segment " + i + " missing Start/End Point.", this);
                continue;
            }

            if (GetSegmentPageIndex(seg) != pageIndex)
                continue;

            if (!seg.dataCalculated)
            {
                CalculateSegmentData(seg);
                seg.dataCalculated = true;
            }

            activePageSegments.Add(seg);
        }


        if (activePageSegments.Count == 0)
        {
            currentSegmentIndex = -1;

            ClearMarkers();

            Debug.Log("PencilDrawTool: Page " + pageIndex + " has no draw segments.");

            return;
        }


        // -----------------------------------------------------
        // Resume from saved progress, or start at 0
        // -----------------------------------------------------

        int resumeIndex;

        if (!pageProgress.TryGetValue(pageIndex, out resumeIndex))
        {
            resumeIndex = 0;
        }

        resumeIndex = Mathf.Clamp(resumeIndex, 0, activePageSegments.Count - 1);


        UpdateAllLineVisibility(pageIndex);

        ActivateSegment(resumeIndex);
    }


    // =========================================================
    // CALCULATE SEGMENT DATA
    // =========================================================

    private void CalculateSegmentData(DrawSegment seg)
    {
        seg.direction = (seg.endPoint.position - seg.startPoint.position).normalized;

        seg.length = Vector3.Distance(seg.startPoint.position, seg.endPoint.position);

        seg.surfaceY = seg.startPoint.position.y + surfaceOffset;
    }


    // =========================================================
    // SURFACE POSITION
    // =========================================================

    private Vector3 GetSurfacePosition(Vector3 position, float surfaceY)
    {
        position.y = surfaceY;
        return position;
    }


    // =========================================================
    // SHOW / HIDE LINES FOR ACTIVE PAGE
    // =========================================================

    private void ShowActivePageLines()
    {
        for (int i = 0; i < activePageSegments.Count; i++)
        {
            GameObject lineObj = activePageSegments[i].lineObject;

            if (lineObj != null)
                lineObj.SetActive(true);
        }
    }


    private void HideActivePageLines()
    {
        for (int i = 0; i < activePageSegments.Count; i++)
        {
            GameObject lineObj = activePageSegments[i].lineObject;

            if (lineObj != null)
                lineObj.SetActive(false);
        }
    }


    // =========================================================
    // ACTIVATE SEGMENT (index within activePageSegments)
    // =========================================================

    private void ActivateSegment(int index)
    {
        currentSegmentIndex = index;

        DrawSegment seg = CurrentSegment;

        if (seg == null)
        {
            // All segments for this page are done.
            currentDistance = 0f;

            ClearMarkers();

            onAllSegmentsComplete?.Invoke();

            return;
        }

        CreateSegmentLine(seg);

        // If this segment was already completed earlier (revisiting page), just restore it.
        if (seg.completed)
        {
            currentDistance = seg.length;

            Vector3 endPos = GetSurfacePosition(seg.endPoint.position, seg.surfaceY);
            Vector3 tipOffsetR = pencilTip.position - pencilObject.position;
            pencilObject.position = endPos - tipOffsetR;

            UpdateSegmentLine(seg);

            ClearMarkers();

            return;
        }

        MovePencilToSegmentStart(seg);

        ClearMarkers();

        if (showDots)
        {
            Vector3 start = GetSurfacePosition(seg.startPoint.position, seg.surfaceY);
            CreateDot(start);
            CreateCharacter(start);
        }
    }


    // =========================================================
    // MOVE PENCIL TO SEGMENT START
    // =========================================================

    private void MovePencilToSegmentStart(DrawSegment seg)
    {
        if (pencilObject == null || pencilTip == null || seg == null || seg.startPoint == null)
            return;

        Vector3 targetTipPosition = GetSurfacePosition(seg.startPoint.position, seg.surfaceY);

        Vector3 tipOffset = pencilTip.position - pencilObject.position;

        pencilObject.position = targetTipPosition - tipOffset;

        seg.currentDistance = 0f;

        currentDistance = 0f;

        UpdateSegmentLine(seg);
    }


    // =========================================================
    // TRY START DRAWING
    // =========================================================

    private void TryStartDrawing()
    {
        DrawSegment seg = CurrentSegment;

        if (seg == null || seg.completed)
            return;

        if (pencilObject == null || pencilTip == null)
            return;


        Ray ray = mainCamera.ScreenPointToRay(GetPointerScreenPosition());

        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, pencilLayer))
            return;


        Transform hitTransform = hit.transform;

        bool touchedPencil =
            hitTransform == pencilObject ||
            hitTransform.IsChildOf(pencilObject);

        if (!touchedPencil)
            return;


        isDragging = true;

        // Snap the pencil onto the line immediately under the finger,
        // instead of waiting for the next drag frame.
        ContinueDrawing();
    }


    // =========================================================
    // CONTINUE DRAWING (forward + backward supported)
    // =========================================================

    private void ContinueDrawing()
    {
        DrawSegment seg = CurrentSegment;

        if (!isDragging || seg == null || seg.completed)
            return;

        if (pencilObject == null || pencilTip == null || mainCamera == null)
            return;


        // -----------------------------------------------------
        // Finger/mouse -> world position at the segment's depth
        // -----------------------------------------------------

        Vector3 pointer = GetPointerScreenPosition();

        Vector3 screenStart = mainCamera.WorldToScreenPoint(seg.startPoint.position);

        pointer.z = screenStart.z;

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(pointer);


        // -----------------------------------------------------
        // Project onto the segment line (allows forward AND
        // backward dragging - distance can go up or down).
        // -----------------------------------------------------

        Vector3 fromStart = worldPos - seg.startPoint.position;

        float distance = Vector3.Dot(fromStart, seg.direction);

        distance = Mathf.Clamp(distance, 0f, seg.length);

        seg.currentDistance = distance;

        currentDistance = distance;


        // -----------------------------------------------------
        // Move the pencil to that distance along the line
        // -----------------------------------------------------

        Vector3 desiredTipPosition = seg.startPoint.position + seg.direction * distance;

        desiredTipPosition.y = seg.surfaceY;

        Vector3 tipOffset = pencilTip.position - pencilObject.position;

        pencilObject.position = desiredTipPosition - tipOffset;


        // -----------------------------------------------------
        // Rebuild the line from scratch each frame based on
        // currentDistance - backward drags correctly shrink it.
        // -----------------------------------------------------

        UpdateSegmentLine(seg);


        // -----------------------------------------------------
        // Check completion
        // -----------------------------------------------------

        if (!seg.completed && distance >= seg.length - seg.endPointRadius)
        {
            CompleteSegment(seg);
        }
    }


    // =========================================================
    // STOP DRAWING
    // =========================================================

    private void StopDrawing()
    {
        isDragging = false;
    }


    // =========================================================
    // CREATE SEGMENT LINE
    // =========================================================

    private void CreateSegmentLine(DrawSegment seg)
    {
        if (seg.lineRenderer != null)
        {
            seg.lineObject.SetActive(true);
            return;
        }

        GameObject lineObj = new GameObject("DrawLine_Page_" + GetSegmentPageIndex(seg) + "_Seg_" + segments.IndexOf(seg));

        LineRenderer renderer = lineObj.AddComponent<LineRenderer>();

        renderer.startWidth = lineWidth;
        renderer.endWidth = lineWidth;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            renderer.material = new Material(shader);
        }
        else
        {
            Debug.LogError("PencilDrawTool: Could not find suitable shader.", this);
        }

        renderer.startColor = lineColor;
        renderer.endColor = lineColor;
        renderer.alignment = LineAlignment.View;
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 4;

        seg.lineObject = lineObj;
        seg.lineRenderer = renderer;
    }


    // =========================================================
    // UPDATE SEGMENT LINE (rebuilt from 0 -> currentDistance)
    // =========================================================

    private void UpdateSegmentLine(DrawSegment seg)
    {
        if (seg.lineRenderer == null)
            return;

        if (seg.lineType == LineType.Full)
        {
            UpdateFullLine(seg);
        }
        else
        {
            UpdateDottedLine(seg);
        }
    }


    // =========================================================
    // FULL LINE
    // =========================================================

    private void UpdateFullLine(DrawSegment seg)
    {
        LineRenderer renderer = seg.lineRenderer;

        if (seg.currentDistance <= 0f)
        {
            renderer.positionCount = 0;
            return;
        }

        Vector3 start = GetSurfacePosition(seg.startPoint.position, seg.surfaceY);
        Vector3 end = start + seg.direction * seg.currentDistance;

        renderer.positionCount = 2;
        renderer.SetPositions(new Vector3[] { start, end });
    }


    // =========================================================
    // DOTTED LINE
    // =========================================================

    private void UpdateDottedLine(DrawSegment seg)
    {
        LineRenderer renderer = seg.lineRenderer;

        if (seg.currentDistance <= 0f)
        {
            renderer.positionCount = 0;
            return;
        }

        List<Vector3> dottedPoints = new List<Vector3>();

        Vector3 start = GetSurfacePosition(seg.startPoint.position, seg.surfaceY);

        float dotLength = Mathf.Max(0.0001f, seg.dotLength);
        float dotSpacing = Mathf.Max(0f, seg.dotSpacing);

        float travelled = 0f;

        while (travelled < seg.currentDistance)
        {
            float remaining = seg.currentDistance - travelled;

            float currentDotLength = Mathf.Min(dotLength, remaining);

            Vector3 dotStart = start + seg.direction * travelled;
            Vector3 dotEnd = dotStart + seg.direction * currentDotLength;

            dottedPoints.Add(dotStart);
            dottedPoints.Add(dotEnd);

            travelled += dotLength + dotSpacing;
        }

        renderer.positionCount = dottedPoints.Count;

        if (dottedPoints.Count > 0)
        {
            renderer.SetPositions(dottedPoints.ToArray());
        }
    }


    // =========================================================
    // COMPLETE SEGMENT
    // =========================================================

    private void CompleteSegment(DrawSegment seg)
    {
        seg.completed = true;
        isDragging = false;
        seg.currentDistance = seg.length;
        currentDistance = seg.length;


        // -----------------------------------------------------
        // Snap pencil exactly to end point
        // -----------------------------------------------------

        Vector3 targetEnd = GetSurfacePosition(seg.endPoint.position, seg.surfaceY);

        Vector3 tipOffset = pencilTip.position - pencilObject.position;

        pencilObject.position = targetEnd - tipOffset;


        UpdateSegmentLine(seg);


        // -----------------------------------------------------
        // Lock all pencil/line input right now - nothing moves
        // again until the next segment's start point is snapped to.
        // -----------------------------------------------------

        cameraMoving = true;


        // Save progress immediately in case the page changes mid-sequence.
        pageProgress[currentPageIndex] = currentSegmentIndex;


        seg.onSegmentComplete?.Invoke();


        // -----------------------------------------------------
        // Hold at the end point, then move camera, then advance
        // -----------------------------------------------------

        StartCoroutine(EndOfSegmentRoutine(seg));
    }


    // =========================================================
    // END OF SEGMENT ROUTINE (hold -> camera move -> next segment)
    // =========================================================

    private IEnumerator EndOfSegmentRoutine(DrawSegment seg)
    {
        // -----------------------------------------------------
        // 1) Freeze here - pencil locked, line locked at the
        //    completed end point. Nothing responds to touch.
        // -----------------------------------------------------

        float hold = Mathf.Max(0f, seg.holdAtEndDuration);

        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }


        // -----------------------------------------------------
        // 2) Move the camera (if this segment has a target)
        // -----------------------------------------------------

        if (seg.cameraTarget != null)
        {
            yield return StartCoroutine(MoveCameraRoutine(seg));
        }


        // -----------------------------------------------------
        // 3) Unlock and snap to the next segment's start point
        // -----------------------------------------------------

        cameraMoving = false;

        AdvanceToNextSegment();
    }


    // =========================================================
    // CAMERA MOVE ROUTINE (pure camera lerp - locking/advancing
    // is handled by the caller, EndOfSegmentRoutine)
    // =========================================================

    private IEnumerator MoveCameraRoutine(DrawSegment seg)
    {
        Camera cam = targetCamera != null ? targetCamera : mainCamera;

        if (cam == null || seg.cameraTarget == null)
        {
            yield break;
        }

        Transform camT = cam.transform;

        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        Vector3 endPos = seg.cameraTarget.position;
        Quaternion endRot = seg.cameraTarget.rotation;

        float duration = Mathf.Max(0.01f, seg.cameraMoveDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float normalized = Mathf.Clamp01(t / duration);
            float eval = cameraMoveCurve.Evaluate(normalized);

            camT.position = Vector3.Lerp(startPos, endPos, eval);
            camT.rotation = Quaternion.Slerp(startRot, endRot, eval);

            yield return null;
        }

        camT.position = endPos;
        camT.rotation = endRot;
    }


    // =========================================================
    // ADVANCE TO NEXT SEGMENT (within current page's sequence)
    // =========================================================

    private void AdvanceToNextSegment()
    {
        int next = currentSegmentIndex + 1;

        pageProgress[currentPageIndex] = next;

        ActivateSegment(next);
    }


    // =========================================================
    // CLEAR CURRENT SEGMENT
    // =========================================================

    public void ClearCurrentSegment()
    {
        DrawSegment seg = CurrentSegment;

        if (seg == null)
            return;

        if (seg.lineObject != null)
        {
            Destroy(seg.lineObject);
            seg.lineObject = null;
            seg.lineRenderer = null;
        }

        seg.completed = false;
        seg.currentDistance = 0f;
        currentDistance = 0f;
        isDragging = false;

        MovePencilToSegmentStart(seg);
    }


    // =========================================================
    // CLEAR ALL SEGMENTS ON THE CURRENT PAGE
    // =========================================================

    public void ClearCurrentPageSegments()
    {
        for (int i = 0; i < activePageSegments.Count; i++)
        {
            DrawSegment seg = activePageSegments[i];

            if (seg.lineObject != null)
            {
                Destroy(seg.lineObject);
                seg.lineObject = null;
                seg.lineRenderer = null;
            }

            seg.completed = false;
            seg.currentDistance = 0f;
        }

        currentDistance = 0f;
        isDragging = false;

        pageProgress[currentPageIndex] = 0;

        ActivateSegment(0);
    }


    // =========================================================
    // CLEAR EVERYTHING (all pages, all segments)
    // =========================================================

    public void ClearAllSegments()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            DrawSegment seg = segments[i];

            if (seg == null)
                continue;

            if (seg.lineObject != null)
            {
                Destroy(seg.lineObject);
                seg.lineObject = null;
                seg.lineRenderer = null;
            }

            seg.completed = false;
            seg.currentDistance = 0f;
        }

        pageProgress.Clear();

        currentDistance = 0f;
        isDragging = false;

        OnPageChanged(currentPageIndex);
    }


    // =========================================================
    // CLEAR MARKERS
    // =========================================================

    private void ClearMarkers()
    {
        if (dotObj != null)
        {
            Destroy(dotObj);
            dotObj = null;
        }

        if (charObj != null)
        {
            Destroy(charObj);
            charObj = null;
        }
    }


    // =========================================================
    // CREATE DOT
    // =========================================================

    private void CreateDot(Vector3 pos)
    {
        dotObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        dotObj.transform.position = pos;
        dotObj.transform.localScale = Vector3.one * 0.0015f;

        Renderer renderer = dotObj.GetComponent<Renderer>();

        if (renderer != null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
                renderer.material = new Material(shader);

            renderer.material.color = Color.black;
        }

        Collider collider = dotObj.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }
    }


    // =========================================================
    // CREATE CHARACTER
    // =========================================================

    private void CreateCharacter(Vector3 pos)
    {
        charObj = new GameObject("DotCharacter");

        charObj.transform.position = pos + charOffset;

        TextMesh textMesh = charObj.AddComponent<TextMesh>();

        textMesh.text = dotCharacter.ToString();
        textMesh.fontSize = 100;
        textMesh.characterSize = 0.0008f;
        textMesh.color = Color.black;
        textMesh.anchor = TextAnchor.MiddleCenter;

        if (Camera.main != null)
        {
            charObj.transform.rotation = Camera.main.transform.rotation;
        }
    }
}