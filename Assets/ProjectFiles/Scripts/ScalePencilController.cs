using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class ScalePencilController : MonoBehaviour
{
    public enum LineMode { None, ToggleImage, Procedural }
    public enum ControllerState { Idle, MovingScale, MovingPencil, Drawing, Holding, MovingCamera, Waiting, Completed }

    [System.Serializable]
    public class ScalePose
    {
        public string poseName = "Pose";
        public Transform targetTransform;
        public bool triggersDrawingSegment = false;
        public int triggeredSegmentIndex = -1;
    }

    [System.Serializable]
    public class PencilPose
    {
        public string poseName = "Pose";
        public Transform targetTransform;
    }

    [System.Serializable]
    public class DrawingSegment
    {
        [Header("Info")]
        public string segmentName = "Segment";

        [Header("Conditions")]
        public int requiredScalePoseIndex = 0;
        public bool requirePencilNearStart = true;
        public float startProximityThreshold = 0.15f;

        [Header("Geometry")]
        public Transform startPoint;
        public Transform endPoint;

        [Header("Line")]
        public LineMode lineMode = LineMode.Procedural;
        public GameObject lineImage;
        public float lineWidth = 0.004f;
        public Color lineColor = Color.black;
        public float surfaceOffset = 0.003f;

        [Header("Completion")]
        public bool snapPencilToEnd = false;
        public Transform endPoseOverride;

        [Header("Camera")]
        public Transform cameraTarget;
        public float holdDuration = 0.3f;
        public float cameraMoveDuration = 1f;

        [Header("Flow")]
        public float waitAfterCompletion = 0.5f;

        [Header("Events")]
        public UnityEvent onSegmentStart;
        public UnityEvent onSegmentComplete;

        [System.NonSerialized] public bool isAvailable;
        [System.NonSerialized] public bool isComplete;
        [System.NonSerialized] public float currentDrawDistance;
        [System.NonSerialized] public GameObject lineObject;
        [System.NonSerialized] public LineRenderer lineRenderer;
        [System.NonSerialized] public Vector3 direction;
        [System.NonSerialized] public float length;
        [System.NonSerialized] public float surfaceY;
        [System.NonSerialized] public bool dataCalculated;
    }

    [Header("Objects")]
    public GameObject scaleObject;
    public GameObject pencilObject;
    public Transform pencilTip;
    public Camera mainCamera;
    public Collider filterPaperCollider;

    [Header("Page Start Positions")]
    public Transform scalePageStart;
    public Transform pencilPageStart;

    [Header("Scale Poses")]
    public List<ScalePose> scalePoses = new List<ScalePose>();

    [Header("Pencil Poses")]
    public List<PencilPose> pencilPoses = new List<PencilPose>();

    [Header("Drawing Segments")]
    public List<DrawingSegment> drawingSegments = new List<DrawingSegment>();

    [Header("Movement")]
    public float dragSpeed = 20f;
    public float moveDuration = 1.5f;
    public float pencilPaperOffset = 0.02f;

    [Header("Camera")]
    public Camera targetCamera;
    public AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Page Sync")]
    public List<int> activePageIndices = new List<int>();
    public bool resetOnPageLeave = true;

    [Header("Events")]
    public UnityEvent onAllSegmentsComplete;

    private ControllerState state = ControllerState.Idle;
    private bool isDraggingPencil = false;
    private int currentScalePoseIndex = -1;
    private int currentSegmentIndex = -1;
    private Vector3 pencilOffset;

    private Camera activeCamera;
    private int currentPageIndex = -1;
    private bool isActiveOnCurrentPage = true;

    private Vector3 originalScalePosition;
    private Quaternion originalScaleRotation;
    private Vector3 originalScaleScale;
    private Vector3 originalPencilPosition;
    private Quaternion originalPencilRotation;
    private Vector3 originalPencilScale;
    private bool hasOriginalScale;
    private bool hasOriginalPencil;

    private void Awake()
    {
        CaptureOriginalTransforms();
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
        activeCamera = targetCamera != null ? targetCamera : Camera.main;
        PreCalculateSegments();
    }

    private void Update()
    {
        if (!isActiveOnCurrentPage) return;
        if (activeCamera == null) activeCamera = Camera.main;

        if (state == ControllerState.MovingScale ||
            state == ControllerState.MovingPencil ||
            state == ControllerState.MovingCamera)
            return;

        HandleMouse();
        HandleTouch();
    }

    private void CaptureOriginalTransforms()
    {
        if (scaleObject != null)
        {
            originalScalePosition = scaleObject.transform.position;
            originalScaleRotation = scaleObject.transform.rotation;
            originalScaleScale = scaleObject.transform.localScale;
            hasOriginalScale = true;
        }

        if (pencilObject != null)
        {
            originalPencilPosition = pencilObject.transform.position;
            originalPencilRotation = pencilObject.transform.rotation;
            originalPencilScale = pencilObject.transform.localScale;
            hasOriginalPencil = true;
        }
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
        bool shouldBeActive = activePageIndices.Count == 0 || activePageIndices.Contains(pageIndex);

        if (shouldBeActive)
        {
            EnterPage();
        }
        else if (!shouldBeActive && isActiveOnCurrentPage && resetOnPageLeave)
        {
            ResetController();
        }

        isActiveOnCurrentPage = shouldBeActive;
    }

    private void EnterPage()
    {
        StopAllCoroutines();
        state = ControllerState.Idle;
        isDraggingPencil = false;
        currentScalePoseIndex = -1;
        currentSegmentIndex = -1;

        if (scaleObject != null)
        {
            if (scalePageStart != null)
            {
                scaleObject.transform.position = scalePageStart.position;
                scaleObject.transform.rotation = scalePageStart.rotation;
                scaleObject.transform.localScale = scalePageStart.localScale;
            }
            else if (hasOriginalScale)
            {
                scaleObject.transform.position = originalScalePosition;
                scaleObject.transform.rotation = originalScaleRotation;
                scaleObject.transform.localScale = originalScaleScale;
            }
        }

        if (pencilObject != null)
        {
            if (pencilPageStart != null)
            {
                pencilObject.transform.position = pencilPageStart.position;
                pencilObject.transform.rotation = pencilPageStart.rotation;
                pencilObject.transform.localScale = pencilPageStart.localScale;
            }
            else if (hasOriginalPencil)
            {
                pencilObject.transform.position = originalPencilPosition;
                pencilObject.transform.rotation = originalPencilRotation;
                pencilObject.transform.localScale = originalPencilScale;
            }
        }

        foreach (var seg in drawingSegments)
        {
            seg.isAvailable = false;
            seg.isComplete = false;
            seg.currentDrawDistance = 0f;

            if (seg.lineObject != null)
            {
                Destroy(seg.lineObject);
                seg.lineObject = null;
                seg.lineRenderer = null;
            }

            if (seg.lineMode == LineMode.ToggleImage && seg.lineImage != null)
                seg.lineImage.SetActive(false);
        }
    }

    private void HandleMouse()
    {
        if (Mouse.current == null) return;

        Vector2 pos = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            ProcessPointerDown(pos);

        if (Mouse.current.leftButton.isPressed && isDraggingPencil)
            ProcessPencilDrag(pos);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            ProcessPointerUp();
    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null) return;

        var touch = Touchscreen.current.primaryTouch;
        Vector2 pos = touch.position.ReadValue();

        if (touch.press.wasPressedThisFrame)
            ProcessPointerDown(pos);

        if (touch.press.isPressed && isDraggingPencil)
            ProcessPencilDrag(pos);

        if (touch.press.wasReleasedThisFrame)
            ProcessPointerUp();
    }

    private void ProcessPointerDown(Vector2 screenPosition)
    {
        if (state == ControllerState.MovingScale ||
            state == ControllerState.MovingPencil ||
            state == ControllerState.MovingCamera)
            return;

        Ray ray = activeCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (scaleObject != null && IsPartOfObject(hit.collider.gameObject, scaleObject))
            {
                if (state != ControllerState.Drawing && state != ControllerState.Holding)
                {
                    AdvanceScalePose();
                }
                return;
            }

            if (pencilObject != null && IsPartOfObject(hit.collider.gameObject, pencilObject))
            {
                TryStartDrawing();
                return;
            }
        }
    }

    private void ProcessPointerUp()
    {
        isDraggingPencil = false;
        if (state == ControllerState.Drawing)
            state = ControllerState.Idle;
    }

    private void AdvanceScalePose()
    {
        if (scaleObject == null || !hasOriginalScale) return;

        currentScalePoseIndex++;

        if (currentScalePoseIndex >= scalePoses.Count)
        {
            currentScalePoseIndex = -1;

            if (scalePageStart != null)
            {
                StartCoroutine(AnimateScaleTo(scalePageStart.position, scalePageStart.rotation, scalePageStart.localScale));
            }
            else if (hasOriginalScale)
            {
                StartCoroutine(AnimateScaleTo(originalScalePosition, originalScaleRotation, originalScaleScale));
            }
            return;
        }

        ScalePose pose = scalePoses[currentScalePoseIndex];
        if (pose.targetTransform == null)
        {
            Debug.LogWarning($"ScalePencilController: Pose '{pose.poseName}' has no target transform. Skipping.");
            currentScalePoseIndex--;
            return;
        }

        StartCoroutine(AnimateScaleTo(
            pose.targetTransform.position,
            pose.targetTransform.rotation,
            pose.targetTransform.localScale
        ));

        if (pose.triggersDrawingSegment && pose.triggeredSegmentIndex >= 0)
        {
            TryTriggerSegment(pose.triggeredSegmentIndex);
        }
    }

    private IEnumerator AnimateScaleTo(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        state = ControllerState.MovingScale;

        Vector3 startPos = scaleObject.transform.position;
        Quaternion startRot = scaleObject.transform.rotation;
        Vector3 startScale = scaleObject.transform.localScale;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));

            scaleObject.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            scaleObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            scaleObject.transform.localScale = Vector3.Lerp(startScale, targetScale, smooth);

            yield return null;
        }

        scaleObject.transform.position = targetPos;
        scaleObject.transform.rotation = targetRot;
        scaleObject.transform.localScale = targetScale;

        state = ControllerState.Idle;
    }

    private void PreCalculateSegments()
    {
        for (int i = 0; i < drawingSegments.Count; i++)
            CalculateSegmentData(drawingSegments[i]);
    }

    private void CalculateSegmentData(DrawingSegment seg)
    {
        if (seg.startPoint == null || seg.endPoint == null) return;

        seg.direction = (seg.endPoint.position - seg.startPoint.position).normalized;
        seg.length = Vector3.Distance(seg.startPoint.position, seg.endPoint.position);
        seg.surfaceY = seg.startPoint.position.y + seg.surfaceOffset;
        seg.dataCalculated = true;
    }

    private void TryTriggerSegment(int index)
    {
        if (index < 0 || index >= drawingSegments.Count) return;

        DrawingSegment seg = drawingSegments[index];
        if (seg.isComplete || seg.isAvailable) return;

        seg.isAvailable = true;

        if (seg.lineMode == LineMode.ToggleImage && seg.lineImage != null)
            seg.lineImage.SetActive(true);

        seg.onSegmentStart?.Invoke();
    }

    private void TryStartDrawing()
    {
        for (int i = 0; i < drawingSegments.Count; i++)
        {
            DrawingSegment seg = drawingSegments[i];
            if (seg.isComplete) continue;

            if (seg.requiredScalePoseIndex >= 0 && seg.requiredScalePoseIndex != currentScalePoseIndex)
                continue;

            if (seg.requirePencilNearStart && seg.startPoint != null)
            {
                float dist = HorizontalDistance(pencilObject.transform.position, seg.startPoint.position);
                if (dist > seg.startProximityThreshold)
                    continue;
            }

            currentSegmentIndex = i;
            isDraggingPencil = true;
            state = ControllerState.Drawing;

            if (seg.lineMode == LineMode.Procedural)
                CreateProceduralLine(seg);

            seg.onSegmentStart?.Invoke();
            return;
        }
    }

    private void ProcessPencilDrag(Vector2 screenPosition)
    {
        if (!isDraggingPencil || currentSegmentIndex < 0) return;

        DrawingSegment seg = drawingSegments[currentSegmentIndex];
        if (seg.isComplete) return;

        if (filterPaperCollider == null) return;

        Ray ray = activeCamera.ScreenPointToRay(screenPosition);
        if (!filterPaperCollider.Raycast(ray, out RaycastHit paperHit, 1000f))
            return;

        Vector3 target = paperHit.point + pencilOffset;

        float paperTopY = filterPaperCollider.bounds.max.y;
        Collider pencilCol = pencilObject.GetComponentInChildren<Collider>();
        if (pencilCol != null)
            target.y = paperTopY + pencilCol.bounds.extents.y + pencilPaperOffset;
        else
            target.y = paperTopY + 0.1f;

        pencilObject.transform.position = Vector3.Lerp(
            pencilObject.transform.position,
            target,
            Time.deltaTime * dragSpeed
        );

        if (seg.lineMode == LineMode.Procedural && seg.dataCalculated)
            UpdateProceduralLine(seg);

        if (pencilTip != null && seg.endPoint != null)
        {
            float distToEnd = HorizontalDistance(pencilTip.position, seg.endPoint.position);
            if (distToEnd <= seg.startProximityThreshold)
                CompleteSegment(seg);
        }
    }

    private void CompleteSegment(DrawingSegment seg)
    {
        seg.isComplete = true;
        isDraggingPencil = false;
        state = ControllerState.Holding;

        if (seg.snapPencilToEnd)
        {
            Transform target = seg.endPoseOverride != null ? seg.endPoseOverride : seg.endPoint;

            // APPLY -90 Z IF SNAPPING TO FIRST PENCIL POSE
            if (pencilPoses.Count > 0 && target == pencilPoses[0].targetTransform)
            {
                Vector3 euler = target.rotation.eulerAngles;
                euler.z = -90f;
                target.rotation = Quaternion.Euler(euler);
            }

            if (target != null && pencilObject != null)
            {
                Vector3 tipOffset = pencilTip != null
                    ? pencilTip.position - pencilObject.transform.position
                    : Vector3.zero;

                pencilObject.transform.position = target.position - tipOffset;
            }
        }

        if (seg.lineMode == LineMode.Procedural)
        {
            seg.currentDrawDistance = seg.length;
            UpdateProceduralLine(seg);
        }

        seg.onSegmentComplete?.Invoke();
        StartCoroutine(SegmentCompletionRoutine(seg));
    }

    private IEnumerator SegmentCompletionRoutine(DrawingSegment seg)
    {
        float hold = Mathf.Max(0f, seg.holdDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        if (seg.cameraTarget != null)
            yield return StartCoroutine(MoveCameraRoutine(seg));

        if (seg.waitAfterCompletion > 0f)
            yield return new WaitForSeconds(seg.waitAfterCompletion);

        bool allComplete = true;
        foreach (var s in drawingSegments)
        {
            if (!s.isComplete) { allComplete = false; break; }
        }

        if (allComplete)
        {
            state = ControllerState.Completed;
            onAllSegmentsComplete?.Invoke();
        }
        else
        {
            state = ControllerState.Idle;
        }
    }

    private void CreateProceduralLine(DrawingSegment seg)
    {
        if (seg.lineRenderer != null) return;

        GameObject lineObj = new GameObject("Line_" + seg.segmentName);
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.startWidth = seg.lineWidth;
        lr.endWidth = seg.lineWidth;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
            lr.material = new Material(shader);
        else
            Debug.LogError("ScalePencilController: Could not find suitable shader.", this);

        lr.startColor = seg.lineColor;
        lr.endColor = seg.lineColor;
        lr.alignment = LineAlignment.View;
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;

        seg.lineObject = lineObj;
        seg.lineRenderer = lr;
    }

    private void UpdateProceduralLine(DrawingSegment seg)
    {
        if (seg.lineRenderer == null || !seg.dataCalculated) return;
        if (pencilTip == null || seg.startPoint == null) return;

        Vector3 fromStart = pencilTip.position - seg.startPoint.position;
        float distance = Vector3.Dot(fromStart, seg.direction);
        distance = Mathf.Clamp(distance, 0f, seg.length);
        seg.currentDrawDistance = distance;

        if (distance <= 0f)
        {
            seg.lineRenderer.positionCount = 0;
            return;
        }

        Vector3 start = GetSurfacePosition(seg.startPoint.position, seg.surfaceY);
        Vector3 end = start + seg.direction * distance;

        seg.lineRenderer.positionCount = 2;
        seg.lineRenderer.SetPositions(new Vector3[] { start, end });
    }

    private Vector3 GetSurfacePosition(Vector3 position, float surfaceY)
    {
        position.y = surfaceY;
        return position;
    }

    private IEnumerator MoveCameraRoutine(DrawingSegment seg)
    {
        if (activeCamera == null || seg.cameraTarget == null) yield break;

        state = ControllerState.MovingCamera;

        Transform camT = activeCamera.transform;
        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        float duration = Mathf.Max(0.01f, seg.cameraMoveDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float norm = Mathf.Clamp01(t / duration);
            float eval = cameraMoveCurve.Evaluate(norm);

            camT.position = Vector3.Lerp(startPos, seg.cameraTarget.position, eval);
            camT.rotation = Quaternion.Slerp(startRot, seg.cameraTarget.rotation, eval);

            yield return null;
        }

        camT.position = seg.cameraTarget.position;
        camT.rotation = seg.cameraTarget.rotation;

        state = ControllerState.Idle;
    }

    public void ResetController()
    {
        StopAllCoroutines();

        state = ControllerState.Idle;
        isDraggingPencil = false;
        currentScalePoseIndex = -1;
        currentSegmentIndex = -1;

        if (scaleObject != null && hasOriginalScale)
        {
            scaleObject.transform.position = originalScalePosition;
            scaleObject.transform.rotation = originalScaleRotation;
            scaleObject.transform.localScale = originalScaleScale;
        }

        if (pencilObject != null && hasOriginalPencil)
        {
            pencilObject.transform.position = originalPencilPosition;
            pencilObject.transform.rotation = originalPencilRotation;
            pencilObject.transform.localScale = originalPencilScale;
        }

        foreach (var seg in drawingSegments)
        {
            seg.isAvailable = false;
            seg.isComplete = false;
            seg.currentDrawDistance = 0f;

            if (seg.lineObject != null)
            {
                Destroy(seg.lineObject);
                seg.lineObject = null;
                seg.lineRenderer = null;
            }

            if (seg.lineMode == LineMode.ToggleImage && seg.lineImage != null)
                seg.lineImage.SetActive(false);
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

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    public void SnapPencilToPose(int poseIndex)
    {
        if (poseIndex < 0 || poseIndex >= pencilPoses.Count) return;
        var pose = pencilPoses[poseIndex];
        if (pose.targetTransform == null || pencilObject == null) return;

        StartCoroutine(AnimatePencilTo(
            pose.targetTransform.position,
            pose.targetTransform.rotation,
            pose.targetTransform.localScale,
            poseIndex == 0
        ));
    }

    private IEnumerator AnimatePencilTo(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, bool isFirstPose = false)
    {
        state = ControllerState.MovingPencil;

        if (isFirstPose)
        {
            Vector3 euler = targetRot.eulerAngles;
            euler.z = -90f;
            targetRot = Quaternion.Euler(euler);
        }

        Vector3 startPos = pencilObject.transform.position;
        Quaternion startRot = pencilObject.transform.rotation;
        Vector3 startScale = pencilObject.transform.localScale;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));

            pencilObject.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            pencilObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            pencilObject.transform.localScale = Vector3.Lerp(startScale, targetScale, smooth);

            yield return null;
        }

        pencilObject.transform.position = targetPos;
        pencilObject.transform.rotation = targetRot;
        pencilObject.transform.localScale = targetScale;

        state = ControllerState.Idle;
    }

    public void ActivateSegment(int index) => TryTriggerSegment(index);
    public void NextScalePose() => AdvanceScalePose();
    public ControllerState GetCurrentState() => state;
}