using UnityEngine;

namespace DeterminingMassofaBodyUsingMeterscale
{
    public class GhostDropGroupController : MonoBehaviour
    {
        [Header("All ghost targets on this slide (size 1 for single-item slides too)")]
        public GhostDropTarget[] targets;

        private int completedCount;
        private bool navigationUnlocked;

        private void OnEnable()
        {
            completedCount = 0;
            navigationUnlocked = false;
            foreach (var t in targets) t.OnCorrectDropped += HandleOneCompleted;
        }

        private void OnDisable()
        {
            foreach (var t in targets) t.OnCorrectDropped -= HandleOneCompleted;
        }

        private void HandleOneCompleted()
        {
            completedCount++;
            if (completedCount >= targets.Length && !navigationUnlocked)
            {
                navigationUnlocked = true;
                PageNavigationController.RequestNavigationUnlock();
            }
        }
    }
}