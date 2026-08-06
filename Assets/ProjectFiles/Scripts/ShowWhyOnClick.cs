using UnityEngine;

public class ShowWhyOnClick : MonoBehaviour
{
    public GameObject whyObject;

    void Start()
    {
        // Game start hote hi Why hide rahega
        if (whyObject != null)
            whyObject.SetActive(false);
    }

    void OnMouseDown()
    {
        // Eye par click karne ke baad Why show hoga
        if (whyObject != null)
            whyObject.SetActive(true);
    }
}