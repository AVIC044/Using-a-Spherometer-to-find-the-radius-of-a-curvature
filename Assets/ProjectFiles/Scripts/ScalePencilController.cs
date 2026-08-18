using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class ScalePencilController : MonoBehaviour
{
    public enum ControllerState { Idle, MovingScale, MovingPencil, Drawing, Holding, MovingCamera, Waiting, Completed }
    public enum PaperNormalAxis { WorldUp, TransformUp, TransformForward, TransformUpNegative }

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

        [Header("Line Settings")]
        public float lineWidth = 0.004f;
        public Color lineColor = Color.black;
        public float surfaceOffset = 0.003f;

        [Header("Page Persistence")]
        [Tooltip("Page indices where this line segment should stay visible after being drawn. Leave empty to display on all active pages.")]
        public List<int> visiblePageIndices = new List<int>();

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
        [System.NonSerialized] public bool dataCalculated;
        [System.NonSerialized] public Vector3 drawStartPosition;
        [System.NonSerialized] public bool hasDrawStart;
    }

    [Header("Objects")]
    public GameObject scaleObject;
    public GameObject pencilObject;
    public Transform pencilTip;
    public Camera mainCamera;
    public Collider filterPaperCollider;

    [Header("Paper Surface Normal")]
    public PaperNormalAxis paperNormalAxis = PaperNormalAxis.WorldUp;

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

    [Header("Pencil Drawing Orientation")]
    public Vector3 drawingRotationEuler = new Vector3(0f, 0f, -90f);
    public float drawingRotationDuration = 0.15f;

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
    private Vector3 pencilTipLocalOffset;
    private bool hasPencilTipOffset;

    private Vector3 drawingPlaneNormal = Vector3.up;
    private float drawingTipPlaneOffset;
    private bool hasDrawingPlaneState;

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

    private void Awake() => CaptureOriginalTransforms();

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnDisable() => PageNavigationController.OnPageChanged -= HandlePageChanged;

    private void Start()
    {
        activeCamera = targetCamera != null ? targetCamera : Camera.main;
        PreCalculateSegments();
    }

    private void Update()
    {
        if (!isActiveOnCurrentPage) return;
        if (activeCamera == null) activeCamera = Camera.main;

        if (state == ControllerState.MovingScale || state == ControllerState.MovingPencil || state == ControllerState.MovingCamera)
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
        else if (isActiveOnCurrentPage && resetOnPageLeave)
        {
            ResetController();
        }

        isActiveOnCurrentPage = shouldBeActive;
        UpdateSegmentVisibility(pageIndex);
    }

    private void EnterPage()
    {
        StopAllCoroutines();
        state = ControllerState.Idle;
        isDraggingPencil = false;
        hasDrawingPlaneState = false;

        if (currentScalePoseIndex < 0 && scaleObject != null)
        {
            Transform t = scalePageStart != null ? scalePageStart : (hasOriginalScale ? scaleObject.transform : null);
            if (t != null)
            {
                scaleObject.transform.position = t.position;
                scaleObject.transform.rotation = t.rotation;
                scaleObject.transform.localScale = t.localScale;
            }
        }

        if (currentSegmentIndex < 0 && pencilObject != null)
        {
            Transform t = pencilPageStart != null ? pencilPageStart : (hasOriginalPencil ? pencilObject.transform : null);
            if (t != null)
            {
                pencilObject.transform.position = t.position;
                pencilObject.transform.rotation = t.rotation;
                pencilObject.transform.localScale = t.localScale;
            }
        }
    }

    private void UpdateSegmentVisibility(int pageIndex)
    {
        foreach (var seg in drawingSegments)
        {
            if (seg.isComplete && seg.lineObject != null)
            {
                bool isAllowed = seg.visiblePageIndices.Count == 0 || seg.visiblePageIndices.Contains(pageIndex);
                seg.lineObject.SetActive(isAllowed);
            }
        }
    }

    private void HandleMouse()
    {
        if (Mouse.current == null) return;
        Vector2 pos = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame) ProcessPointerDown(pos);
        if (Mouse.current.leftButton.isPressed && isDraggingPencil) ProcessPencilDrag(pos);
        if (Mouse.current.leftButton.wasReleasedThisFrame) ProcessPointerUp();
    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null) return;
        var touch = Touchscreen.current.primaryTouch;
        Vector2 pos = touch.position.ReadValue();

        if (touch.press.wasPressedThisFrame) ProcessPointerDown(pos);
        if (touch.press.isPressed && isDraggingPencil) ProcessPencilDrag(pos);
        if (touch.press.wasReleasedThisFrame) ProcessPointerUp();
    }

    private void ProcessPointerDown(Vector2 screenPosition)
    {
        if (state == ControllerState.MovingScale || state == ControllerState.MovingPencil || state == ControllerState.MovingCamera)
            return;

        Ray ray = activeCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (scaleObject != null && IsPartOfObject(hit.collider.gameObject, scaleObject))
            {
                if (state != ControllerState.Drawing && state != ControllerState.Holding) AdvanceScalePose();
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
        if (state == ControllerState.Drawing) state = ControllerState.Idle;
    }

    private void AdvanceScalePose()
    {
        if (scaleObject == null || !hasOriginalScale) return;

        currentScalePoseIndex++;

        if (currentScalePoseIndex >= scalePoses.Count)
        {
            currentScalePoseIndex = -1;
            Vector3 targetP = scalePageStart != null ? scalePageStart.position : originalScalePosition;
            Quaternion targetR = scalePageStart != null ? scalePageStart.rotation : originalScaleRotation;
            Vector3 targetS = scalePageStart != null ? scalePageStart.localScale : originalScaleScale;
            StartCoroutine(AnimateScaleTo(targetP, targetR, targetS));
            return;
        }

        ScalePose pose = scalePoses[currentScalePoseIndex];
        if (pose.targetTransform == null)
        {
            currentScalePoseIndex--;
            return;
        }

        StartCoroutine(AnimateScaleTo(pose.targetTransform.position, pose.targetTransform.rotation, pose.targetTransform.localScale));

        if (pose.triggersDrawingSegment && pose.triggeredSegmentIndex >= 0)
            TryTriggerSegment(pose.triggeredSegmentIndex);
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

        scaleObject.transform.SetPositionAndRotation(targetPos, targetRot);
        scaleObject.transform.localScale = targetScale;
        state = ControllerState.Idle;
    }

    private void PreCalculateSegments()
    {
        for (int i = 0; i < drawingSegments.Count; i++) CalculateSegmentData(drawingSegments[i]);
    }

    private void CalculateSegmentData(DrawingSegment seg)
    {
        if (seg.startPoint == null || seg.endPoint == null) return;

        Vector3 normal = GetPaperNormal();
        Vector3 delta = seg.endPoint.position - seg.startPoint.position;
        Vector3 flatDelta = Vector3.ProjectOnPlane(delta, normal);

        seg.length = flatDelta.magnitude > 0.0001f ? flatDelta.magnitude : delta.magnitude;
        if (seg.length <= 0.0001f) { seg.dataCalculated = false; return; }

        seg.direction = flatDelta.normalized;
        seg.dataCalculated = true;
    }

    private void TryTriggerSegment(int index)
    {
        if (index < 0 || index >= drawingSegments.Count) return;
        DrawingSegment seg = drawingSegments[index];
        if (seg.isComplete || seg.isAvailable) return;

        seg.isAvailable = true;
        seg.onSegmentStart?.Invoke();
    }

    private void TryStartDrawing()
    {
        for (int i = 0; i < drawingSegments.Count; i++)
        {
            DrawingSegment seg = drawingSegments[i];
            if (seg.isComplete) continue;
            if (seg.requiredScalePoseIndex >= 0 && seg.requiredScalePoseIndex != currentScalePoseIndex) continue;

            BeginDrawingSegment(i);
            return;
        }
    }

    private void BeginDrawingSegment(int i)
    {
        DrawingSegment seg = drawingSegments[i];
        currentSegmentIndex = i;
        isDraggingPencil = true;
        state = ControllerState.Drawing;

        if (pencilObject != null)
        {
            pencilObject.transform.rotation = Quaternion.Euler(drawingRotationEuler);

            if (pencilTip != null)
            {
                pencilTipLocalOffset = pencilObject.transform.InverseTransformPoint(pencilTip.position);
                hasPencilTipOffset = true;

                if (seg.startPoint != null)
                    pencilObject.transform.position = seg.startPoint.position - pencilObject.transform.rotation * pencilTipLocalOffset;
            }
            else
            {
                hasPencilTipOffset = false;
            }

            if (filterPaperCollider != null && pencilTip != null)
            {
                drawingPlaneNormal = GetPaperNormal();
                drawingTipPlaneOffset = Vector3.Dot(pencilTip.position - filterPaperCollider.bounds.center, drawingPlaneNormal);
                hasDrawingPlaneState = true;
            }
            else
            {
                drawingPlaneNormal = GetPaperNormal();
                drawingTipPlaneOffset = 0f;
                hasDrawingPlaneState = false;
            }
        }

        seg.drawStartPosition = pencilTip != null ? pencilTip.position : pencilObject.transform.position;
        seg.hasDrawStart = true;

        CreateProceduralLine(seg);
        UpdateProceduralLine(seg);
        seg.onSegmentStart?.Invoke();
    }

    private void ProcessPencilDrag(Vector2 screenPosition)
    {
        if (!isDraggingPencil || currentSegmentIndex < 0) return;
        DrawingSegment seg = drawingSegments[currentSegmentIndex];
        if (seg.isComplete) return;

        Ray ray = activeCamera.ScreenPointToRay(screenPosition);
        Vector3 target;

        if (filterPaperCollider != null && filterPaperCollider.Raycast(ray, out RaycastHit paperHit, 1000f))
        {
            Vector3 paperNormal = hasDrawingPlaneState ? drawingPlaneNormal : GetPaperNormal();
            float offset = hasDrawingPlaneState ? drawingTipPlaneOffset + pencilPaperOffset : pencilPaperOffset;
            Vector3 desiredTipPosition = paperHit.point + paperNormal * offset;

            target = (pencilTip != null && hasPencilTipOffset)
                ? desiredTipPosition - pencilObject.transform.TransformVector(pencilTipLocalOffset)
                : desiredTipPosition;
        }
        else return;

        pencilObject.transform.position = Vector3.Lerp(pencilObject.transform.position, target, Mathf.Clamp01(Time.deltaTime * dragSpeed));

        UpdateProceduralLine(seg);

        if (pencilTip != null && seg.endPoint != null)
        {
            if (HorizontalDistance(pencilTip.position, seg.endPoint.position) <= seg.startProximityThreshold)
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

            if (pencilPoses.Count > 0 && target == pencilPoses[0].targetTransform)
            {
                Vector3 euler = target.rotation.eulerAngles;
                euler.z = -90f;
                target.rotation = Quaternion.Euler(euler);
            }

            if (target != null && pencilObject != null)
            {
                if (pencilTip != null)
                {
                    Vector3 localTip = pencilObject.transform.InverseTransformPoint(pencilTip.position);
                    pencilObject.transform.position = target.position - pencilObject.transform.rotation * localTip;
                }
                else
                {
                    pencilObject.transform.position = target.position;
                }
            }
        }

        seg.currentDrawDistance = seg.length;
        UpdateProceduralLine(seg);
        seg.onSegmentComplete?.Invoke();
        StartCoroutine(SegmentCompletionRoutine(seg));
    }

    private IEnumerator SegmentCompletionRoutine(DrawingSegment seg)
    {
        if (seg.holdDuration > 0f) yield return new WaitForSeconds(seg.holdDuration);
        if (seg.cameraTarget != null) yield return StartCoroutine(MoveCameraRoutine(seg));
        if (seg.waitAfterCompletion > 0f) yield return new WaitForSeconds(seg.waitAfterCompletion);

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

        float visibleWidth = Mathf.Max(seg.lineWidth, 0.008f);
        lr.startWidth = visibleWidth;
        lr.endWidth = visibleWidth;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null) lr.material = new Material(shader);

        lr.startColor = seg.lineColor;
        lr.endColor = seg.lineColor;
        lr.alignment = LineAlignment.View;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;

        seg.lineObject = lineObj;
        seg.lineRenderer = lr;
    }

    private void UpdateProceduralLine(DrawingSegment seg)
    {
        if (seg.lineRenderer == null || seg.startPoint == null || seg.endPoint == null) return;

        Vector3 normal = GetPaperNormal();
        Vector3 segmentVector = seg.endPoint.position - seg.startPoint.position;
        Vector3 flatSegmentVector = Vector3.ProjectOnPlane(segmentVector, normal);
        float segmentLength = flatSegmentVector.magnitude;

        if (segmentLength <= 0.0001f) return;

        Vector3 direction = flatSegmentVector / segmentLength;
        Vector3 drawingPoint = pencilTip != null ? pencilTip.position : pencilObject.transform.position;
        Vector3 renderStart = seg.hasDrawStart ? seg.drawStartPosition : seg.startPoint.position;

        float distance = Mathf.Clamp(Vector3.Dot(drawingPoint - renderStart, direction), 0f, segmentLength);
        seg.currentDrawDistance = distance;

        Vector3 lineStart = renderStart + normal * seg.surfaceOffset;
        Vector3 lineEnd = lineStart + direction * distance;

        seg.lineRenderer.positionCount = 2;
        seg.lineRenderer.SetPosition(0, lineStart);
        seg.lineRenderer.SetPosition(1, lineEnd);
    }

    private Vector3 GetPaperNormal()
    {
        if (filterPaperCollider == null) return Vector3.up;

        return paperNormalAxis switch
        {
            PaperNormalAxis.TransformUp => filterPaperCollider.transform.up.normalized,
            PaperNormalAxis.TransformForward => filterPaperCollider.transform.forward.normalized,
            PaperNormalAxis.TransformUpNegative => -filterPaperCollider.transform.up.normalized,
            _ => Vector3.up,
        };
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
            float eval = cameraMoveCurve.Evaluate(Mathf.Clamp01(t / duration));
            camT.position = Vector3.Lerp(startPos, seg.cameraTarget.position, eval);
            camT.rotation = Quaternion.Slerp(startRot, seg.cameraTarget.rotation, eval);
            yield return null;
        }

        camT.SetPositionAndRotation(seg.cameraTarget.position, seg.cameraTarget.rotation);
        state = ControllerState.Idle;
    }

    public void ResetController()
    {
        StopAllCoroutines();
        state = ControllerState.Idle;
        isDraggingPencil = false;
        currentScalePoseIndex = -1;
        currentSegmentIndex = -1;
        hasDrawingPlaneState = false;

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
            seg.hasDrawStart = false;

            if (seg.lineObject != null)
            {
                Destroy(seg.lineObject);
                seg.lineObject = null;
                seg.lineRenderer = null;
            }
        }
    }

    private bool IsPartOfObject(GameObject hitObject, GameObject targetObject)
    {
        Transform current = hitObject.transform;
        while (current != null)
        {
            if (current.gameObject == targetObject) return true;
            current = current.parent;
        }
        return false;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    public void SnapPencilToPose(int poseIndex)
    {
        if (poseIndex < 0 || poseIndex >= pencilPoses.Count) return;
        var pose = pencilPoses[poseIndex];
        if (pose.targetTransform != null && pencilObject != null)
            StartCoroutine(AnimatePencilToTipPose(pose.targetTransform, poseIndex == 0));
    }

    private IEnumerator AnimatePencilToTipPose(Transform tipTarget, bool isFirstPose = false)
    {
        state = ControllerState.MovingPencil;

        Quaternion targetRot = tipTarget.rotation;
        if (isFirstPose)
        {
            Vector3 euler = targetRot.eulerAngles;
            euler.z = -90f;
            targetRot = Quaternion.Euler(euler);
        }

        Vector3 startPos = pencilObject.transform.position;
        Quaternion startRot = pencilObject.transform.rotation;
        Vector3 startScale = pencilObject.transform.localScale;

        Vector3 targetPos = tipTarget.position;
        if (pencilTip != null)
        {
            Vector3 localTip = pencilObject.transform.InverseTransformPoint(pencilTip.position);
            targetPos -= targetRot * localTip;
        }

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveDuration));
            pencilObject.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            pencilObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            pencilObject.transform.localScale = Vector3.Lerp(startScale, tipTarget.localScale, smooth);
            yield return null;
        }

        pencilObject.transform.SetPositionAndRotation(targetPos, targetRot);
        pencilObject.transform.localScale = tipTarget.localScale;
        state = ControllerState.Idle;
    }

    public void ActivateSegment(int index) => TryTriggerSegment(index);
    public void NextScalePose() => AdvanceScalePose();
    public ControllerState GetCurrentState() => state;
}