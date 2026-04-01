using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    public GameObject Avatar;
    public float animateThreshold = 1.0f;

    // StreamManager 참조 추가
    public StreamManager streamManager;

    private Animator animator;
    private bool isClose;
    private bool isDance;

    // MediaPipe landmark 인덱스 (MixamoRetargeter와 동일)
    const int MP_NOSE    = 0;
    const int MP_L_WRIST = 15;
    const int MP_R_WRIST = 16;

    private StreamClient poseClient;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();

        FindPoseClient();
    }

    void FindPoseClient()
    {
        // StreamManager에서 Pose 타입 클라이언트 찾기
        if (streamManager != null)
        {
            foreach (var client in streamManager.activeClients)
            {
                if (client.clientType == StreamClient.ClientType.Pose)
                {
                    poseClient = client;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (Avatar == null || animator == null)
            return;

        // --- IsClose 기존 로직 ---
        float distance = Vector3.Distance(transform.position, Avatar.transform.position);
        bool wasClose = isClose;
        isClose = distance <= animateThreshold;
        if (isClose != wasClose)
        {
            Debug.Log("SetBool IsClose: " + isClose);
            animator.SetBool("IsClose", isClose);
        }

        // --- IsDance: 양손이 머리보다 위인지 체크 ---
        bool wasDance = isDance;
        isDance = CheckBothHandsAboveHead();
        if (isDance != wasDance)
        {
            Debug.Log("SetBool IsDance: " + isDance);
            animator.SetBool("IsDance", isDance);
        }
    }

    bool CheckBothHandsAboveHead()
    {
        if (poseClient == null) 
        {
            FindPoseClient();
            if (poseClient == null) 
            {
                Debug.LogWarning("Pose client still not found after searching.");
                return false;
            }
        };

        var lms = poseClient.activeLandmarks;
        Debug.Log($"Landmarks count: {(lms != null ? lms.Count : 0)}");
        if (lms == null || lms.Count <= MP_R_WRIST) return false;

        // activeLandmarks[i].worldPosition은 Unity 월드 좌표
        float noseY    = lms[MP_NOSE].worldPosition.y;
        float lWristY  = lms[MP_L_WRIST].worldPosition.y;
        float rWristY  = lms[MP_R_WRIST].worldPosition.y;

        Debug.Log($"Nose Y: {noseY}, Left Wrist Y: {lWristY}, Right Wrist Y: {rWristY}");

        // 양손 모두 코(머리 기준)보다 위에 있을 때
        return (lWristY > noseY) && (rWristY > noseY);
    }
}