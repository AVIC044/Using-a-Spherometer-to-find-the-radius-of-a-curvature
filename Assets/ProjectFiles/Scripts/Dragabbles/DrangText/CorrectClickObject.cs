using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// IMPORTANT (Unity rule): this file's name and this class name MUST match
/// exactly - "CorrectClickObject.cs" -> class CorrectClickObject.
/// If they don't match, Unity throws "script class cannot be found" and
/// nothing will work - this is a very common cause of "click hi nahi ho raha".
///
/// Attach this to the panel that holds all the options for one question.
/// Set question/explanation text directly here (no ScriptableObject needed).
/// For each option button, tick TRUE/FALSE in "Is Correct Option" (same
/// index/order as "Option Buttons"). Clicking the correct option unlocks
/// Page Navigation (Next button).
/// </summary>
public class CorrectClickObject : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mcqPanel;
    public GameObject explanationPanel;

    [Header("Question UI")]
    public TMP_Text questionText;
    [TextArea] public string question;
    public Image referenceImage;
    public Sprite referenceImageSprite;

    [Header("Options")]
    [Tooltip("Buttons in the order you want them.")]
    public Button[] optionButtons;

    [Tooltip("MUST be the same size and order as Option Buttons. Tick TRUE for the correct option, FALSE for every wrong option.")]
    public bool[] isCorrectOption;

    [Header("Explanation UI")]
    public TMP_Text explanationText;
    [TextArea] public string explanation;
    public Button explanationActionButton;

    [Header("Explanation Entry Buttons")]
    public Button rightExplanationButton;
    public Button wrongExplanationButton;

    [Header("Sprites")]
    public Sprite defaultButtonSprite;
    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;

    // ================= STATE =================
    private bool answeredCorrectly;
    private readonly HashSet<int> wrongAttempts = new HashSet<int>();

    // =================================================

    private void OnEnable()
    {
        if (!ValidateSetup()) return;

        LoadQuestion();
        BindButtons();
        RestoreState();
    }

    bool ValidateSetup()
    {
        if (optionButtons == null || optionButtons.Length == 0)
        {
            Debug.LogError($"[CorrectClickObject] '{name}': Option Buttons array is empty.", this);
            return false;
        }

        if (isCorrectOption == null || isCorrectOption.Length != optionButtons.Length)
        {
            Debug.LogError($"[CorrectClickObject] '{name}': 'Is Correct Option' array size ({(isCorrectOption == null ? 0 : isCorrectOption.Length)}) does not match Option Buttons size ({optionButtons.Length}). Fix this in the Inspector.", this);
            return false;
        }

        bool hasCorrect = false;
        foreach (bool b in isCorrectOption)
            if (b) { hasCorrect = true; break; }

        if (!hasCorrect)
        {
            Debug.LogError($"[CorrectClickObject] '{name}': No option is marked as correct. Tick TRUE on one entry in 'Is Correct Option'.", this);
            return false;
        }

        return true;
    }

    void LoadQuestion()
    {
        if (questionText) questionText.text = question;
        if (explanationText) explanationText.text = explanation;

        if (referenceImage != null)
        {
            if (referenceImageSprite != null)
            {
                referenceImage.gameObject.SetActive(true);
                referenceImage.sprite = referenceImageSprite;
                referenceImage.color = Color.white;
            }
            else
            {
                referenceImage.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i; // local copy so the click listener captures the right index

            optionButtons[i].interactable = true;

            var img = optionButtons[i].GetComponent<Image>();
            if (img && defaultButtonSprite) img.sprite = defaultButtonSprite;

            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }

        if (mcqPanel) mcqPanel.SetActive(true);
        if (explanationPanel) explanationPanel.SetActive(false);
        HideExplanationButtons();
    }

    void RestoreState()
    {
        foreach (int wrong in wrongAttempts)
        {
            optionButtons[wrong].GetComponent<Image>().sprite = wrongSprite;
            optionButtons[wrong].interactable = false;
        }

        if (answeredCorrectly)
        {
            int correctIndex = GetCorrectIndex();
            if (correctIndex >= 0)
                optionButtons[correctIndex].GetComponent<Image>().sprite = correctSprite;

            DisableAllOptions();
            ShowRightExplanation();
        }
    }

    void OnOptionSelected(int index)
    {
        bool isCorrect = isCorrectOption[index];

        if (isCorrect)
        {
            if (answeredCorrectly)
                return; // prevent double trigger

            answeredCorrectly = true;

            PlaySound(correctClip);

            optionButtons[index].GetComponent<Image>().sprite = correctSprite;
            DisableAllOptions();
            ShowRightExplanation();

            // Correct click -> unlock Next / start page navigation
            PageNavigationController.RequestNavigationUnlock();
        }
        else
        {
            wrongAttempts.Add(index);

            PlaySound(wrongClip);

            optionButtons[index].GetComponent<Image>().sprite = wrongSprite;
            optionButtons[index].interactable = false;
            ShowWrongExplanation();
        }
    }

    int GetCorrectIndex()
    {
        for (int i = 0; i < isCorrectOption.Length; i++)
            if (isCorrectOption[i]) return i;
        return -1;
    }

    void BindButtons()
    {
        if (rightExplanationButton)
        {
            rightExplanationButton.onClick.RemoveAllListeners();
            rightExplanationButton.onClick.AddListener(OpenExplanation);
        }

        if (wrongExplanationButton)
        {
            wrongExplanationButton.onClick.RemoveAllListeners();
            wrongExplanationButton.onClick.AddListener(OpenExplanation);
        }

        if (explanationActionButton)
        {
            explanationActionButton.onClick.RemoveAllListeners();
            explanationActionButton.onClick.AddListener(CloseExplanation);
        }
    }

    void OpenExplanation()
    {
        if (mcqPanel) mcqPanel.SetActive(false);
        if (explanationPanel) explanationPanel.SetActive(true);
    }

    void CloseExplanation()
    {
        if (explanationPanel) explanationPanel.SetActive(false);
        if (mcqPanel) mcqPanel.SetActive(true);
    }

    void DisableAllOptions()
    {
        foreach (var btn in optionButtons)
            btn.interactable = false;
    }

    void HideExplanationButtons()
    {
        if (rightExplanationButton) rightExplanationButton.gameObject.SetActive(false);
        if (wrongExplanationButton) wrongExplanationButton.gameObject.SetActive(false);
    }

    void ShowRightExplanation()
    {
        if (rightExplanationButton) rightExplanationButton.gameObject.SetActive(true);
        if (wrongExplanationButton) wrongExplanationButton.gameObject.SetActive(false);
    }

    void ShowWrongExplanation()
    {
        if (wrongExplanationButton) wrongExplanationButton.gameObject.SetActive(true);
        if (rightExplanationButton) rightExplanationButton.gameObject.SetActive(false);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}