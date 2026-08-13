using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ScalePencilTriangleController : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] private GameObject scaleObject;
    [SerializeField] private GameObject pencilObject;

    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Filter Paper")]
    [SerializeField] private Collider filterPaperCollider;

    [Header("Line Images")]
    [SerializeField] private GameObject line1Image; // A -> B
    [SerializeField] private GameObject line2Image; // B -> C
    [SerializeField] private GameObject line3Image; // A -> C

    [Header("Movement Settings")]
    [SerializeField] private float dragSpeed = 20f;
    [SerializeField] private float moveDuration = 1.5f;
    [SerializeField] private float waitBetweenSteps = 0.5f;
    [SerializeField] private float pointReachThreshold = 0.15f;

    [Header("Pencil Paper Height")]
    [SerializeField] private float pencilPaperOffset = 0.02f;

    // =========================================================
    // STATE
    // =========================================================

    private bool draggingPencil = false;
    private bool scaleMoving = false;

    private bool pencilAtPointA = false;
    private bool pencilMovingToA = false;

    private bool pencilWasAtPointB = false;
    private bool line1Shown = false;
    private bool line2Shown = false;
    private bool line3Shown = false;

    private bool scaleReachedBC = false;
    private bool scaleReachedAC = false;
    private bool pencilSnappingToC = false;

    private int sequenceStep = 0;
    private int scaleTapStep = 0;

    private Vector3 pencilOffset;

    // =========================================================
    // ORIGINAL SCALE TRANSFORM
    // =========================================================

    private Vector3 originalScalePosition;
    private Quaternion originalScaleRotation;
    private Vector3 originalScaleSize;

    // =========================================================
    // ORIGINAL PENCIL TRANSFORM
    // =========================================================

    private Vector3 originalPencilPosition;
    private Quaternion originalPencilRotation;
    private Vector3 originalPencilScale;

    // =========================================================
    // SCALE - A TO B
    // =========================================================

    private Vector3 scaleABPosition =
        new Vector3(
            -0.64990234375f,
            0.3619384765625f,
            -4.955184936523438f
        );

    private Quaternion scaleABRotation =
        new Quaternion(
            -0.09919184446334839f,
            0.11455544829368591f,
            -0.04185716062784195f,
            0.9875656962394714f
        );

    private Vector3 scaleABScale =
        new Vector3(
            0.22176410257816316f,
            3.2475950717926025f,
            10.620046615600586f
        );

    // =========================================================
    // SCALE - B TO C
    // =========================================================

    private Vector3 scaleBCPosition =
        new Vector3(
            -0.3863525390625f,
            0.3648681640625f,
            -4.886076927185059f
        );

    private Quaternion scaleBCRotation =
        new Quaternion(
            0.009287320077419281f,
            0.974868893623352f,
            -0.10726039856672287f,
            0.19503775233990785f
        );

    private Vector3 scaleBCScale =
        new Vector3(
            0.22176410257816316f,
            3.2475950717926025f,
            10.620046615600586f
        );

    // =========================================================
    // SCALE - A TO C
    // =========================================================

    private Vector3 scaleACPosition =
        new Vector3(
            -0.51318359375f,
            0.289794921875f,
            -5.335580825805664f
        );

    private Quaternion scaleACRotation =
        new Quaternion(
            0.06821417808532715f,
            0.6776037216186523f,
            -0.07466026395559311f,
            -0.7284407615661621f
        );

    private Vector3 scaleACScale =
        new Vector3(
            0.22176410257816316f,
            3.2475950717926025f,
            10.620046615600586f
        );

    // =========================================================
    // PENCIL - A TO B
    // =========================================================

    private Vector3 pencilABPosition =
        new Vector3(
            -0.572265625f,
            0.676513671875f,
            -5.058512210845947f
        );

    private Quaternion pencilABRotation =
        new Quaternion(
            0.5635210871696472f,
            -0.480669766664505f,
            0.5612855553627014f,
            -0.36926838755607607f
        );

    private Vector3 pencilABScale =
        new Vector3(
            2.4356961250305177f,
            2.4356963634490969f,
            2.435696601867676f
        );

    // =========================================================
    // PENCIL - B TO C
    // =========================================================

    private Vector3 pencilBCPosition =
        new Vector3(
            -0.3604736328125f,
            0.6876220703125f,
            -5.083145618438721f
        );

    private Quaternion pencilBCRotation =
        new Quaternion(
            0.5635210871696472f,
            -0.480669766664505f,
            0.5612855553627014f,
            -0.36926838755607607f
        );

    private Vector3 pencilBCScale =
        new Vector3(
            2.4356961250305177f,
            2.4356963634490969f,
            2.435696601867676f
        );

    // =========================================================
    // PENCIL - A TO C
    // =========================================================

    private Vector3 pencilACPosition =
        new Vector3(
            -0.3973388671875f,
            0.619873046875f,
            -5.368896007537842f
        );

    private Quaternion pencilACRotation =
        new Quaternion(
            0.5635210871696472f,
            -0.480669766664505f,
            0.5612855553627014f,
            -0.36926838755607607f
        );

    private Vector3 pencilACScale =
        new Vector3(
            2.4356961250305177f,
            2.4356963634490969f,
            2.435696601867676f
        );

    // =========================================================
    // PENCIL - POINT A
    // =========================================================

    private Vector3 pencilPointAPosition =
        new Vector3(
            -0.62646484375f,
            0.63958740234375f,
            -5.360274314880371f
        );

    private Quaternion pencilPointARotation =
        new Quaternion(
            0.5161210894584656f,
            -0.3815830945968628f,
            0.638754665851593f,
            -0.4242709279060364f
        );

    private Vector3 pencilPointAScale =
        new Vector3(
            2.4356961250305177f,
            2.4356963634490969f,
            2.435696601867676f
        );

    // =========================================================
    // PENCIL - POINT B
    // =========================================================

    private Vector3 pencilPointBPosition =
        new Vector3(
            -0.467529296875f,
            0.8009033203125f,
            -5.014172077178955f
        );

    private Quaternion pencilPointBRotation =
        new Quaternion(
            0.5161210894584656f,
            -0.3815830945968628f,
            0.638754665851593f,
            -0.4242709279060364f
        );

    private Vector3 pencilPointBScale =
        new Vector3(
            2.4788079261779787f,
            2.4788081645965578f,
            2.4788084030151369f
        );

    // =========================================================
    // PENCIL - POINT C
    // =========================================================

    private Vector3 pencilPointCPosition =
        new Vector3(
            -0.217529296875f,
            0.6370849609375f,
            -5.389834403991699f
        );

    private Quaternion pencilPointCRotation =
        new Quaternion(
            0.5161210894584656f,
            -0.3815830945968628f,
            0.638754665851593f,
            -0.4242709279060364f
        );

    private Vector3 pencilPointCScale =
        new Vector3(
            2.4788079261779787f,
            2.4788081645965578f,
            2.4788084030151369f
        );

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (scaleObject != null)
        {
            originalScalePosition =
                scaleObject.transform.position;

            originalScaleRotation =
                scaleObject.transform.rotation;

            originalScaleSize =
                scaleObject.transform.localScale;
        }

        if (pencilObject != null)
        {
            originalPencilPosition =
                pencilObject.transform.position;

            originalPencilRotation =
                pencilObject.transform.rotation;

            originalPencilScale =
                pencilObject.transform.localScale;
        }

        Debug.Log("SCALE PENCIL CONTROLLER STARTED");
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        HandleMouse();
        HandleTouch();
    }

    // =========================================================
    // MOUSE
    // =========================================================

    private void HandleMouse()
    {
        if (Mouse.current == null)
            return;

        Vector2 position =
            Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            StartObjectClick(position);
        }

        if (Mouse.current.leftButton.isPressed)
        {
            ContinuePencilDrag(position);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            StopPencilDrag();
        }
    }

    // =========================================================
    // TOUCH
    // =========================================================

    private void HandleTouch()
    {
        if (Touchscreen.current == null)
            return;

        var touch =
            Touchscreen.current.primaryTouch;

        Vector2 position =
            touch.position.ReadValue();

        if (touch.press.wasPressedThisFrame)
        {
            StartObjectClick(position);
        }

        if (touch.press.isPressed)
        {
            ContinuePencilDrag(position);
        }

        if (touch.press.wasReleasedThisFrame)
        {
            StopPencilDrag();
        }
    }

    // =========================================================
    // CLICK SCALE / PENCIL
    // =========================================================

    private void StartObjectClick(Vector2 screenPosition)
    {
        Ray ray =
            mainCamera.ScreenPointToRay(screenPosition);

        RaycastHit[] hits =
            Physics.RaycastAll(ray, 1000f);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            // =================================================
            // SCALE CLICK
            // =================================================

            if (scaleObject != null &&
                IsPartOfObject(
                    hit.collider.gameObject,
                    scaleObject))
            {
                Debug.Log("SCALE CLICKED");

                if (!scaleMoving)
                {
                    StartCoroutine(
                        MoveScaleOnTap()
                    );
                }

                return;
            }

            // =================================================
            // PENCIL CLICK
            // =================================================

            if (pencilObject != null &&
                IsPartOfObject(
                    hit.collider.gameObject,
                    pencilObject))
            {
                Debug.Log("PENCIL CLICKED");

                if (!pencilAtPointA &&
                    !pencilMovingToA)
                {
                    StartCoroutine(
                        MovePencilToPointA()
                    );

                    return;
                }

                if (pencilAtPointA)
                {
                    draggingPencil = true;

                    pencilOffset =
                        pencilObject.transform.position -
                        hit.point;

                    CheckDragStartLine();
                }

                return;
            }
        }
    }

    // =========================================================
    // CHECK OBJECT + CHILD
    // =========================================================

    private bool IsPartOfObject(
        GameObject hitObject,
        GameObject targetObject)
    {
        if (hitObject == targetObject)
            return true;

        Transform current =
            hitObject.transform;

        while (current != null)
        {
            if (current.gameObject == targetObject)
                return true;

            current = current.parent;
        }

        return false;
    }

    // =========================================================
    // PENCIL DRAG
    // =========================================================

    private void ContinuePencilDrag(Vector2 screenPosition)
    {
        if (!draggingPencil)
            return;

        if (filterPaperCollider == null)
        {
            Debug.LogError(
                "FILTER PAPER COLLIDER ASSIGN NAHI HAI!"
            );

            return;
        }

        Ray ray =
            mainCamera.ScreenPointToRay(screenPosition);

        if (!filterPaperCollider.Raycast(
            ray,
            out RaycastHit paperHit,
            1000f))
        {
            return;
        }

        Vector3 target =
            paperHit.point;

        target += pencilOffset;

        // =====================================================
        // IMPORTANT:
        // PENCIL PAPER KE ANDAR NAHI JAYEGI
        // Pencil ko paper ke TOP par rakhenge.
        // =====================================================

        float paperTopY =
            filterPaperCollider.bounds.max.y;

        Collider pencilCollider =
            pencilObject.GetComponentInChildren<Collider>();

        if (pencilCollider != null)
        {
            float pencilHalfHeight =
                pencilCollider.bounds.extents.y;

            target.y =
                paperTopY +
                pencilHalfHeight +
                pencilPaperOffset;
        }
        else
        {
            // Agar pencil par Collider nahi hai
            // to safe height use hogi.
            target.y =
                paperTopY +
                0.1f;
        }

        // =====================================================
        // SMOOTH PENCIL DRAG
        // =====================================================

        pencilObject.transform.position =
            Vector3.Lerp(
                pencilObject.transform.position,
                target,
                Time.deltaTime * dragSpeed
            );

        CheckDragStartLine();
    }

    // =========================================================
    // DRAG START PAR LINE CHECK
    // =========================================================

    private void CheckDragStartLine()
    {
        Vector3 currentPos =
            pencilObject.transform.position;

        // Point A se drag shuru -> Line 1
        if (!line1Shown)
        {
            float distanceToA =
                HorizontalDistance(
                    currentPos,
                    pencilPointAPosition
                );

            if (distanceToA <= pointReachThreshold)
            {
                line1Shown = true;

                if (line1Image != null)
                    line1Image.SetActive(true);
            }
        }

        // Scale B -> C par pohoch chuka hai
        // aur ab pencil drag ho rahi hai -> Line 2
        if (!line2Shown &&
            scaleReachedBC)
        {
            line2Shown = true;
            pencilWasAtPointB = true;

            if (line2Image != null)
                line2Image.SetActive(true);
        }

        // Scale A -> C par pohoch chuka hai
        // -> Line 3
        if (!line3Shown &&
            scaleReachedAC &&
            !pencilSnappingToC)
        {
            line3Shown = true;

            if (line3Image != null)
                line3Image.SetActive(true);

            StartCoroutine(
                SnapPencilToPointC()
            );
        }
    }

    // =========================================================
    // PENCIL KHUD POINT C PAR SNAP
    // =========================================================

    private IEnumerator SnapPencilToPointC()
    {
        pencilSnappingToC = true;

        draggingPencil = false;

        yield return StartCoroutine(
            MoveObject(
                pencilObject,
                pencilPointCPosition,
                pencilPointCRotation,
                pencilPointCScale
            )
        );

        yield return new WaitForSeconds(
            waitBetweenSteps
        );

        yield return StartCoroutine(
            MoveObject(
                pencilObject,
                originalPencilPosition,
                originalPencilRotation,
                originalPencilScale
            )
        );

        pencilSnappingToC = false;
    }

    // =========================================================
    // HORIZONTAL X-Z DISTANCE
    // =========================================================

    private float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        Vector2 flatA =
            new Vector2(a.x, a.z);

        Vector2 flatB =
            new Vector2(b.x, b.z);

        return Vector2.Distance(
            flatA,
            flatB
        );
    }

    // =========================================================
    // STOP PENCIL DRAG
    // =========================================================

    private void StopPencilDrag()
    {
        draggingPencil = false;
    }

    // =========================================================
    // SCALE A -> B
    // =========================================================

    private IEnumerator MoveScaleToAB()
    {
        scaleMoving = true;

        Debug.Log("SCALE A -> B START");

        yield return StartCoroutine(
            MoveObject(
                scaleObject,
                scaleABPosition,
                scaleABRotation,
                scaleABScale
            )
        );

        Debug.Log("SCALE A -> B COMPLETE");

        scaleMoving = false;

        sequenceStep = 1;
    }

    // =========================================================
    // SCALE TAP MOVEMENT
    // =========================================================

    private IEnumerator MoveScaleOnTap()
    {
        scaleMoving = true;

        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 targetScale;

        switch (scaleTapStep)
        {
            case 0:

                Debug.Log("SCALE TAP: A -> B");

                targetPosition = scaleABPosition;
                targetRotation = scaleABRotation;
                targetScale = scaleABScale;

                break;

            case 1:

                Debug.Log("SCALE TAP: B -> C");

                targetPosition = scaleBCPosition;
                targetRotation = scaleBCRotation;
                targetScale = scaleBCScale;

                break;

            case 2:

                Debug.Log("SCALE TAP: A -> C");

                targetPosition = scaleACPosition;
                targetRotation = scaleACRotation;
                targetScale = scaleACScale;

                break;

            default:

                Debug.Log("SCALE TAP: BACK TO ORIGINAL");

                targetPosition = originalScalePosition;
                targetRotation = originalScaleRotation;
                targetScale = originalScaleSize;

                break;
        }

        yield return StartCoroutine(
            MoveObject(
                scaleObject,
                targetPosition,
                targetRotation,
                targetScale
            )
        );

        if (scaleTapStep == 1)
        {
            scaleReachedBC = true;
        }

        if (scaleTapStep == 2)
        {
            scaleReachedAC = true;
        }

        scaleTapStep =
            (scaleTapStep + 1) % 4;

        scaleMoving = false;
    }

    // =========================================================
    // PENCIL TO POINT A
    // =========================================================

    private IEnumerator MovePencilToPointA()
    {
        if (pencilObject == null)
            yield break;

        pencilMovingToA = true;

        Debug.Log("PENCIL -> POINT A START");

        yield return StartCoroutine(
            MoveObject(
                pencilObject,
                pencilPointAPosition,
                pencilPointARotation,
                pencilPointAScale
            )
        );

        Debug.Log("PENCIL -> POINT A COMPLETE");

        pencilMovingToA = false;
        pencilAtPointA = true;
    }

    // =========================================================
    // START PENCIL + SCALE SEQUENCE
    // =========================================================

    private IEnumerator StartPencilAndScaleSequence()
    {
        scaleMoving = true;

        Debug.Log("PENCIL + SCALE : A -> B");

        yield return StartCoroutine(
            MoveBothObjects(
                pencilABPosition,
                pencilABRotation,
                pencilABScale,

                scaleABPosition,
                scaleABRotation,
                scaleABScale
            )
        );

        yield return new WaitForSeconds(
            waitBetweenSteps
        );

        Debug.Log("PENCIL + SCALE : B -> C");

        yield return StartCoroutine(
            MoveBothObjects(
                pencilBCPosition,
                pencilBCRotation,
                pencilBCScale,

                scaleBCPosition,
                scaleBCRotation,
                scaleBCScale
            )
        );

        yield return new WaitForSeconds(
            waitBetweenSteps
        );

        Debug.Log("PENCIL + SCALE : A -> C");

        yield return StartCoroutine(
            MoveBothObjects(
                pencilACPosition,
                pencilACRotation,
                pencilACScale,

                scaleACPosition,
                scaleACRotation,
                scaleACScale
            )
        );

        yield return new WaitForSeconds(
            waitBetweenSteps
        );

        Debug.Log("RETURNING BOTH OBJECTS");

        yield return StartCoroutine(
            MoveBothObjects(
                originalPencilPosition,
                originalPencilRotation,
                originalPencilScale,

                originalScalePosition,
                originalScaleRotation,
                originalScaleSize
            )
        );

        Debug.Log(
            "PENCIL + SCALE BACK TO ORIGINAL POSITION"
        );

        scaleMoving = false;

        sequenceStep = 3;
    }

    // =========================================================
    // MOVE OBJECT
    // =========================================================

    private IEnumerator MoveObject(
        GameObject obj,
        Vector3 targetPosition,
        Quaternion targetRotation,
        Vector3 targetScale)
    {
        if (obj == null)
            yield break;

        Vector3 startPosition =
            obj.transform.position;

        Quaternion startRotation =
            obj.transform.rotation;

        Vector3 startScale =
            obj.transform.localScale;

        float time = 0f;

        while (time < moveDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / moveDuration
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            obj.transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t
                );

            obj.transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t
                );

            obj.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    t
                );

            yield return null;
        }

        obj.transform.position =
            targetPosition;

        obj.transform.rotation =
            targetRotation;

        obj.transform.localScale =
            targetScale;
    }

    // =========================================================
    // MOVE BOTH OBJECTS
    // =========================================================

    private IEnumerator MoveBothObjects(
        Vector3 targetPencilPosition,
        Quaternion targetPencilRotation,
        Vector3 targetPencilScale,

        Vector3 targetScalePosition,
        Quaternion targetScaleRotation,
        Vector3 targetScaleScale)
    {
        if (pencilObject == null ||
            scaleObject == null)
            yield break;

        Vector3 startPencilPosition =
            pencilObject.transform.position;

        Quaternion startPencilRotation =
            pencilObject.transform.rotation;

        Vector3 startPencilScale =
            pencilObject.transform.localScale;

        Vector3 startScalePosition =
            scaleObject.transform.position;

        Quaternion startScaleRotation =
            scaleObject.transform.rotation;

        Vector3 startScaleScale =
            scaleObject.transform.localScale;

        float time = 0f;

        while (time < moveDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / moveDuration
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            pencilObject.transform.position =
                Vector3.Lerp(
                    startPencilPosition,
                    targetPencilPosition,
                    t
                );

            pencilObject.transform.rotation =
                Quaternion.Slerp(
                    startPencilRotation,
                    targetPencilRotation,
                    t
                );

            pencilObject.transform.localScale =
                Vector3.Lerp(
                    startPencilScale,
                    targetPencilScale,
                    t
                );

            scaleObject.transform.position =
                Vector3.Lerp(
                    startScalePosition,
                    targetScalePosition,
                    t
                );

            scaleObject.transform.rotation =
                Quaternion.Slerp(
                    startScaleRotation,
                    targetScaleRotation,
                    t
                );

            scaleObject.transform.localScale =
                Vector3.Lerp(
                    startScaleScale,
                    targetScaleScale,
                    t
                );

            yield return null;
        }

        pencilObject.transform.position =
            targetPencilPosition;

        pencilObject.transform.rotation =
            targetPencilRotation;

        pencilObject.transform.localScale =
            targetPencilScale;

        scaleObject.transform.position =
            targetScalePosition;

        scaleObject.transform.rotation =
            targetScaleRotation;

        scaleObject.transform.localScale =
            targetScaleScale;
    }
}