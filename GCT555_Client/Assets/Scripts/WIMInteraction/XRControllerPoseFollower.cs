using UnityEngine;
using UnityEngine.XR;

public class XRControllerPoseFollower : MonoBehaviour
{
    public XRNode controllerNode = XRNode.RightHand;
    public bool followPosition = true;
    public bool followRotation = true;

    void Update()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid)
            return;

        if (followPosition && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            transform.position = position;

        if (followRotation && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            transform.rotation = rotation;
    }
}
