using UnityEngine;
using System.Collections;

public class MoveSpherometerAway : MonoBehaviour
{
    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Right Side Movement")]
    [SerializeField] private float moveDistance = 8f;

    [SerializeField] private float moveDuration = 1f;

    [Header("Extra Up Movement")]
    [SerializeField] private float moveUp = 0f;

    private bool hasMoved = false;

    public void MoveAway()
    {
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