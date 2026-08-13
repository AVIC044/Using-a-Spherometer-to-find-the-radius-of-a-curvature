using UnityEngine;

/// <summary>
/// Holds all Spherometer-specific target/highlight mapping.
/// Attach this to the SAME GameObject as DraggableObject only when that
/// object needs spherometer-style mapped targets. If this component is
/// absent, DraggableObject falls back to its normal per-SnapElement
/// highlightObject behavior.
/// </summary>
public class SpherometerTargetProvider : MonoBehaviour
{
    [Tooltip("Single shared highlight object used across all spherometer steps.")]
    [SerializeField] private GameObject highlightObject;

    [Tooltip("Master list of target Transforms in scene (e.g., Index 0: Table, Index 1: Convex Lens, Index 2: Glass Plate, etc.).")]
    [SerializeField] private Transform[] targets;

    private Collider highlightCollider;

    public GameObject HighlightObject => highlightObject;
    public Collider HighlightCollider => highlightCollider;

    private void Awake()
    {
        if (highlightObject != null)
        {
            highlightCollider = highlightObject.GetComponent<Collider>();
            highlightObject.SetActive(false);
        }
    }

    /// <summary>
    /// Returns the mapped target Transform for a given SnapElement's
    /// targetPointIndex, or null (with a warning) if out of range.
    /// </summary>
    public Transform GetTargetTransform(int targetPointIndex)
    {
        if (targets == null || targets.Length == 0)
            return null;

        if (targetPointIndex >= 0 && targetPointIndex < targets.Length)
            return targets[targetPointIndex];

        Debug.LogWarning($"[SpherometerTargetProvider] Index {targetPointIndex} out of bounds for targets.");
        return null;
    }
}