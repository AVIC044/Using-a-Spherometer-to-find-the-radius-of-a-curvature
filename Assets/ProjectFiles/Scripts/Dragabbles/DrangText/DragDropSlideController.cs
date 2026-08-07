using UnityEngine;
using TMPro;

public class DragDropSlideController : MonoBehaviour
{
    // [Header("Data")]
    // public DragDropQuestionData questionData;

    [Header("Instruction UI (this slide's own text, separate from UIPromptController)")]
    public TMP_Text instructionText;

    [Header("Draggable Items For This Slide")]
    public UIDragToUIDrop[] items;

    private bool navigationUnlocked = false;

    private void OnEnable()
    {
        // if (instructionText != null && questionData != null)
        //     instructionText.text = questionData.instructionText;

        foreach (var item in items)
            item.onCorrectDrop.AddListener(HandleItemCorrect);

        // Revisiting an already-completed slide: re-check without resetting anything
        navigationUnlocked = false;
        CheckCompletion();
    }

    private void OnDisable()
    {
        foreach (var item in items)
            item.onCorrectDrop.RemoveListener(HandleItemCorrect);
    }

    private void HandleItemCorrect() => CheckCompletion();

    private void CheckCompletion()
    {
        foreach (var item in items)
            if (!item.IsCorrect) return; // not all placed yet

        if (!navigationUnlocked)
        {
            navigationUnlocked = true;
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    // Wire this to your Retake Evaluation flow ONLY — never called on normal page revisit
    public void ResetSlide()
    {
        navigationUnlocked = false;
        foreach (var item in items)
            item.ResetDraggable();
    }
}

 