using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class MultiStepDialerController : MonoBehaviour
{
    [System.Serializable]
    public class DialerPageConfig
    {
        [Header("Page Setup")]
        public string configName;
        public int pageIndex;

        [Header("Input Fields & Answers (Order Matters)")]
        public TMP_InputField[] fields;
        public float[] answers;

        [Header("Question Texts (Optional Hiding)")]
        [Tooltip("Check this box if you want question texts to disappear when answered on this slide.")]
        public bool hideQuestionTextsOnCompletion = false;
        [Tooltip("Question text labels that should be hidden along with input fields.")]
        public TMP_Text[] questionTexts;

        [Header("Answer Text Displays (Shown after correct input)")]
        [Tooltip("Text components placed in the UI to display the verified answer once input field hides.")]
        public TMP_Text[] answerTexts;

        [Header("Feedback Icons (Same Order)")]
        public GameObject[] correctIcons;
        public GameObject[] wrongIcons;

        [Header("Buttons")]
        public Button validateButton;
        public Button autoFillButton;
    }

    [Header("Keypad / Calculator Buttons")]
    [Tooltip("Assign numeric buttons (0-9). The script will automatically assign click listeners based on button text or index.")]
    public Button[] digitButtons;
    public Button decimalButton;
    public Button backspaceButton;

    [Header("Mapped Configurations")]
    [SerializeField] private List<DialerPageConfig> pageConfigs = new List<DialerPageConfig>();

    [Header("Global Settings")]
    public int maxWrongAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Global Events")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;
    public UnityEvent OnAllAnswersVerified;

    [Tooltip("Map this in the Inspector to PageNavigationController.RequestNavigationUnlock")]
    public UnityEvent OnRequestNavigationUnlock;

    private DialerPageConfig activeConfig;
    private int activeFieldIndex;
    private int wrongAttempts;
    private bool solved;

    private void Start()
    {
        SetupKeypadListeners();
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    /// <summary>
    /// Dynamically binds click listeners to calculator buttons in code.
    /// </summary>
    private void SetupKeypadListeners()
    {
        // Setup digit buttons (0-9)
        for (int i = 0; i < digitButtons.Length; i++)
        {
            if (digitButtons[i] != null)
            {
                // Try reading text from button label, otherwise fall back to array index string
                TMP_Text btnText = digitButtons[i].GetComponentInChildren<TMP_Text>();
                string digitValue = (btnText != null && !string.IsNullOrEmpty(btnText.text)) ? btnText.text.Trim() : i.ToString();

                digitButtons[i].onClick.RemoveAllListeners();
                digitButtons[i].onClick.AddListener(() => OnDigitPressed(digitValue));
            }
        }

        // Setup decimal button
        if (decimalButton != null)
        {
            decimalButton.onClick.RemoveAllListeners();
            decimalButton.onClick.AddListener(OnDecimalPressed);
        }

        // Setup backspace button
        if (backspaceButton != null)
        {
            backspaceButton.onClick.RemoveAllListeners();
            backspaceButton.onClick.AddListener(OnBackspacePressed);
        }
    }

    private void HandlePageChanged(int pageIndex)
    {
        activeConfig = pageConfigs.Find(c => c.pageIndex == pageIndex);

        if (activeConfig != null)
        {
            SetupCurrentPage();
        }
    }

    public void SetupCurrentPage()
    {
        solved = false;
        wrongAttempts = 0;
        activeFieldIndex = 0;

        if (activeConfig.validateButton)
        {
            activeConfig.validateButton.onClick.RemoveAllListeners();
            activeConfig.validateButton.onClick.AddListener(OnValidatePressed);
            activeConfig.validateButton.interactable = true;
        }

        // Keep Autofill hidden initially on setup
        if (activeConfig.autoFillButton)
        {
            activeConfig.autoFillButton.onClick.RemoveAllListeners();
            activeConfig.autoFillButton.onClick.AddListener(AutoFillCurrentField);
            activeConfig.autoFillButton.gameObject.SetActive(false);
        }

        for (int i = 0; i < activeConfig.fields.Length; i++)
        {
            if (activeConfig.fields[i] != null)
            {
                activeConfig.fields[i].gameObject.SetActive(true);
                activeConfig.fields[i].text = "";
                activeConfig.fields[i].interactable = (i == 0);
            }

            // Make sure question texts are active on initial setup
            if (i < activeConfig.questionTexts.Length && activeConfig.questionTexts[i] != null)
                activeConfig.questionTexts[i].gameObject.SetActive(true);

            if (i < activeConfig.answerTexts.Length && activeConfig.answerTexts[i] != null)
                activeConfig.answerTexts[i].gameObject.SetActive(false);

            if (i < activeConfig.correctIcons.Length && activeConfig.correctIcons[i] != null)
                activeConfig.correctIcons[i].SetActive(false);

            if (i < activeConfig.wrongIcons.Length && activeConfig.wrongIcons[i] != null)
                activeConfig.wrongIcons[i].SetActive(false);
        }

        if (activeConfig.fields.Length > 0 && activeConfig.fields[0] != null)
        {
            activeConfig.fields[0].Select();
            activeConfig.fields[0].ActivateInputField();
        }
    }

    IEnumerator ShowWrongIcon(int index)
    {
        if (index < activeConfig.wrongIcons.Length && activeConfig.wrongIcons[index] != null)
        {
            activeConfig.wrongIcons[index].SetActive(true);
            yield return new WaitForSeconds(0.7f);
            activeConfig.wrongIcons[index].SetActive(false);
        }

        if (activeConfig.fields[index] != null)
        {
            activeConfig.fields[index].text = "";
            activeConfig.fields[index].Select();
            activeConfig.fields[index].ActivateInputField();
        }
    }

    public void OnDigitPressed(string digit)
    {
        if (solved || activeConfig == null) return;
        if (activeConfig.fields[activeFieldIndex] == null || !activeConfig.fields[activeFieldIndex].interactable) return;

        activeConfig.fields[activeFieldIndex].text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved || activeConfig == null) return;

        TMP_InputField f = activeConfig.fields[activeFieldIndex];
        if (f == null || !f.interactable) return;

        if (!f.text.Contains("."))
        {
            if (string.IsNullOrEmpty(f.text))
                f.text = "0.";
            else
                f.text += ".";
        }
    }

    public void OnBackspacePressed()
    {
        if (solved || activeConfig == null) return;

        TMP_InputField f = activeConfig.fields[activeFieldIndex];
        if (f == null || !f.interactable) return;

        if (f.text.Length > 0)
            f.text = f.text.Substring(0, f.text.Length - 1);
    }

    public void OnValidatePressed()
    {
        if (solved || activeConfig == null) return;

        TMP_InputField current = activeConfig.fields[activeFieldIndex];
        if (current == null) return;

        if (!float.TryParse(current.text, out float value))
            return;

        // Check if the answer is wrong
        if (Mathf.Abs(value - activeConfig.answers[activeFieldIndex]) > tolerance)
        {
            wrongAttempts++;
            OnWrongAnswer?.Invoke();
            StartCoroutine(ShowWrongIcon(activeFieldIndex));

            // Show autofill ONLY when max wrong attempts are reached or exceeded
            if (wrongAttempts >= maxWrongAttempts && activeConfig.autoFillButton != null)
            {
                activeConfig.autoFillButton.gameObject.SetActive(true);
            }

            return;
        }

        // If correct, complete field
        CompleteCurrentField();
    }

    public void AutoFillCurrentField()
    {
        if (solved || activeConfig == null) return;

        if (activeConfig.fields[activeFieldIndex] != null)
        {
            activeConfig.fields[activeFieldIndex].text = activeConfig.answers[activeFieldIndex].ToString();
        }

        CompleteCurrentField();
    }

    private void CompleteCurrentField()
    {
        TMP_InputField current = activeConfig.fields[activeFieldIndex];

        // 1. Hide the input field
        if (current != null)
        {
            current.interactable = false;
            current.gameObject.SetActive(false);
        }

        // 2. Hide question text if enabled for this slide
        if (activeConfig.hideQuestionTextsOnCompletion)
        {
            if (activeFieldIndex < activeConfig.questionTexts.Length && activeConfig.questionTexts[activeFieldIndex] != null)
            {
                activeConfig.questionTexts[activeFieldIndex].gameObject.SetActive(false);
            }
        }

        // 3. Display the answer text
        if (activeFieldIndex < activeConfig.answerTexts.Length && activeConfig.answerTexts[activeFieldIndex] != null)
        {
            // activeConfig.answerTexts[activeFieldIndex].text = current != null ? current.text : activeConfig.answers[activeFieldIndex].ToString();
            activeConfig.answerTexts[activeFieldIndex].gameObject.SetActive(true);
        }

        // 4. Show correct icon feedback
        if (activeFieldIndex < activeConfig.correctIcons.Length && activeConfig.correctIcons[activeFieldIndex] != null)
            activeConfig.correctIcons[activeFieldIndex].SetActive(true);

        OnCorrectAnswer?.Invoke();

        // 5. Reset wrong attempts & hide Autofill button for the next field
        wrongAttempts = 0;
        if (activeConfig.autoFillButton != null)
        {
            activeConfig.autoFillButton.gameObject.SetActive(false);
        }

        activeFieldIndex++;

        // Enable next field or complete slide
        if (activeFieldIndex < activeConfig.fields.Length)
        {
            if (activeConfig.fields[activeFieldIndex] != null)
            {
                activeConfig.fields[activeFieldIndex].gameObject.SetActive(true);
                activeConfig.fields[activeFieldIndex].interactable = true;
                activeConfig.fields[activeFieldIndex].Select();
                activeConfig.fields[activeFieldIndex].ActivateInputField();
            }
        }
        else
        {
            FinishPuzzle();
        }
    }

    void FinishPuzzle()
    {
        solved = true;
        activeFieldIndex = activeConfig.fields.Length;

        if (activeConfig.validateButton)
            activeConfig.validateButton.interactable = false;

        if (activeConfig.autoFillButton)
            activeConfig.autoFillButton.gameObject.SetActive(false);

        foreach (TMP_InputField f in activeConfig.fields)
        {
            if (f != null)
                f.interactable = false;
        }

        OnRequestNavigationUnlock?.Invoke();
    }
}
