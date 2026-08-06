using UnityEngine;
using UnityEngine.EventSystems;

public class WhyClick : MonoBehaviour, IPointerClickHandler
{
    public GameObject explanationText;

    void Start()
    {
        explanationText.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        explanationText.SetActive(true);
    }
}