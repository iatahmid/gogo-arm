using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class GoGoShadowARM : MonoBehaviour {

    private Camera playerCamera;

#if SteamVR_Legacy
    public SteamVR_TrackedObject trackedObj;

    // ARM Integration
    private SteamVR_Controller.Device Controller
    {
        get
        {
            return SteamVR_Controller.Input((int)trackedObj.index);
        }
    }

    private SteamVR_Controller.Device device;
#elif SteamVR_2
    public SteamVR_Behaviour_Pose trackedObj;
    public SteamVR_Action_Boolean m_touchpadPress;
#else
    public GameObject trackedObj;
#endif

    public GameObject cameraRig; // So shadow can attach itself to the camera rig on game start

    public enum ToggleArmLengthCalculator {
        on,
        off
    }
    // If toggled on the user can press down on the touchpad with their arm extended to take a measurement of the arm
    // If it is off the user must inut a manual estimate of what the users arm length would be
    public ToggleArmLengthCalculator armLengthCalculator = ToggleArmLengthCalculator.off;

    public float armLength; // Either manually inputted or will be set to the arm length when calculated

    public float distanceFromHeadToChest = 0.3f; // estimation of the distance from the users headset to their chest area

    public GameObject theController; // controller for the gogo to access inout

    public GameObject theModel; // the model of the controller that will be shadowed for gogo use

    public float extensionVariable = 10f; // this variable in the equation controls the multiplier for how far the arm can extend with small movements

    bool calibrated = false;
    Vector3 chestPosition;
    Vector3 relativeChestPos;

    // ARM Variables
    private bool ARMOn = false;
    private Vector3 lastDirectionPointing;
    private Quaternion lastRotation;
    private Vector3 lastPosition;

    // velocity tracking
    LinkedList<float> velocities = new LinkedList<float>();
    float velocityThreshold = 0.2f;


    // ARM Controller for Toggling
    public enum ControllerState
    {
        TRIGGER_DOWN, TOUCHPAD_UP, TOUCHPAD_DOWN, NONE,
        TRIGGER_HALF_DOWN
    }

    void makeModelChild() {
        if (this.transform.childCount == 0) {
            if (theModel.GetComponent<SteamVR_RenderModel>() != null) { // The steamVR_RenderModel is generated after code start so we cannot parent right away or it wont generate. 
                if (theModel.transform.childCount > 0) {
                    theModel.transform.parent = this.transform;
                    // Due to the transfer happening at a random time down the line we need to re-align the model inside the shadow controller to 0 so nothing is wonky.
                    theModel.transform.localPosition = Vector3.zero;
                    theModel.transform.localRotation = Quaternion.identity;
                }
            } else {
                // If it is just a custom model we can immediately parent
                theModel.transform.parent = this.transform;
                // Due to the transfer happening at a random time down the line we need to re-align the model inside the shadow controller to 0 so nothing is wonky.
                theModel.transform.localPosition = Vector3.zero;
                theModel.transform.localRotation = Quaternion.identity;
            }
        }

    }

    // Might have to have a manuel calibration for best use
    float getDistanceToExtend() {
        // estimating chest position using an assumed distance from head to chest and then going that distance down the down vector of the camera. This will not allways be optimal especially when leaning is involved.
        // To improve gogo to suite your needs all you need to do is implement your own algorithm to estimate chest (or shoulder for even high accuracy) position and set the chest position vector to match it

        Vector3 direction = playerCamera.transform.up * -1;
        Vector3 normalizedDirectionPlusDistance = direction.normalized * distanceFromHeadToChest;
        chestPosition = playerCamera.transform.position + normalizedDirectionPlusDistance;

        float distChestPos = Vector3.Distance(trackedObj.transform.position, chestPosition);

        float D = (2f * armLength) / 3f; // 2/3 of users arm length
        //ibrahim
        //float D = armLength;
        //D = 0;
        if (distChestPos >= D) {
            float extensionDistance = distChestPos + (extensionVariable * (float)Math.Pow(distChestPos - D, 2));
            // Dont need both here as we only want the distance to extend by not the full distance
            // but we want to keep the above formula matching the original papers formula so will then calculate just the distance to extend below
            return extensionDistance - distChestPos;
        }
        return 0; // dont extend
    }

    // Use this for initialization
    void Start() {
        this.transform.parent = cameraRig.transform;
        if (Camera.main != null) {
            playerCamera = Camera.main;
        } else {
            playerCamera = cameraRig.GetComponentInChildren<Camera>();
        }
        makeModelChild();

        lastDirectionPointing = theController.transform.forward;
        lastRotation = theController.transform.rotation;
        lastPosition = theController.transform.position;
    }


    // Update is called once per frame
    void Update() {

        makeModelChild();
        //this.GetComponentInChildren<SteamVR_RenderModel>().gameObject.SetActive(false);
        Renderer[] renderers = this.transform.parent.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) {
            if (renderer.material.name == "Standard (Instance)") {
                renderer.enabled = true;
            }
        }

        checkForAction();

        // Calculate the GoGo position of the controller
        Vector3 gogoPos = moveControllerForward();

        /************************************************************
        * Toggle ARM/GoGo with Controller Speed START
        * **********************************************************/
        float controllerVelocity = Controller.velocity.magnitude;
        Debug.Log("Controller Velocity: " + controllerVelocity);

        velocities.AddLast(controllerVelocity);

        // ensure a finite Queue
        if (velocities.Count > 20)
        {
            velocities.RemoveFirst();
        }

        updatePositionAndRotationToFollowControllerSpeed(gogoPos, controllerVelocity);

        if (!ARMOn && velocities.First.Value < velocityThreshold && controllerVelocity < velocityThreshold)
        {
            lastDirectionPointing = trackedObj.transform.forward;
            lastRotation = this.transform.rotation;
            lastPosition = this.transform.position;
            Debug.Log("Last Position: " + lastPosition);
            ARMOn = !ARMOn;
        }
        else if (controllerVelocity >= velocityThreshold)
        {
            ARMOn = false;
        }
        /************************************************************
         * Toggle ARM/GoGo with Controller Speed END
         * **********************************************************/


        /************************************************************
         * Toggle ARM/GoGo with TouchPad Press START
         * **********************************************************/
        //updatePositionAndRotationToFollowControllerHalfPress(gogoPos);
        //if (controllerEvents() == ControllerState.TOUCHPAD_DOWN)
        //{
        //    toggleARM();
        //}
        /************************************************************
         * Toggle ARM/GoGo with TouchPad Press END
         * **********************************************************/


        //if (!ARMOn && controllerEvents() == ControllerState.TRIGGER_HALF_DOWN)
        //{
        //    toggleARM();
        //}
        //else if (ARMOn && controllerEvents() != ControllerState.TRIGGER_HALF_DOWN)
        //{
        //    toggleARM();
        //}
    }

    void updatePositionAndRotationToFollowControllerSpeed(Vector3 gogoPosition, float controllerVelocity)
    {
        //Debug.Log("This Transform Position: " + this.transform.position);
        //Debug.Log("TrackedObject Transform Position: " + trackedObj.transform.position);
        //Debug.Log("TheController Transform Position: " + theController.transform.position);
        //Debug.Log("GoGo Position: " + gogoPosition);

        //this.transform.position = trackedObj.transform.position;
        Quaternion rotationOfDevice = trackedObj.transform.rotation;
        if (ARMOn)
        {
            // scaled down by factor of 10
            this.transform.rotation = Quaternion.Lerp(lastRotation, rotationOfDevice, 0.2f);
            this.transform.position = Vector3.Lerp(lastPosition, gogoPosition, 0.2f);

            print("ARM On");
        }
        else
        {
            this.transform.rotation = trackedObj.transform.rotation;
            this.transform.position = gogoPosition;
        }
    }

    void updatePositionAndRotationToFollowControllerDistance(Vector3 gogoPosition)
    {
        Debug.Log("This Transform Position: " + this.transform.position);
        Debug.Log("TrackedObject Transform Position: " + trackedObj.transform.position);
        Debug.Log("TheController Transform Position: " + theController.transform.position);
        Debug.Log("GoGo Position: " + gogoPosition);

        //this.transform.position = trackedObj.transform.position;
        Quaternion rotationOfDevice = trackedObj.transform.rotation;
        if (ARMOn)
        {
            float scaleFactor = 1 / (1f * Vector3.Distance(trackedObj.transform.position, gogoPosition));
            //scaleFactor = Math.Round(scaleFactor, 1);
            if(scaleFactor > 1)
            {
                lastRotation = this.transform.rotation;
                lastPosition = this.transform.position;
                scaleFactor = 1;
            } else if(scaleFactor < 0.1)
            {
                //lastRotation = this.transform.rotation;
                //lastPosition = this.transform.position;
                scaleFactor = 0.1f;
            }
            Debug.Log("ScaleFactor: " + scaleFactor);
            // scaled down by factor of 10
            //this.transform.rotation = Quaternion.Lerp(lastRotation, rotationOfDevice, 0.1f);
            //this.transform.position = Vector3.Lerp(lastPosition, gogoPosition, 0.1f);
            this.transform.rotation = Quaternion.Lerp(lastRotation, rotationOfDevice, scaleFactor);
            this.transform.position = Vector3.Lerp(lastPosition, gogoPosition, scaleFactor);

            print("On");
        }
        else
        {
            this.transform.rotation = trackedObj.transform.rotation;
            this.transform.position = gogoPosition;
        }
    }

    void updatePositionAndRotationToFollowControllerHalfPress(Vector3 gogoPosition)
    {
        //this.transform.position = trackedObj.transform.position;
        Quaternion rotationOfDevice = trackedObj.transform.rotation;
        if (ARMOn)
        {
            // scaled down by factor of 10
            this.transform.rotation = Quaternion.Lerp(lastRotation, rotationOfDevice, 0.1f);
            this.transform.position = Vector3.Lerp(lastPosition, gogoPosition, 0.1f);

            print("On");
        }
        else
        {
            this.transform.rotation = trackedObj.transform.rotation;
            this.transform.position = gogoPosition;
        }
    }

    void updatePositionAndRotationToFollowControllerHalfPress()
    {
        this.transform.position = trackedObj.transform.position;
        Quaternion rotationOfDevice = trackedObj.transform.rotation;
        if (ARMOn)
        {
            // scaled down by factor of 10
            this.transform.rotation = Quaternion.Lerp(lastRotation, rotationOfDevice, 0.1f);
            this.transform.position = Vector3.Lerp(lastPosition, trackedObj.transform.position, 0.1f);

            print("On");
        }
        else
        {
            this.transform.rotation = trackedObj.transform.rotation;
            this.transform.position = trackedObj.transform.position;
        }
    }

    Vector3 moveControllerForward() {
        // Using the origin and the forward vector of the remote the extended positon of the remote can be calculated
        //Vector3 theVector = theController.transform.forward;
        Vector3 theVector = theController.transform.position - chestPosition;

        Vector3 pose = theController.transform.position;
        Quaternion rot = theController.transform.rotation;

        float distance_formula_on_vector = Mathf.Sqrt(theVector.x * theVector.x + theVector.y * theVector.y + theVector.z * theVector.z);

        float distanceToExtend = getDistanceToExtend();

        if (distanceToExtend != 0) {
            // Using formula to find a point which lies at distance on a 3D line from vector and direction
            pose.x = pose.x + (distanceToExtend / (distance_formula_on_vector)) * theVector.x;
            pose.y = pose.y + (distanceToExtend / (distance_formula_on_vector)) * theVector.y;
            pose.z = pose.z + (distanceToExtend / (distance_formula_on_vector)) * theVector.z;
        }
        
        return pose;
    }
    

    private ControllerState controllerEvents() {
#if SteamVR_Legacy
        if (device.GetPressUp(SteamVR_Controller.ButtonMask.Axis0)) {
            return ControllerState.TOUCHPAD_UP;
        }
        if (Controller.GetPressDown(SteamVR_Controller.ButtonMask.Touchpad))
        {
            return ControllerState.TOUCHPAD_DOWN;
        }
        if (Controller.GetHairTriggerDown())
        {
            return ControllerState.TRIGGER_DOWN;
        }

        Vector2 triggerPressure = device.GetAxis(EVRButtonId.k_EButton_SteamVR_Trigger);
        if(0.2f < triggerPressure.x)
        {
            return ControllerState.TRIGGER_HALF_DOWN;
        }

#elif SteamVR_2
        if (m_touchpadPress.GetStateUp(trackedObj.inputSource)) {
            return ControllerState.TOUCHPAD_UP;
        }
#endif
        return ControllerState.NONE;
    }

    void toggleARM()
    {
        if (!ARMOn)
        {
            lastDirectionPointing = trackedObj.transform.forward;
            lastRotation = this.transform.rotation;
            lastPosition = this.transform.position;
        }
        ARMOn = !ARMOn;
    }

    void checkForAction() {
#if SteamVR_Legacy
        device = SteamVR_Controller.Input((int)trackedObj.index);
#endif
        if (armLengthCalculator == ToggleArmLengthCalculator.on && controllerEvents() == ControllerState.TOUCHPAD_UP) //(will only register if arm length calculator is on)
        {
            armLength = Vector3.Distance(trackedObj.transform.position, chestPosition);
            calibrated = true;
        }
    }
}
