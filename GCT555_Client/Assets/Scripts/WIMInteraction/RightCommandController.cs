using System.Collections.Generic;
using UnityEngine;

public class RightCommandController : MonoBehaviour
{
    private enum RuntimeCommandMode
    {
        Idle,
        PuppetDragging,
        SymbolicAiming
    }

    [Header("Managers")]
    public ExperimentConditionManager conditionManager;
    public PinchInputSource rightCommandInput;

    [Header("Command Space")]
    [Tooltip("보통 WIMRoot. 출력 명령은 이 transform 기준 local 좌표로 변환된다.")]
    public Transform commandSpace;

    [Header("Puppet Condition")]
    public Transform puppetFigure;

    [Tooltip("true면 입력점이 피규어 근처에 있을 때만 Puppet 선택 가능")]
    public bool requirePuppetNearInput = true;

    public float puppetGrabRadius = 0.08f;
    public float puppetMoveGain = 1.0f;

    [Header("Symbolic Condition")]
    [Tooltip("오른손 컨트롤러 또는 RayOrigin. forward 방향으로 raycast한다.")]
    public Transform rightRayOrigin;

    public LayerMask targetSurfaceMask = Physics.DefaultRaycastLayers;
    public float rayMaxDistance = 10.0f;

    [Header("Visual Feedback")]
    public Transform targetMarker;
    public LineRenderer trajectoryLine;

    [Header("Sampling")]
    public float minSampleDistance = 0.01f;

    private RuntimeCommandMode _runtimeMode = RuntimeCommandMode.Idle;

    private Vector3 _startInputPointWorld;
    private Vector3 _startPuppetPositionWorld;

    private Vector3 _lastWorldTarget;
    private bool _hasLastWorldTarget;

    private readonly List<Vector3> _trajectoryWorld = new List<Vector3>();
    private readonly List<Vector3> _trajectoryCommandSpace = new List<Vector3>();

    private void Update()
    {
        if (conditionManager == null || rightCommandInput == null)
            return;

        rightCommandInput.Refresh();

        if (_runtimeMode == RuntimeCommandMode.PuppetDragging && !conditionManager.IsPuppet)
            CancelCommand();

        if (_runtimeMode == RuntimeCommandMode.SymbolicAiming && !conditionManager.IsSymbolic)
            CancelCommand();

        if (conditionManager.IsPuppet)
            UpdatePuppetCondition();
        else
            UpdateSymbolicCondition();
    }

    private void UpdatePuppetCondition()
    {
        if (puppetFigure == null)
            return;

        Vector3 inputPoint = rightCommandInput.InteractionPointWorld;

        if (_runtimeMode == RuntimeCommandMode.Idle && rightCommandInput.PressedThisFrame)
        {
            bool canGrab = !requirePuppetNearInput ||
                           Vector3.Distance(inputPoint, puppetFigure.position) <= puppetGrabRadius;

            if (canGrab)
                BeginPuppetDrag(inputPoint);
        }

        if (_runtimeMode != RuntimeCommandMode.PuppetDragging)
            return;

        if (rightCommandInput.IsActive)
        {
            Vector3 delta = inputPoint - _startInputPointWorld;
            Vector3 newPuppetPosition = _startPuppetPositionWorld + delta * puppetMoveGain;

            puppetFigure.position = newPuppetPosition;
            SetTargetMarker(newPuppetPosition);
            AddSampleIfNeeded(newPuppetPosition);
        }

        if (rightCommandInput.ReleasedThisFrame)
        {
            CommitCommand(puppetFigure.position);
        }
    }

    private void BeginPuppetDrag(Vector3 inputPointWorld)
    {
        BeginCommand();

        _runtimeMode = RuntimeCommandMode.PuppetDragging;
        _startInputPointWorld = inputPointWorld;
        _startPuppetPositionWorld = puppetFigure.position;

        SetTargetMarker(puppetFigure.position);
        AddSampleIfNeeded(puppetFigure.position, true);
    }

    private void UpdateSymbolicCondition()
    {
        bool hasHit = TryGetSymbolicRayHit(out Vector3 hitPointWorld);

        if (_runtimeMode == RuntimeCommandMode.Idle)
        {
            if (hasHit)
                SetTargetMarker(hitPointWorld);

            if (rightCommandInput.PressedThisFrame && hasHit)
                BeginSymbolicAim(hitPointWorld);

            return;
        }

        if (_runtimeMode != RuntimeCommandMode.SymbolicAiming)
            return;

        if (rightCommandInput.IsActive && hasHit)
        {
            _lastWorldTarget = hitPointWorld;
            _hasLastWorldTarget = true;

            SetTargetMarker(hitPointWorld);
            AddSampleIfNeeded(hitPointWorld);
        }

        if (rightCommandInput.ReleasedThisFrame)
        {
            if (_hasLastWorldTarget)
                CommitCommand(_lastWorldTarget);
            else
                CancelCommand();
        }
    }

    private void BeginSymbolicAim(Vector3 hitPointWorld)
    {
        BeginCommand();

        _runtimeMode = RuntimeCommandMode.SymbolicAiming;
        _lastWorldTarget = hitPointWorld;
        _hasLastWorldTarget = true;

        SetTargetMarker(hitPointWorld);
        AddSampleIfNeeded(hitPointWorld, true);
    }

    private bool TryGetSymbolicRayHit(out Vector3 hitPointWorld)
    {
        hitPointWorld = Vector3.zero;

        if (rightRayOrigin == null)
            return false;

        Ray ray = new Ray(rightRayOrigin.position, rightRayOrigin.forward);

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayMaxDistance,
                targetSurfaceMask,
                QueryTriggerInteraction.Ignore))
        {
            hitPointWorld = hit.point;
            return true;
        }

        return false;
    }

    private void BeginCommand()
    {
        _trajectoryWorld.Clear();
        _trajectoryCommandSpace.Clear();

        _hasLastWorldTarget = false;

        if (trajectoryLine != null)
            trajectoryLine.positionCount = 0;
    }

    private void AddSampleIfNeeded(Vector3 worldPosition, bool force = false)
    {
        if (!conditionManager.IsContinuous)
            return;

        if (!force && _trajectoryWorld.Count > 0)
        {
            float distance = Vector3.Distance(
                _trajectoryWorld[_trajectoryWorld.Count - 1],
                worldPosition
            );

            if (distance < minSampleDistance)
                return;
        }

        _trajectoryWorld.Add(worldPosition);
        _trajectoryCommandSpace.Add(ToCommandSpace(worldPosition));

        UpdateTrajectoryLine();

        // Continuous 조건에서 매 sample마다 기존 알고리즘/로봇/시뮬레이션으로 보내고 싶으면 여기 연결.
        EmitContinuousSample(_trajectoryCommandSpace[_trajectoryCommandSpace.Count - 1]);
    }

    private void CommitCommand(Vector3 finalWorldPosition)
    {
        Vector3 finalCommandPosition = ToCommandSpace(finalWorldPosition);

        if (conditionManager.IsContinuous)
        {
            if (_trajectoryCommandSpace.Count == 0)
            {
                _trajectoryWorld.Add(finalWorldPosition);
                _trajectoryCommandSpace.Add(finalCommandPosition);
                UpdateTrajectoryLine();
            }

            EmitContinuousCommit(finalCommandPosition, _trajectoryCommandSpace);
        }
        else
        {
            EmitDiscreteFinal(finalCommandPosition);
        }

        SetTargetMarker(finalWorldPosition);
        _runtimeMode = RuntimeCommandMode.Idle;
    }

    private void CancelCommand()
    {
        _runtimeMode = RuntimeCommandMode.Idle;
        _trajectoryWorld.Clear();
        _trajectoryCommandSpace.Clear();

        if (trajectoryLine != null)
            trajectoryLine.positionCount = 0;
    }

    private Vector3 ToCommandSpace(Vector3 worldPosition)
    {
        if (commandSpace == null)
            return worldPosition;

        return commandSpace.InverseTransformPoint(worldPosition);
    }

    private void SetTargetMarker(Vector3 worldPosition)
    {
        if (targetMarker != null)
            targetMarker.position = worldPosition;
    }

    private void UpdateTrajectoryLine()
    {
        if (trajectoryLine == null)
            return;

        trajectoryLine.useWorldSpace = true;
        trajectoryLine.positionCount = _trajectoryWorld.Count;
        trajectoryLine.SetPositions(_trajectoryWorld.ToArray());
    }

    private void EmitContinuousSample(Vector3 commandSpacePosition)
    {
        // 여기에 너희 기존 continuous trajectory 전달 코드를 연결하면 됨.
        // 예: robotController.UpdateTarget(commandSpacePosition);
    }

    private void EmitContinuousCommit(Vector3 finalCommandPosition, List<Vector3> trajectoryCommandSpace)
    {
        Debug.Log(
            "[WIM COMMAND - CONTINUOUS] " +
            conditionManager.condition +
            " | samples=" + trajectoryCommandSpace.Count +
            " | finalLocal=" + finalCommandPosition.ToString("F3")
        );

        // 여기에 trajectory 전체 전달 코드를 연결하면 됨.
        // 예: robotController.CommitTrajectory(trajectoryCommandSpace);
    }

    private void EmitDiscreteFinal(Vector3 finalCommandPosition)
    {
        Debug.Log(
            "[WIM COMMAND - DISCRETE] " +
            conditionManager.condition +
            " | finalLocal=" + finalCommandPosition.ToString("F3")
        );

        // 여기에 최종 위치만 전달하는 코드를 연결하면 됨.
        // 예: robotController.CommitFinalTarget(finalCommandPosition);
    }
}