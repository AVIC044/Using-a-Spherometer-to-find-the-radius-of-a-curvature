using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Question7AnswerController : MonoBehaviour
{
    [Header("Button Panel")]
    [SerializeField] private GameObject buttonPanel;

    [Header("Answer Buttons (Correct Answer = Element 0)")]
    [SerializeField] private List<Button> answerButtons = new();

    [Header("Answer Sprites")]
    [SerializeField] private List<GameObject> answerSprites = new();

    [Header("Why Buttons")]
    [SerializeField] private Button correctWhyButton;
    [SerializeField] private Button wrongWhyButton;

    [Header("Explanation Panel")]
    [SerializeField] private GameObject explanationPanel;

    [Header("Continue Button")]
    [SerializeField] private Button continueButton;

    [Header("Page Settings")]
    [SerializeField] private int activePageIndex = 6;

    private bool hasAnswered = false;
    private int selectedAnswerIndex = -1;

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void Start()
    {
        // Hide everything initially
        buttonPanel.SetActive(false);
        explanationPanel.SetActive(false);
        correctWhyButton.gameObject.SetActive(false);
        wrongWhyButton.gameObject.SetActive(false);

        foreach (GameObject sprite in answerSprites)
        {
            if (sprite != null)
                sprite.SetActive(false);
        }

        // Register answer buttons
        for (int i = 0; i < answerButtons.Count; i++)
        {
            int index = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerClicked(index));
        }

        // Register Why buttons
        correctWhyButton.onClick.AddListener(OpenExplanation);
        wrongWhyButton.onClick.AddListener(OpenExplanation);

        // Register Continue button
        continueButton.onClick.AddListener(CloseExplanation);

        // Initialize page
        OnPageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnPageChanged(int pageIndex)
    {
        bool active = (pageIndex == activePageIndex);

        buttonPanel.SetActive(active);

        if (!active)
        {
            explanationPanel.SetActive(false);
            correctWhyButton.gameObject.SetActive(false);
            wrongWhyButton.gameObject.SetActive(false);

            foreach (GameObject sprite in answerSprites)
            {
                if (sprite != null)
                    sprite.SetActive(false);
            }

            return;
        }

        explanationPanel.SetActive(false);

        // First time opening the page
        if (!hasAnswered)
        {
            correctWhyButton.gameObject.SetActive(false);
            wrongWhyButton.gameObject.SetActive(false);

            foreach (GameObject sprite in answerSprites)
            {
                if (sprite != null)
                    sprite.SetActive(false);
            }

            foreach (Button btn in answerButtons)
            {
                if (btn != null)
                {
                    btn.gameObject.SetActive(true);
                    btn.interactable = true;
                }
            }

            return;
        }

        // ==========================
        // Restore previous state
        // ==========================

        foreach (Button btn in answerButtons)
        {
            btn.gameObject.SetActive(true);
            btn.interactable = false;
        }

        foreach (GameObject sprite in answerSprites)
        {
            sprite.SetActive(false);
        }

        // Show selected answer sprite
        answerButtons[selectedAnswerIndex].gameObject.SetActive(false);
        answerSprites[selectedAnswerIndex].SetActive(true);

        if (selectedAnswerIndex == 0)
        {
            // Correct answer
            correctWhyButton.gameObject.SetActive(true);
            wrongWhyButton.gameObject.SetActive(false);
        }
        else
        {
            // Wrong answer
            correctWhyButton.gameObject.SetActive(false);
            wrongWhyButton.gameObject.SetActive(true);

            // Also show the correct answer
            answerButtons[0].gameObject.SetActive(false);
            answerSprites[0].SetActive(true);
        }
    }
    private void OnAnswerClicked(int index)
    {
        if (PageNavigationController.CurrentIndex != activePageIndex)
            return;

        if (hasAnswered)
            return;

        hasAnswered = true;
        selectedAnswerIndex = index;

        // Disable all buttons
        foreach (Button btn in answerButtons)
        {
            btn.interactable = false;
        }

        // Hide clicked button
        answerButtons[index].gameObject.SetActive(false);

        // Show clicked sprite
        answerSprites[index].SetActive(true);

        if (index == 0) // Correct
        {
            correctWhyButton.gameObject.SetActive(true);
            wrongWhyButton.gameObject.SetActive(false);

            PageNavigationController.RequestNavigationUnlock();
        }
        else // Wrong
        {
            correctWhyButton.gameObject.SetActive(false);
            wrongWhyButton.gameObject.SetActive(true);

            // Show correct answer automatically
            answerButtons[0].gameObject.SetActive(false);
            answerSprites[0].SetActive(true);
        }
    }

    private void OpenExplanation()
    {
        explanationPanel.SetActive(true);
    }

    private void CloseExplanation()
    {
        explanationPanel.SetActive(false);
    }
}