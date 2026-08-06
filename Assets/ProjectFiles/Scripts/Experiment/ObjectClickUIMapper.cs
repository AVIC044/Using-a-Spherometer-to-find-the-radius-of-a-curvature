using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ObjectClickUIMapper : MonoBehaviour
{
    [System.Serializable]

    public class ObjectUIMap

    {

        public GameObject object3D;

        public Image uiImage;

        public Image componentImage;



        // [TextArea(3, 6)]

        // // public string infoText;



        [Header(" Right /  Wrong")]

        public bool isRight;

        public bool isWrong;

    }



    public Camera cam;



    [Header("Mappings")]

    public List<ObjectUIMap> mappings;



    [Header("Common Info Panel")]

    public GameObject infoPanel;

    public TMP_Text infoTextUI;

    private Pointer pointer;



    [Header("Global Events")]

    public UnityEvent onRightObjectClicked;    //  Fire when any RIGHT object is clicked

    public UnityEvent onWrongObjectClicked;    // Fire when any WRONG object is clicked

    public UnityEvent onAllObjectsClicked;     // When all objects are clicked



    Dictionary<GameObject, ObjectUIMap> lookup;

    HashSet<GameObject> clickedObjects = new();



    bool eventFired = false;



    void Awake()

    {

        lookup = new Dictionary<GameObject, ObjectUIMap>();



        foreach (var map in mappings)

        {

            if (map.object3D != null)

            {

                lookup[map.object3D] = map;



                if (map.uiImage != null)

                    map.uiImage.gameObject.SetActive(false);



                if (map.componentImage != null)

                    map.componentImage.gameObject.SetActive(false);

            }

        }



        // infoPanel.SetActive(false);

    }



    void Update()

    {

        // Re-fetch each frame in case the device connects/reconnects at runtime,

        // or in case input switches between mouse and touch (e.g. Editor vs. Device Simulator vs. real iPad).

        pointer = Pointer.current;



        if (pointer == null)

        {
            return;
        }



        if (pointer.press.wasPressedThisFrame)

        {

            Vector2 pointerPosition = pointer.position.ReadValue();

            Ray ray = cam.ScreenPointToRay(pointerPosition);

            RaycastHit hit;



            if (Physics.Raycast(ray, out hit))

            {


                if (lookup.TryGetValue(hit.collider.gameObject, out ObjectUIMap clicked))

                {


                    // Track clicked object

                    clickedObjects.Add(clicked.object3D);



                    // ======================

                    // RIGHT /  WRONG LOGIC

                    // ======================

                    if (clicked.isRight)

                    {

                        //  Right objects: show both the labeled UI image and the component highlight

                        if (clicked.uiImage != null)

                            clicked.uiImage.gameObject.SetActive(true);



                        if (clicked.componentImage != null)

                            clicked.componentImage.gameObject.SetActive(true);



                        onRightObjectClicked?.Invoke();

                    }

                    else if (clicked.isWrong)

                    {

                        //  Wrong objects: only the component highlight (no uiImage/name to show)

                        if (clicked.componentImage != null)

                            clicked.componentImage.gameObject.SetActive(true);



                        onWrongObjectClicked?.Invoke();

                    }



                    // ================================

                    //  FIRE EVENT WHEN ALL VISITED

                    // ================================

                    if (!eventFired && clickedObjects.Count == mappings.Count)

                    {

                        eventFired = true;

                        onAllObjectsClicked?.Invoke();

                    }

                }

            }

        }

    }


}
