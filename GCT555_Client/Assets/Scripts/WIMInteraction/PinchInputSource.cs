using UnityEngine;
using UnityEngine.InputSystem;

public class PinchInputSource : MonoBehaviour
{
    public enum PinchKind
    {
        IndexThumb,
        MiddleThumb
    }

    [Header("Optional Hand Tracking Source")]
    public XRHandPinchReader handReader;
    public PinchKind handPinchKind = PinchKind.IndexThumb;

    [Header("Optional Controller Source")]
    [Tooltip("오른손 컨트롤러 Trigger/Select action을 연결한다.")]
    public InputActionReference controllerSelectAction;

    [Tooltip("컨트롤러를 pinch point처럼 사용할 위치. 보통 Right Controller 아래 RayOrigin 또는 Tip 오브젝트.")]
    public Transform controllerInteractionPoint;

    [Range(0.01f, 1.0f)]
    public float controllerPressThreshold = 0.5f;

    public bool IsActive { get; private set; }
    public bool PressedThisFrame { get; private set; }
    public bool ReleasedThisFrame { get; private set; }

    public Vector3 InteractionPointWorld { get; private set; }

    private bool _controllerActive;
    private bool _handActive;
    private int _lastRefreshFrame = -1;

    private void OnEnable()
    {
        if (controllerSelectAction != null && controllerSelectAction.action != null)
            controllerSelectAction.action.Enable();
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_lastRefreshFrame == Time.frameCount)
            return;

        _lastRefreshFrame = Time.frameCount;

        bool previous = IsActive;

        _controllerActive = ReadControllerActive();
        _handActive = ReadHandActive();

        IsActive = _controllerActive || _handActive;
        PressedThisFrame = !previous && IsActive;
        ReleasedThisFrame = previous && !IsActive;

        if (_controllerActive && controllerInteractionPoint != null)
        {
            InteractionPointWorld = controllerInteractionPoint.position;
        }
        else if (_handActive && handReader != null)
        {
            InteractionPointWorld = handPinchKind == PinchKind.IndexThumb
                ? handReader.IndexPinchWorld
                : handReader.MiddlePinchWorld;
        }
    }

    private bool ReadControllerActive()
    {
        if (controllerSelectAction == null || controllerSelectAction.action == null)
            return false;

        float value = controllerSelectAction.action.ReadValue<float>();
        return value >= controllerPressThreshold;
    }

    private bool ReadHandActive()
    {
        if (handReader == null)
            return false;

        handReader.Refresh();

        if (!handReader.HasValidData)
            return false;

        return handPinchKind == PinchKind.IndexThumb
            ? handReader.IndexPinching
            : handReader.MiddlePinching;
    }
}