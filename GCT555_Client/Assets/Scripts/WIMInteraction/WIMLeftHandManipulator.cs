using UnityEngine;

public class WIMLeftHandManipulator : MonoBehaviour
{
    private enum ManipulationMode
    {
        None,
        Translate,
        Scale
    }

    [Header("References")]
    public XRHandPinchReader leftHandReader;
    public Transform wimRoot;
    public Transform wimCenter;

    [Header("Translation")]
    public float translationGain = 1.0f;

    [Header("Scale")]
    [Tooltip("거리 변화 1m당 scale 변화량. 0.1m 당 30% 변화시키려면 3 정도.")]
    public float scaleGain = 3.0f;

    public float minUniformScale = 0.15f;
    public float maxUniformScale = 8.0f;

    [Tooltip("중지+엄지 scale gesture가 검지+엄지 translation보다 우선한다.")]
    public bool scaleGestureHasPriority = true;

    private ManipulationMode _mode = ManipulationMode.None;

    private Vector3 _startPinchWorld;
    private Vector3 _startRootPosition;
    private Vector3 _startRootScale;

    private float _startScaleDistance;
    private Vector3 _lockedScaleCenterWorld;

    private void Reset()
    {
        wimRoot = transform;
    }

    private void Update()
    {
        if (leftHandReader == null || wimRoot == null)
            return;

        leftHandReader.Refresh();

        bool indexPinch = leftHandReader.HasValidData && leftHandReader.IndexPinching;
        bool middlePinch = leftHandReader.HasValidData && leftHandReader.MiddlePinching;

        bool wantsScale = middlePinch;
        bool wantsTranslate = indexPinch && (!scaleGestureHasPriority || !middlePinch);

        switch (_mode)
        {
            case ManipulationMode.None:
                if (wantsScale)
                    BeginScale();
                else if (wantsTranslate)
                    BeginTranslate();
                break;

            case ManipulationMode.Translate:
                if (!wantsTranslate || wantsScale)
                    EndManipulation();
                else
                    UpdateTranslate();
                break;

            case ManipulationMode.Scale:
                if (!wantsScale)
                    EndManipulation();
                else
                    UpdateScale();
                break;
        }
    }

    private void BeginTranslate()
    {
        _mode = ManipulationMode.Translate;
        _startPinchWorld = leftHandReader.IndexPinchWorld;
        _startRootPosition = wimRoot.position;
    }

    private void UpdateTranslate()
    {
        Vector3 currentPinch = leftHandReader.IndexPinchWorld;
        Vector3 delta = currentPinch - _startPinchWorld;

        wimRoot.position = _startRootPosition + delta * translationGain;
    }

    private void BeginScale()
    {
        _mode = ManipulationMode.Scale;

        _startPinchWorld = leftHandReader.MiddlePinchWorld;
        _startRootScale = wimRoot.localScale;

        _lockedScaleCenterWorld = GetCenterWorld();
        _startScaleDistance = Mathf.Max(
            0.001f,
            Vector3.Distance(_startPinchWorld, _lockedScaleCenterWorld)
        );
    }

    private void UpdateScale()
    {
        Vector3 currentPinch = leftHandReader.MiddlePinchWorld;
        float currentDistance = Vector3.Distance(currentPinch, _lockedScaleCenterWorld);

        // WIM 중심 방향으로 밀면 currentDistance 감소 → scale 감소
        // 바깥으로 당기면 currentDistance 증가 → scale 증가
        float deltaDistance = currentDistance - _startScaleDistance;
        float scaleFactor = 1.0f + deltaDistance * scaleGain;

        float startUniform = GetUniformScale(_startRootScale);
        float minFactor = minUniformScale / Mathf.Max(0.001f, startUniform);
        float maxFactor = maxUniformScale / Mathf.Max(0.001f, startUniform);

        scaleFactor = Mathf.Clamp(scaleFactor, minFactor, maxFactor);

        Vector3 newScale = _startRootScale * scaleFactor;
        ApplyScaleKeepingCenter(newScale);
    }

    private void ApplyScaleKeepingCenter(Vector3 newLocalScale)
    {
        wimRoot.localScale = newLocalScale;

        Vector3 centerAfterScale = GetCenterWorld();
        Vector3 correction = _lockedScaleCenterWorld - centerAfterScale;

        wimRoot.position += correction;
    }

    private Vector3 GetCenterWorld()
    {
        return wimCenter != null ? wimCenter.position : wimRoot.position;
    }

    private float GetUniformScale(Vector3 scale)
    {
        return (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3.0f;
    }

    private void EndManipulation()
    {
        _mode = ManipulationMode.None;
    }
}