using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public class ComvexLensGapController : MonoBehaviour
{
   [System.Serializable]
    public class LensSlideData
    {
        [Tooltip("The page index where this interaction is active.")]
        public int pageIndex;

        [Tooltip("The specific convex lenses selectable on this slide.")]
        public List<GameObject> selectableLenses = new();

        [Header("Paper Position Mapping (Optional for this slide)")]
        [Tooltip("Reference to the paper Transform.")]
        public Transform paperObject;
        [Tooltip("Where the paper sits initially.")]
        public Transform paperStartTransform;
        [Tooltip("Where the paper sits when placed under the screw.")]
        public Transform paperUnderScrewTransform;
        [Tooltip("Where the paper stops when blocked by the screw.")]
        public Transform paperBlockedTransform;

        [Tooltip("The target transform representing the 'gap' for this slide.")]
        public Transform gapTarget;

        [Tooltip("Distance threshold required to pass the gap check.")]
        public float gapThreshold = 0.05f;

        [Header("Slide Events")]
        public UnityEvent OnSlideCheckPassed;
    }

    [Header("Slide Configurations")]
    [SerializeField] private List<LensSlideData> slideDataList = new();

    [Header("Events")]
    [Tooltip("Invoked when the selected lens successfully passes the gap check.")]
    public UnityEvent OnFinalGapCheckPassed;

    private int currentPageIndex = 0;

    private void OnEnable()
    {
        // Listen to the navigation event[cite: 3]
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        // Listen to the navigation event[cite: 3]
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;
    }

    /// <summary>
    /// Call this via UI buttons or click events to move the paper to a specific target position.
    /// positionType: 0 = Start, 1 = Under Screw, 2 = Blocked
    /// </summary>
    public void MovePaper(int positionType)
    {
        LensSlideData currentData = slideDataList.Find(x => x.pageIndex == currentPageIndex);
        if (currentData == null || currentData.paperObject == null) return;

        Transform target = null;
        if (positionType == 0) target = currentData.paperStartTransform;
        else if (positionType == 1) target = currentData.paperUnderScrewTransform;
        else if (positionType == 2) target = currentData.paperBlockedTransform;

        if (target != null)
        {
            currentData.paperObject.position = target.position;
            currentData.paperObject.rotation = target.rotation;
        }
    }

    /// <summary>
    /// Call this from your interaction script (e.g., OnMouseUp or OnDrop) passing the dragged lens.
    /// </summary>
    public void CheckLensGap(GameObject selectedLens)
    {
        LensSlideData currentData = slideDataList.Find(x => x.pageIndex == currentPageIndex);

        if (currentData == null) return;

        // Verify the object is allowed to be selected on this specific slide index
        if (!currentData.selectableLenses.Contains(selectedLens))
        {
            Debug.LogWarning($"[ConvexLens] {selectedLens.name} is not a valid selection for page {currentPageIndex}.");
            return;
        }

        if (currentData.gapTarget == null) return;

        // Perform the final gap check
        float distance = Vector3.Distance(selectedLens.transform.position, currentData.gapTarget.position);
        
        if (distance <= currentData.gapThreshold)
        {
            // Optional: Snap perfectly into the gap
            selectedLens.transform.position = currentData.gapTarget.position;
            selectedLens.transform.rotation = currentData.gapTarget.rotation;

            // Fire the events
            currentData.OnSlideCheckPassed?.Invoke();
            OnFinalGapCheckPassed?.Invoke();
        }
    }

    /// <summary>
    /// Helper method to bind in the Inspector's OnFinalGapCheckPassed event.
    /// </summary>
    public void UnlockPageNavigation()
    {
        // Calls the static unlock method from your manager[cite: 3]
        PageNavigationController.RequestNavigationUnlock();
    }
}
