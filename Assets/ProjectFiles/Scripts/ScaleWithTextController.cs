using UnityEngine;

using UnityEngine.InputSystem;

using System.Collections;

using System.Collections.Generic;



public class ScaleWithTextController : MonoBehaviour

{

    [Header("Scale Object")]

    [SerializeField] private GameObject scaleObject;



    [Header("Main Camera")]

    [SerializeField] private Camera mainCamera;



    [Header("3cm Text Objects")]

    [SerializeField] private GameObject text3cm1; // A -> B par show hoga

    [SerializeField] private GameObject text3cm2; // B -> C par show hoga

    [SerializeField] private GameObject text3cm3; // A -> C par show hoga



    [Header("Movement Settings")]

    [SerializeField] private float moveDuration = 1.5f;



    [Header("Slide Sync")]

    [Tooltip("Page indices on which this controller accepts clicks. Leave empty to allow on any page.")]

    [SerializeField] private List<int> activePageIndices = new List<int>();



    [Tooltip("If true, the scale and texts reset to their original state whenever the page changes away from an active index.")]

    [SerializeField] private bool resetOnPageLeave = true;



    // =========================================================

    // STATE

    // =========================================================



    private bool scaleMoving = false;

    private int scaleTapStep = 0; // 0 = A->B, 1 = B->C, 2 = A->C, 3 = back to original

    private bool isActiveOnCurrentPage = true;



    // =========================================================

    // ORIGINAL SCALE TRANSFORM

    // =========================================================



    private Vector3 originalScalePosition;

    private Quaternion originalScaleRotation;

    private Vector3 originalScaleSize;



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

    // START

    // =========================================================



    // =========================================================
    // AWAKE (runs before OnEnable, so original transform is
    // captured before HandlePageChanged can possibly reset it)
    // =========================================================
 
    private bool hasOriginalScaleTransform;
 
    private void Awake()
    {
        if (scaleObject != null)
        {
            originalScalePosition = scaleObject.transform.position;
            originalScaleRotation = scaleObject.transform.rotation;
            originalScaleSize = scaleObject.transform.localScale;
            hasOriginalScaleTransform = true;
        }
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
 
    private void HandlePageChanged(int pageIndex)
    {
        isActiveOnCurrentPage = activePageIndices.Count == 0 || activePageIndices.Contains(pageIndex);
 
        if (!isActiveOnCurrentPage && resetOnPageLeave)
            ResetScaleAndText();
    }
 
    private void ResetScaleAndText()
    {
        StopAllCoroutines();
        scaleMoving = false;
        scaleTapStep = 0;
 
        if (scaleObject != null && hasOriginalScaleTransform)
        {
            scaleObject.transform.position = originalScalePosition;
            scaleObject.transform.rotation = originalScaleRotation;
            scaleObject.transform.localScale = originalScaleSize;
        }
 
        if (text3cm1 != null) text3cm1.SetActive(false);
        if (text3cm2 != null) text3cm2.SetActive(false);
        if (text3cm3 != null) text3cm3.SetActive(false);
    }
 
    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
 
        // =====================================================
        // START MAI SAB TEXT HIDE RAKHO
        // =====================================================
 
        if (text3cm1 != null) text3cm1.SetActive(false);
        if (text3cm2 != null) text3cm2.SetActive(false);
        if (text3cm3 != null) text3cm3.SetActive(false);
 
        Debug.Log("SCALE WITH TEXT CONTROLLER STARTED");
    }



    // =========================================================

    // UPDATE

    // =========================================================



    private void Update()

    {

        if (!isActiveOnCurrentPage)

            return;



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



        if (Mouse.current.leftButton.wasPressedThisFrame)

        {

            Vector2 position = Mouse.current.position.ReadValue();

            TryClickScale(position);

        }

    }



    // =========================================================

    // TOUCH

    // =========================================================



    private void HandleTouch()

    {

        if (Touchscreen.current == null)

            return;



        var touch = Touchscreen.current.primaryTouch;



        if (touch.press.wasPressedThisFrame)

        {

            Vector2 position = touch.position.ReadValue();

            TryClickScale(position);

        }

    }



    // =========================================================

    // CLICK CHECK ON SCALE

    // =========================================================



    private void TryClickScale(Vector2 screenPosition)

    {

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);



        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);



        System.Array.Sort(

            hits,

            (a, b) => a.distance.CompareTo(b.distance)

        );



        foreach (RaycastHit hit in hits)

        {

            if (scaleObject != null &&

                IsPartOfObject(hit.collider.gameObject, scaleObject))

            {

                Debug.Log("SCALE CLICKED");



                if (!scaleMoving)

                {

                    StartCoroutine(MoveScaleOnTap());

                }



                return;

            }

        }

    }



    // =========================================================

    // CHECK OBJECT + CHILD

    // =========================================================



    private bool IsPartOfObject(GameObject hitObject, GameObject targetObject)

    {

        if (hitObject == targetObject)

            return true;



        Transform current = hitObject.transform;



        while (current != null)

        {

            if (current.gameObject == targetObject)

                return true;



            current = current.parent;

        }



        return false;

    }



    // =========================================================

    // SCALE TAP MOVEMENT (A->B, B->C, A->C, BACK)

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



        // =====================================================

        // MOVEMENT COMPLETE HONE KE BAAD SAHI TEXT SHOW KARO

        // =====================================================



        if (scaleTapStep == 0)

        {

            // A -> B complete

            if (text3cm1 != null)

                text3cm1.SetActive(true);

        }

        else if (scaleTapStep == 1)

        {

            // B -> C complete

            if (text3cm2 != null)

                text3cm2.SetActive(true);

        }

        else if (scaleTapStep == 2)

        {

            // A -> C complete

            if (text3cm3 != null)

                text3cm3.SetActive(true);

        }

        // =====================================================

        // scaleTapStep == 3 (scale wapas original par gaya) -> 

        // KOI text hide NAHI hoga, jo bhi show ho chuka hai

        // wo show hi rahega.

        // =====================================================



        scaleTapStep = (scaleTapStep + 1) % 4;



        scaleMoving = false;

    }



    // =========================================================

    // MOVE OBJECT (SMOOTH LERP)

    // =========================================================



    private IEnumerator MoveObject(

        GameObject obj,

        Vector3 targetPosition,

        Quaternion targetRotation,

        Vector3 targetScale)

    {

        if (obj == null)

            yield break;



        Vector3 startPosition = obj.transform.position;

        Quaternion startRotation = obj.transform.rotation;

        Vector3 startScale = obj.transform.localScale;



        float time = 0f;



        while (time < moveDuration)

        {

            time += Time.deltaTime;



            float t = Mathf.Clamp01(time / moveDuration);



            t = Mathf.SmoothStep(0f, 1f, t);



            obj.transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            obj.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            obj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);



            yield return null;

        }



        obj.transform.position = targetPosition;

        obj.transform.rotation = targetRotation;

        obj.transform.localScale = targetScale;

    }

}
