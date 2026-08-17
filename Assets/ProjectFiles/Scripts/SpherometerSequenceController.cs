using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
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

    public enum PageActionType
    {
        None,
        PrickPage,
        PutAsidePage
    }

    [System.Serializable]
    public class SpherometerPageConfig
    {
        public int pageIndex;
        public PageActionType actionType;

        [Header("Prick Settings")]
        public Transform prickTargetTransform;
        public Transform postDotsCameraTarget;
        public GameObject dotsCanvas;

        [Header("Put Aside Settings")]
        public Transform offCameraPosition;
    }

    [Header("Spherometer")]
    public GameObject spherometerObject;
    public float prickDuration = 0.5f;

    [Header("Prick Default")]
    public Transform prickTargetTransform;

    [Header("Dots Default")]
    public float dotAppearDelay = 0.2f;

    [Header("Put Aside Default")]
    public float moveAwayDuration = 1.2f;

    [Header("Drawing Controller")]
    public ScalePencilController scalePencilController;

    [Header("Camera Defaults")]
    public Camera mainCamera;
    public float cameraMoveDuration = 1f;
    public AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Per-Index Page Configuration")]
    public List<SpherometerPageConfig> pageConfigs = new List<SpherometerPageConfig>();
    public bool resetOnPageLeave = true;

    [Header("Events")]
    public UnityEvent onSpherometerPricked;
    public UnityEvent onDotsShown;
    public UnityEvent onSpherometerPutAside;
    public UnityEvent onDrawingPhaseReady;

    private SequenceStage currentStage = SequenceStage.Inactive;
    private Camera activeCamera;
    private int currentPageIndex = -1;
    private SpherometerPageConfig currentConfig;

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
        DisableAllDotsCanvases();
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
        SpherometerPageConfig config = GetConfigForPage(pageIndex);

        if (config == null || config.actionType == PageActionType.None)
        {
            if (resetOnPageLeave && currentStage != SequenceStage.Inactive)
            {
                ResetSequence();
            }
            return;
        }

        currentConfig = config;

        switch (config.actionType)
        {
            case PageActionType.PrickPage:
                EnterPrickPage(config);
                break;
            case PageActionType.PutAsidePage:
                EnterPutAsidePage(config);
                break;
        }
    }

    private void EnterPrickPage(SpherometerPageConfig config)
    {
        if (spherometerObject != null)
        {
            paperPosition = spherometerObject.transform.position;
            paperRotation = spherometerObject.transform.rotation;
            paperScale = spherometerObject.transform.localScale;
        }

        if (config.dotsCanvas != null) config.dotsCanvas.SetActive(false);
        currentStage = SequenceStage.PrickPage_Waiting;
    }

    private void EnterPutAsidePage(SpherometerPageConfig config)
    {
        if (spherometerObject != null)
        {
            spherometerObject.transform.position = paperPosition;
            spherometerObject.transform.rotation = paperRotation;
            spherometerObject.transform.localScale = paperScale;
            spherometerObject.SetActive(true);
        }

        if (config.dotsCanvas != null) config.dotsCanvas.SetActive(true);
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

        Debug.Log($"[SpherometerSequence] Click detected at screen position {screenPos.Value}. Page={currentPageIndex}, Stage={currentStage}");

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
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);

        foreach (RaycastHit hit in hits)
        {
            Debug.Log($"[SpherometerSequence] Raycast hit: {hit.collider.gameObject.name}");

            if (!IsPartOfObject(hit.collider.gameObject, spherometerObject))
                continue;

            Debug.Log($"[SpherometerSequence] Spherometer hit detected: {hit.collider.gameObject.name}. Page={currentPageIndex}, Stage={currentStage}");

            if (currentStage == SequenceStage.PrickPage_Waiting)
            {
                Debug.Log("[SpherometerSequence] Prick-page click accepted.");
                OnPrickPageClicked();
                return;
            }

            if (currentStage == SequenceStage.PutAsidePage_Waiting)
            {
                Debug.Log("[SpherometerSequence] Put-aside click accepted.");
                OnPutAsidePageClicked();
                return;
            }

            Debug.Log($"[SpherometerSequence] Spherometer clicked, but current stage does not accept input: {currentStage}");
        }

        Debug.Log("[SpherometerSequence] Click raycast did not hit the configured Spherometer object.");
    }

    private void OnPrickPageClicked()
    {
        Debug.Log($"[SpherometerSequence] Prick page clicked on index {currentPageIndex}.");

        GameObject activeDotsCanvas = currentConfig != null ? currentConfig.dotsCanvas : null;

        if (activeDotsCanvas != null)
        {
            activeDotsCanvas.SetActive(true);
            Debug.Log($"[SpherometerSequence] Dots UI enabled: {activeDotsCanvas.name}");
        }
        else
        {
            Debug.LogWarning($"[SpherometerSequence] No Dots Canvas assigned for page {currentPageIndex}.");
        }

        onSpherometerPricked?.Invoke();
        onDotsShown?.Invoke();

        currentStage = SequenceStage.PrickPage_DotsShown;
        Debug.Log($"[SpherometerSequence] Prick page complete on index {currentPageIndex}.");
    }

    private void OnPutAsidePageClicked()
    {
        currentStage = SequenceStage.PutAsidePage_Moving;
        onSpherometerPutAside?.Invoke();
        StartCoroutine(PutAsideRoutine());
    }

    private IEnumerator PutAsideRoutine()
    {
        Transform offCam = currentConfig != null ? currentConfig.offCameraPosition : null;

        if (spherometerObject != null && offCam != null)
        {
            yield return StartCoroutine(AnimateSpherometerTo(
                offCam.position,
                offCam.rotation,
                offCam.localScale,
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

        DisableAllDotsCanvases();

        if (scalePencilController != null)
        {
            scalePencilController.ResetController();
            scalePencilController.enabled = false;
        }

        currentConfig = null;
    }

    private SpherometerPageConfig GetConfigForPage(int pageIndex)
    {
        foreach (var config in pageConfigs)
        {
            if (config != null && config.pageIndex == pageIndex)
                return config;
        }
        return null;
    }

    private void DisableAllDotsCanvases()
    {
        foreach (var config in pageConfigs)
        {
            if (config != null && config.dotsCanvas != null)
                config.dotsCanvas.SetActive(false);
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