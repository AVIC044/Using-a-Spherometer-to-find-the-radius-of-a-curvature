using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MoveSpherometerAway : MonoBehaviour
{
    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Right Side Movement")]
    [SerializeField] private float moveDistance = 8f;

    [SerializeField] private float moveDuration = 1f;

    [Header("Extra Up Movement")]
    [SerializeField] private float moveUp = 0f;

    [Header("Slide Sync")]
    [Tooltip("Page indices on which MoveAway() is allowed to run. Leave empty to allow on any page.")]
    [SerializeField] private List<int> activePageIndices = new List<int>();

    [Tooltip("If true, the spherometer snaps back to its original spot whenever the page changes away from an active index.")]
    [SerializeField] private bool resetOnPageLeave = true;

    private bool hasMoved = false;
    private bool isActiveOnCurrentPage = true;

    private Vector3 originalPosition;
    private bool hasOriginalPosition;

    private void Awake()
    {
        originalPosition = transform.position;
        hasOriginalPosition = true;
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
            ResetSpherometer();
    }

    private void ResetSpherometer()
    {
        StopAllCoroutines();

        if (hasOriginalPosition)
            transform.position = originalPosition;

        hasMoved = false;
    }

    public void MoveAway()
    {
        if (!isActiveOnCurrentPage)
        {
            Debug.Log($"[MoveSpherometerAway] Ignored: page {PageNavigationController.CurrentIndex} is not an active page for this trigger.");
            return;
        }

        if (hasMoved)
            return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is not assigned!");
            return;
        }

        hasMoved = true;

        StartCoroutine(MoveToRightSide());
    }

    private IEnumerator MoveToRightSide()
    {
        Vector3 startPosition = transform.position;

        // Camera ka RIGHT direction
        Vector3 rightDirection = mainCamera.transform.right;

        // Camera ke right side ki taraf final position
        Vector3 endPosition =
            startPosition + (rightDirection * moveDistance);

        // Optional upward movement
        endPosition += mainCamera.transform.up * moveUp;

        float time = 0f;

        while (time < moveDuration)
        {
            time += Time.deltaTime;

            float t = time / moveDuration;

            // Smooth floating movement
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                startPosition,
                endPosition,
                t
            );

            yield return null;
        }

        transform.position = endPosition;

        Debug.Log("SPHEROMETER MOVED TO RIGHT SIDE");
    }
}