using UnityEngine;
using System.Collections;
using TMPro;
public class SpherometerController : MonoBehaviour
{
    [Header("General references")]
    [SerializeField] private GameObject _spherometer;
    [SerializeField] private int maxrotationsCount = 3;
    [SerializeField] private GameObject[] _highlightobjectsInPitchScale;
    [SerializeField] private MeshRenderer _spehrometerRotationObject;
    [SerializeField] private TMP_Text _countText;
    [Header("Private fields")]
    [SerializeField] private float moveDistance = 0.01f;   // how far down it moves per rotation (much smaller now)
    [SerializeField] private float highlightFadeDuration = 0.4f; // how long each highlight takes to fade in

    [Header("Camera FOV Settings")]
    [SerializeField] private Camera _cam;
    [SerializeField] private float _defaultCamFOV = 60f; // FOV used for other slides
    [SerializeField] private float _slide2FOV = 15f;     // FOV specifically for Slide 2
    [SerializeField] private int _slide2PageIndex = 1;   // 0-based page index (Slide 2 = Index 1)

    [Header("Private fields")]
    private int currentRotationcount = 0;
    private bool hasStartedRotation = false;
    private const string AlphaPropertyName = "_alpha_value"; // update if Reference differs


    private void OnEnable()
    {
        // Listen to page change events from PageNavigationController
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        _cam = Camera.main;

        // Turn off all highlight objects initially
        foreach (GameObject highlight in _highlightobjectsInPitchScale)
        {
            if (highlight != null)
                highlight.SetActive(false);
        }

        // Hide the rotation count text until the rotation starts
        if (_countText != null)
        {
            _countText.gameObject.SetActive(false);
        }

        // Apply initial FOV check based on current page
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        if (_cam == null) _cam = Camera.main;

        if (_cam != null)
        {
            // Set FOV to 15 on Slide 2 (index 1), otherwise set to default FOV
            if (pageIndex == _slide2PageIndex)
            {
                _cam.fieldOfView = _slide2FOV;
            }
            else
            {
                _cam.fieldOfView = _defaultCamFOV;
                _cam = Camera.main; // Reset to main camera for other slides
            }
        }
    }

    /// <summary>
    /// Call this method from MultiStepDialerController's "OnCorrectAnswer" event.
    /// Ensures it only triggers once upon entering the first correct answer.
    /// </summary>
    public void StartRotationSequence()
    {
        if (!hasStartedRotation)
        {
            hasStartedRotation = true;
            StartCoroutine(RotateSpherometerScaleRoutine());
        }
    }

    private IEnumerator RotateSpherometerScaleRoutine()
    {
        float rotationDuration = 1f;   // time for one full 360 spin
        currentRotationcount = 0;

        while (currentRotationcount < maxrotationsCount)
        {
            float elapsed = 0f;
            Vector3 rotationStartPos = _spehrometerRotationObject.transform.localPosition;
            Vector3 rotationEndPos = rotationStartPos + Vector3.down * moveDistance;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);

                // Rotation
                float angle = t * 360f;
                _spehrometerRotationObject.transform.localRotation = Quaternion.Euler(0, angle, 0);

                // Slight downward movement
                _spehrometerRotationObject.transform.localPosition = Vector3.Lerp(rotationStartPos, rotationEndPos, t);

                yield return null;
            }

            // Snap exactly to avoid positional drift
            _spehrometerRotationObject.transform.localPosition = rotationEndPos;

            // Increment rotation count
            currentRotationcount++;

            // 1. Fade in the highlight object corresponding to the completed rotation
            int highlightIndex = currentRotationcount - 1;
            if (highlightIndex >= 0 && highlightIndex < _highlightobjectsInPitchScale.Length)
            {
                if (_highlightobjectsInPitchScale[highlightIndex] != null)
                {
                    StartCoroutine(FadeInHighlight(_highlightobjectsInPitchScale[highlightIndex], highlightFadeDuration));
                }
            }

            // 2. Make the rotation count appear on screen right after incrementing
            if (_countText != null)
            {
                _countText.text = currentRotationcount.ToString();
                _countText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator FadeInHighlight(GameObject highlightObj, float duration)
    {
        highlightObj.SetActive(true);

        Renderer rend = highlightObj.GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogWarning($"[FadeInHighlight] No Renderer found on '{highlightObj.name}'.");
            yield break;
        }

        Material mat = rend.material;

        if (!mat.HasProperty(AlphaPropertyName))
        {
            Debug.LogWarning($"[FadeInHighlight] '{mat.name}' has no '{AlphaPropertyName}' property. " +
                              $"Check the Shader Graph Blackboard's Reference name for alpha_value.");
            yield break;
        }

        float targetAlpha = mat.GetFloat(AlphaPropertyName);

        mat.SetFloat(AlphaPropertyName, 0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            mat.SetFloat(AlphaPropertyName, Mathf.Lerp(0f, targetAlpha, t));
            yield return null;
        }

        mat.SetFloat(AlphaPropertyName, targetAlpha);
    }



}
