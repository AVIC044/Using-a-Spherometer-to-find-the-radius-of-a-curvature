using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Events;

public class SpherometerSequenceController : MonoBehaviour
{
    public enum SequenceStage
    {
        Inactive,
        PrickPage_Waiting,
        PrickPage_Pricking,
        PrickPage_DotsShown,
        PutAsidePage_Waiting,
        PutAsidePage_Moving,
        Complete
    }

    [Header("Spherometer")]
    public GameObject spherometerObject;
    public Transform prickTargetTransform;      // optional: small dip/rotate on prick
    public float prickDuration = 0.5f;

    [Header("Dots")]
    public GameObject dotsCanvas;
    public float dotAppearDelay = 0.2f;

    [Header("Put Aside")]
    public Transform offCameraPosition;
    public float moveAwayDuration = 1.2f;

    [Header("Drawing Controller")]
    public ScalePencilController scalePencilController;

    [Header("Camera")]
    public Camera mainCamera;
    public Transform postDotsCameraTarget;
    public float cameraMoveDuration = 1f;
    public AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Page Sync")]
    public int prickPageIndex = 0;
    public int putAsidePageIndex = 1;
    public bool resetOnPageLeave = true;

    [Header("Events")]
    public UnityEvent onSpherometerPricked;
    public UnityEvent onDotsShown;
    public UnityEvent onSpherometerPutAside;
    public UnityEvent onDrawingPhaseReady;

    private SequenceStage currentStage = SequenceStage.Inactive;
    private Camera activeCamera;
    private int currentPageIndex = -1;

    // captured transform after drag-drop
    private Vector3 paperPosition;
    private Quaternion paperRotation;
    private Vector3 paperScale;

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
        activeCamera = mainCamera != null ? mainCamera : Camera.main;
        if (dotsCanvas != null) dotsCanvas.SetActive(false);
    }

    private void Update()
    {
        if (activeCamera == null) activeCamera = Camera.main;

        if (currentStage == SequenceStage.PrickPage_Waiting ||
            currentStage == SequenceStage.PutAsidePage_Waiting)
        {
            HandleInput();
        }
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;

        if (resetOnPageLeave &&
            currentPageIndex != prickPageIndex &&
            currentPageIndex != putAsidePageIndex)
        {
            if (currentStage != SequenceStage.Inactive)
                ResetSequence();
            return;
        }

        if (pageIndex == prickPageIndex)
            EnterPrickPage();
        else if (pageIndex == putAsidePageIndex)
            EnterPutAsidePage();
    }

    private void EnterPrickPage()
    {
        if (spherometerObject != null)
        {
            paperPosition = spherometerObject.transform.position;
            paperRotation = spherometerObject.transform.rotation;
            paperScale = spherometerObject.transform.localScale;
        }

        if (dotsCanvas != null) dotsCanvas.SetActive(false);
        currentStage = SequenceStage.PrickPage_Waiting;
    }

    private void EnterPutAsidePage()
    {
        if (spherometerObject != null)
        {
            spherometerObject.transform.position = paperPosition;
            spherometerObject.transform.rotation = paperRotation;
            spherometerObject.transform.localScale = paperScale;
            spherometerObject.SetActive(true);
        }

        if (dotsCanvas != null) dotsCanvas.SetActive(true);
        currentStage = SequenceStage.PutAsidePage_Waiting;
    }

    private void HandleInput()
    {
        Vector2? screenPos = null;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            screenPos = Mouse.current.position.ReadValue();
        else if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
                screenPos = touch.position.ReadValue();
        }

        if (!screenPos.HasValue) return;

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (Input.touchCount > 0)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                    return;
            }
            else if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;
        }

        Ray ray = activeCamera.ScreenPointToRay(screenPos.Value);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        if (!IsPartOfObject(hit.collider.gameObject, spherometerObject)) return;

        if (currentStage == SequenceStage.PrickPage_Waiting)
            OnPrickPageClicked();
        else if (currentStage == SequenceStage.PutAsidePage_Waiting)
            OnPutAsidePageClicked();
    }

    private void OnPrickPageClicked()
    {
        currentStage = SequenceStage.PrickPage_Pricking;
        onSpherometerPricked?.Invoke();
        StartCoroutine(PrickRoutine());
    }

    private IEnumerator PrickRoutine()
    {
        if (prickTargetTransform != null)
        {
            yield return StartCoroutine(AnimateSpherometerTo(
                prickTargetTransform.position,
                prickTargetTransform.rotation,
                prickTargetTransform.localScale,
                prickDuration));
        }

        if (postDotsCameraTarget != null)
            yield return StartCoroutine(MoveCameraRoutine(postDotsCameraTarget));

        yield return new WaitForSeconds(dotAppearDelay);

        if (dotsCanvas != null)
        {
            dotsCanvas.SetActive(true);
            dotsCanvas.transform.localScale = Vector3.zero;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                dotsCanvas.transform.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t / 0.3f);
                yield return null;
            }
            dotsCanvas.transform.localScale = Vector3.one;
        }

        onDotsShown?.Invoke();
        currentStage = SequenceStage.PrickPage_DotsShown;
    }

    private void OnPutAsidePageClicked()
    {
        currentStage = SequenceStage.PutAsidePage_Moving;
        onSpherometerPutAside?.Invoke();
        StartCoroutine(PutAsideRoutine());
    }

    private IEnumerator PutAsideRoutine()
    {
        if (spherometerObject != null && offCameraPosition != null)
        {
            yield return StartCoroutine(AnimateSpherometerTo(
                offCameraPosition.position,
                offCameraPosition.rotation,
                offCameraPosition.localScale,
                moveAwayDuration));

            spherometerObject.SetActive(false);
        }

        onDrawingPhaseReady?.Invoke();

        if (scalePencilController != null)
            scalePencilController.enabled = true;

        currentStage = SequenceStage.Complete;
    }

    private IEnumerator AnimateSpherometerTo(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, float duration)
    {
        if (spherometerObject == null) yield break;

        Vector3 startPos = spherometerObject.transform.position;
        Quaternion startRot = spherometerObject.transform.rotation;
        Vector3 startScale = spherometerObject.transform.localScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

            spherometerObject.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            spherometerObject.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            spherometerObject.transform.localScale = Vector3.Lerp(startScale, targetScale, smooth);
            yield return null;
        }

        spherometerObject.transform.position = targetPos;
        spherometerObject.transform.rotation = targetRot;
        spherometerObject.transform.localScale = targetScale;
    }

    private IEnumerator MoveCameraRoutine(Transform target)
    {
        if (activeCamera == null || target == null) yield break;

        Transform camT = activeCamera.transform;
        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        float t = 0f;
        while (t < cameraMoveDuration)
        {
            t += Time.deltaTime;
            float norm = Mathf.Clamp01(t / cameraMoveDuration);
            float eval = cameraMoveCurve.Evaluate(norm);

            camT.position = Vector3.Lerp(startPos, target.position, eval);
            camT.rotation = Quaternion.Slerp(startRot, target.rotation, eval);
            yield return null;
        }

        camT.position = target.position;
        camT.rotation = target.rotation;
    }

    public void ResetSequence()
    {
        StopAllCoroutines();
        currentStage = SequenceStage.Inactive;

        if (spherometerObject != null)
            spherometerObject.SetActive(true);

        if (dotsCanvas != null) dotsCanvas.SetActive(false);

        if (scalePencilController != null)
        {
            scalePencilController.ResetController();
            scalePencilController.enabled = false;
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

    public SequenceStage GetCurrentStage() => currentStage;
}