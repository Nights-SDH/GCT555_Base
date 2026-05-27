using UnityEngine;

public class OVRControllerPoseFollower : MonoBehaviour
{
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public Transform trackingSpace;
    public bool followPosition = true;
    public bool followRotation = true;

    void Start()
    {
        if (trackingSpace == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
                trackingSpace = rig.trackingSpace;
        }
    }

    void Update()
    {
        Vector3 localPosition = OVRInput.GetLocalControllerPosition(controller);
        Quaternion localRotation = OVRInput.GetLocalControllerRotation(controller);

        if (trackingSpace != null)
        {
            if (followPosition)
                transform.position = trackingSpace.TransformPoint(localPosition);
            if (followRotation)
                transform.rotation = trackingSpace.rotation * localRotation;
        }
        else
        {
            if (followPosition)
                transform.localPosition = localPosition;
            if (followRotation)
                transform.localRotation = localRotation;
        }
    }
}
