using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class AllObjectsClickTracker : MonoBehaviour
{
    [Header("Objects To Track")]
    public List<GameObject> clickableObjects = new List<GameObject>();

    [Header("Progress Texts")]
    public List<TMP_Text> progressTexts = new List<TMP_Text>();

    [Header("Event")]
    public UnityEvent OnAllObjectsClicked;

    private HashSet<GameObject> clickedObjects = new HashSet<GameObject>();
    private bool eventTriggered = false;

    private void Start()
    {
        UpdateProgressText();
    }

    // Call this whenever an object is clicked
    public void RegisterClick(GameObject clickedObject)
    {
        if (eventTriggered)
            return;

        if (clickableObjects.Contains(clickedObject))
        {
            // HashSet automatically ignores duplicate clicks
            if (clickedObjects.Add(clickedObject))
            {
                UpdateProgressText();

                Debug.Log($"Clicked: {clickedObject.name} ({clickedObjects.Count}/{clickableObjects.Count})");

                if (clickedObjects.Count == clickableObjects.Count)
                {
                    eventTriggered = true;
                    Debug.Log("✅ All objects clicked!");
                    OnAllObjectsClicked?.Invoke();
                }
            }
        }
    }

    private void UpdateProgressText()
    {
        string progress = $"{clickedObjects.Count}/{clickableObjects.Count}";

        foreach (TMP_Text txt in progressTexts)
        {
            if (txt != null)
                txt.text = progress;
        }
    }

    public void ResetTracker()
    {
        clickedObjects.Clear();
        eventTriggered = false;
        UpdateProgressText();
    }
}