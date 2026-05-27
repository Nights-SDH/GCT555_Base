using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

public class XRHandPinchReader : MonoBehaviour
{
    public enum TargetHand
    {
        Left,
        Right
    }

    [Header("Hand")]
    public TargetHand targetHand = TargetHand.Left;

    [Tooltip("XR Origin transform. 비워두면 씬에서 XROrigin을 자동 탐색한다.")]
    public Transform xrOriginTransform;

    [Header("Pinch Thresholds, meters")]
    [Tooltip("이 거리 이하가 되면 pinch 시작으로 판단")]
    public float pinchEnterDistance = 0.035f;

    [Tooltip("이 거리 이상이 되면 pinch 해제로 판단. enter보다 크게 두면 흔들림이 줄어든다.")]
    public float pinchExitDistance = 0.055f;

    public bool HasValidData { get; private set; }

    public bool IndexPinching { get; private set; }
    public bool MiddlePinching { get; private set; }

    public Vector3 IndexPinchWorld { get; private set; }
    public Vector3 MiddlePinchWorld { get; private set; }
    public Vector3 PalmWorld { get; private set; }

    private XRHandSubsystem _handSubsystem;
    private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
    private int _lastRefreshFrame = -1;

    private void Start()
    {
        TryFindHandSubsystem();

        if (xrOriginTransform == null)
        {
            XROrigin origin = FindObjectOfType<XROrigin>();
            if (origin != null)
                xrOriginTransform = origin.transform;
        }
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

        if (_handSubsystem == null || !_handSubsystem.running)
            TryFindHandSubsystem();

        if (_handSubsystem == null || !_handSubsystem.running)
        {
            ClearState();
            return;
        }

        XRHand hand = targetHand == TargetHand.Left
            ? _handSubsystem.leftHand
            : _handSubsystem.rightHand;

        if (!hand.isTracked)
        {
            ClearState();
            return;
        }

        HasValidData = true;

        bool hasThumb = TryGetJointWorldPose(hand, XRHandJointID.ThumbTip, out Pose thumbPose);
        bool hasIndex = TryGetJointWorldPose(hand, XRHandJointID.IndexTip, out Pose indexPose);
        bool hasMiddle = TryGetJointWorldPose(hand, XRHandJointID.MiddleTip, out Pose middlePose);

        if (TryGetJointWorldPose(hand, XRHandJointID.Palm, out Pose palmPose))
            PalmWorld = palmPose.position;

        if (hasThumb && hasIndex)
        {
            float indexDistance = Vector3.Distance(thumbPose.position, indexPose.position);
            IndexPinching = ApplyHysteresis(IndexPinching, indexDistance);
            IndexPinchWorld = (thumbPose.position + indexPose.position) * 0.5f;
        }
        else
        {
            IndexPinching = false;
        }

        if (hasThumb && hasMiddle)
        {
            float middleDistance = Vector3.Distance(thumbPose.position, middlePose.position);
            MiddlePinching = ApplyHysteresis(MiddlePinching, middleDistance);
            MiddlePinchWorld = (thumbPose.position + middlePose.position) * 0.5f;
        }
        else
        {
            MiddlePinching = false;
        }
    }

    private void TryFindHandSubsystem()
    {
        _subsystems.Clear();
        SubsystemManager.GetSubsystems(_subsystems);

        for (int i = 0; i < _subsystems.Count; i++)
        {
            if (_subsystems[i] != null && _subsystems[i].running)
            {
                _handSubsystem = _subsystems[i];
                return;
            }
        }

        _handSubsystem = null;
    }

    private bool TryGetJointWorldPose(XRHand hand, XRHandJointID jointId, out Pose worldPose)
    {
        XRHandJoint joint = hand.GetJoint(jointId);

        if (!joint.TryGetPose(out Pose localPose))
        {
            worldPose = Pose.identity;
            return false;
        }

        if (xrOriginTransform != null)
        {
            Pose originPose = new Pose(xrOriginTransform.position, xrOriginTransform.rotation);
            worldPose = localPose.GetTransformedBy(originPose);
        }
        else
        {
            worldPose = localPose;
        }

        return true;
    }

    private bool ApplyHysteresis(bool currentState, float distance)
    {
        if (currentState)
            return distance <= pinchExitDistance;

        return distance <= pinchEnterDistance;
    }

    private void ClearState()
    {
        HasValidData = false;
        IndexPinching = false;
        MiddlePinching = false;
    }
}