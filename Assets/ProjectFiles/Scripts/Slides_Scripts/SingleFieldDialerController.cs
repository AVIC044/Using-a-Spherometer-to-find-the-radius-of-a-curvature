using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class SingleFieldDialerController : MonoBehaviour
{
    [Header("Input")]
    public TMP_InputField[] answerFields;
    [Tooltip("Which answer field is used on this slide")]
    public int activeFieldIndex = 0;

    [Header("Correct Answer")]
    public float[] correctAnswers;

    [Header("Icons")]
    public GameObject[] successIcons;
    public GameObject[] wrongIcons;

    private GameObject ActiveSuccessIcon => successIcons[activeFieldIndex];
    private GameObject ActiveWrongIcon => wrongIcons[activeFieldIndex];

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;

    [Header("Settings")]
    public int maxWrongAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Events")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;
    public UnityEvent OnAllAnswersVerified;

    private int wrongAttempts;
    private bool solved;

    private PageNavigationController slideController;

    private TMP_InputField ActiveField => answerFields[activeFieldIndex];

    private float ActiveAnswer => correctAnswers[activeFieldIndex];

    void Start()
    {
        slideController = FindFirstObjectByType<PageNavigationController>();

        ResetAll();

        validateButton.onClick.AddListener(OnValidatePressed);
        autoFillButton.onClick.AddListener(AutoFill);

        autoFillButton.gameObject.SetActive(false);
        if (activeFieldIndex < 0 || activeFieldIndex >= answerFields.Length)
        {
            Debug.LogError($"Active Field Index ({activeFieldIndex}) is out of range.");
            enabled = false;
            return;
        }
        if (answerFields.Length != correctAnswers.Length)
        {
            Debug.LogError("Answer Fields and Correct Answers arrays must be the same size.");
            enabled = false;
            return;
        }
    }



    IEnumerator ShowWrongIcon()
    {
        ActiveWrongIcon.SetActive(true);

        yield return new WaitForSeconds(0.7f);

        ActiveWrongIcon.SetActive(false);

        ActiveField.text = "";

        ActiveField.Select();
        ActiveField.ActivateInputField();
    }

    public void OnDigitPressed(string digit)
    {
        if (solved) return;

        ActiveField.text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved) return;

        if (!ActiveField.text.Contains("."))
        {
            if (ActiveField.text == "")
                ActiveField.text = "0.";
            else
                ActiveField.text += ".";
        }
    }

    public void OnBackspacePressed()
    {
        if (solved) return;

        if (ActiveField.text.Length > 0)
        {
            ActiveField.text =
                ActiveField.text.Substring(0, ActiveField.text.Length - 1);
        }
    }

    public void OnValidatePressed()
    {
        if (solved) return;

        if (!float.TryParse(ActiveField.text, out float value))
            return;

        if (Mathf.Abs(value - ActiveAnswer) > tolerance)
        {
            StartCoroutine(ShowWrongIcon());
            wrongAttempts++;
            OnWrongAnswer?.Invoke();

            if (wrongAttempts >= maxWrongAttempts)
                autoFillButton.gameObject.SetActive(true);

            return;
        }

        ActiveSuccessIcon.SetActive(true);
        ActiveField.interactable = false;

        OnCorrectAnswer?.Invoke();


        FinishPuzzle();
    }

    public void AutoFill()
    {
        ActiveField.text = ActiveAnswer.ToString();
        ActiveField.interactable = false;

        ActiveSuccessIcon.SetActive(true);

        FinishPuzzle();
    }

    void FinishPuzzle()
    {
        solved = true;

        ActiveField.interactable = false;
        validateButton.interactable = false;
        autoFillButton.gameObject.SetActive(false);

        slideController?.EnableNavigationButtons();

        OnAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        wrongAttempts = 0;

        ActiveSuccessIcon.SetActive(false);
        ActiveWrongIcon.SetActive(false);

        validateButton.interactable = true;
        autoFillButton.gameObject.SetActive(false);

        for (int i = 0; i < answerFields.Length; i++)
        {
            answerFields[i].text = "";
            answerFields[i].interactable = false;
        }

        ActiveField.interactable = true;

        ActiveField.Select();
        ActiveField.ActivateInputField();
    }
}
