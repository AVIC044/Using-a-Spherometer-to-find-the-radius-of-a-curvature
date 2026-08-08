using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class GlassPlaneGapController : MonoBehaviour
{
    [System.Serializable]
    public class GlassSlideData
    {
        [Header("Page Info")]
        [Tooltip("The page index where this configuration is active.")]
        public int pageIndex;

        [Header("Selectable Objects")]
        [Tooltip("Objects (like the glass plane or paper) allowed to interact on this slide.")]
        public List<GameObject> selectableObjects = new();

        [Header("Paper Position Mapping (Optional for this slide)")]
        [Tooltip("Reference to the paper GameObject.")]
        public Transform paperObject;
        [Tooltip("Where the paper sits initially.")]
        public Transform paperStartTransform;
        [Tooltip("Where the paper sits when placed under the screw.")]
        public Transform paperUnderScrewTransform;
        [Tooltip("Where the paper stops when blocked by the screw.")]
        public Transform paperBlockedTransform;

        [Header("Gap Target")]
        public Transform gapTarget;
        public float gapThreshold = 0.05f;

        [Header("Slide Events")]
        public UnityEvent OnSlideCheckPassed;
    }

    [Header("Slide Configurations")]
    [SerializeField] private List<GlassSlideData> slideDataList = new();

    [Header("Global Events")]
    [Tooltip("Invoked when the final gap check passes successfully.")]
    public UnityEvent OnFinalGapCheckPassed;

    private int currentPageIndex = 0;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
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
        GlassSlideData currentData = slideDataList.Find(x => x.pageIndex == currentPageIndex);
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
    /// Call this when testing an object (glass plane or paper) against the gap target.
    /// </summary>
    public void CheckGap(GameObject selectedObject)
    {
        GlassSlideData currentData = slideDataList.Find(x => x.pageIndex == currentPageIndex);
        if (currentData == null) return;

        if (!currentData.selectableObjects.Contains(selectedObject))
            return;

        if (currentData.gapTarget == null) return;

        float distance = Vector3.Distance(selectedObject.transform.position, currentData.gapTarget.position);
        
        if (distance <= currentData.gapThreshold)
        {
            // Snap to target
            selectedObject.transform.position = currentData.gapTarget.position;
            selectedObject.transform.rotation = currentData.gapTarget.rotation;

            currentData.OnSlideCheckPassed?.Invoke();
            OnFinalGapCheckPassed?.Invoke();
        }
    }

    /// <summary>
    /// Helper method to bind in the Inspector to unlock navigation.
    /// </summary>
    public void UnlockPageNavigation()
    {
        PageNavigationController.RequestNavigationUnlock();
    }
}
