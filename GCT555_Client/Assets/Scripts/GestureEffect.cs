using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
public class GestureEffect : MonoBehaviour
{
    private StreamClient handClient;
    private Renderer myRenderer; // <== Variable to control the object's color

    [Header("Movement Settings")]
    public float moveRangeX = 10f;
    public float moveRangeY = 10f;
    public float moveRangeZ = 10f;
    public float moveSpeed = 10f;

    [Header("Audio Settings")]
    public AudioClip collisionSound;
    public AudioSource audioSource;

    public AudioClip ceilingSound;
    public AudioSource audioSourceCeiling;

    public AudioClip sideSound;
    public AudioSource audioSourceSide;

    [Header("Gesture Volume Settings")]
    public float volumeGun = 1.0f;
    public float volumeMiddleFinger = 0.5f;
    public float volumeRock = 0.7f;
    public float volumeDefault = 0.3f;

    [Header("TeamProject Settings")]
    public GameObject WIM;
    public GameObject Puppet;

    [Header("Grab Settings")]
    public float grabDistance = 0.3f;       // WIM Grab 인식 거리

    // 핀치 스케일 상태 (Left_Pinch_Second)
    private float pinchStartDist = -1f;     // -1 = 비활성
    private Vector3 wimScaleAtPinchStart;

    // Grab 상태
    private bool isGrabbingWIM = false;
    private Quaternion handGrabRotation;
    private Quaternion wimGrabRotation;
    private bool isPuppetGrabbed = false;
    private Vector3 puppetGrabOffset;

    void Start()
    {
        // Get the Renderer component attached to THIS object when the game starts
        myRenderer = GetComponent<Renderer>();

        // Automatically add or get AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (audioSourceCeiling == null)
            audioSourceCeiling = gameObject.AddComponent<AudioSource>();
        audioSourceCeiling.playOnAwake = false;

        if (audioSourceSide == null)
            audioSourceSide = gameObject.AddComponent<AudioSource>();
        audioSourceSide.playOnAwake = false;

    }

    void Update()
    {
        // 1. Automatically find the active 'Hand' StreamClient in the Scene
        if (handClient == null)
        {
            StreamClient[] clients = FindObjectsOfType<StreamClient>();
            foreach (var c in clients)
            {
                if (c.clientType == StreamClient.ClientType.Hand)
                    handClient = c;
            }
            return; // Wait and try again next frame if not found
        }

        // 2. Read the current gesture string from the Client
        HandleLeftHand(handClient.leftGesture, handClient.leftLandmarks);
        HandleRightHand(handClient.rightGesture, handClient.rightLandmarks);
    }

    void HandleLeftHand(string gesture, List<Landmark> lm)
    {
        Debug.Log($"Left Gesture: {gesture}"); // Log the current left hand gesture
        Debug.Log("Left lm count: " + (lm != null ? lm.Count.ToString() : "null")); // Log landmark count for debugging
        if (gesture == "Left_Pinch_Second")
        {
            isGrabbingWIM = false;
            if (WIM == null || lm == null || lm.Count < 21) return;

            float dist = Vector3.Distance(lm[4].worldPosition, lm[12].worldPosition);

            // 첫 프레임: 시작 거리와 WIM 스케일 기억
            if (pinchStartDist < 0f)
            {
                pinchStartDist = dist;
                wimScaleAtPinchStart = WIM.transform.localScale;
            }
            else
            {
                // 현재 거리 / 시작 거리 비율을 시작 스케일에 곱함
                float scaleFactor = dist / pinchStartDist;
                WIM.transform.localScale = wimScaleAtPinchStart * scaleFactor;
            }
        }
        else if (gesture == "Left_Pinch_Middle")
        {
            pinchStartDist = -1f;
            isGrabbingWIM = false;
            // 일단 비워두기
        }
        else
        {
            pinchStartDist = -1f;  // 핀치 제스처 해제 시 리셋

            // 왼손이 WIM 근처에 오면 Grab → 손 회전에 따라 WIM 회전
            if (WIM == null || lm == null || lm.Count < 21) { isGrabbingWIM = false; return; }

            float distToWIM = Vector3.Distance(lm[0].worldPosition, WIM.transform.position);

            if (!isGrabbingWIM && distToWIM < grabDistance)
            {
                isGrabbingWIM = true;
                handGrabRotation = GetHandRotation(lm);
                wimGrabRotation = WIM.transform.rotation;
            }
            else if (isGrabbingWIM && distToWIM > grabDistance * 1.2f)
            {
                isGrabbingWIM = false;
            }

            if (isGrabbingWIM)
            {
                Quaternion delta = GetHandRotation(lm) * Quaternion.Inverse(handGrabRotation);
                WIM.transform.rotation = delta * wimGrabRotation;
            }
        }
    }

    void HandleRightHand(string gesture, List<Landmark> lm)
    {
        Debug.Log($"Right Gesture: {gesture}"); // Log the current right hand gesture
        Debug.Log("Right lm count: " + (lm != null ? lm.Count.ToString() : "null")); // Log landmark count for debugging
        if (gesture == "Right_Grab")
        {
            if (Puppet == null || lm == null || lm.Count < 1) return;

            Vector3 wristPos = lm[0].worldPosition;
            wristPos.x = -wristPos.x;
            wristPos.z = -wristPos.z;
            if (!isPuppetGrabbed)
            {
                isPuppetGrabbed = true;
                puppetGrabOffset = Puppet.transform.position - wristPos;
            }
            Puppet.transform.position = wristPos + puppetGrabOffset;
        }
        else if (gesture == "Right_Release")
        {
            isPuppetGrabbed = false;
        }
    }

    // Wrist(0), IndexMCP(5), MiddleMCP(9), PinkyMCP(17)으로 손바닥 평면 방향 계산
    private Quaternion GetHandRotation(List<Landmark> lm)
    {
        Vector3 wrist     = lm[0].worldPosition;
        Vector3 indexMCP  = lm[5].worldPosition;
        Vector3 middleMCP = lm[9].worldPosition;
        Vector3 pinkyMCP  = lm[17].worldPosition;

        Vector3 forward = (middleMCP - wrist).normalized;
        Vector3 right   = (indexMCP - pinkyMCP).normalized;
        Vector3 up      = Vector3.Cross(forward, right).normalized;

        if (forward == Vector3.zero || up == Vector3.zero)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward, up);
    }

    void CustomFunction(string gesture)
    {
        if (gesture == "Custom_Gun")
        {
            myRenderer.material.color = Color.red; // Turns red when "Gun"

            List<Landmark> landmarks = handClient.activeLandmarks;
            if (landmarks != null && landmarks.Count > 0)
            {
                float handX = landmarks[0].x;
                float handY = landmarks[0].y;

                float targetX = (handX - 0.5f) * moveRangeX;
                float targetZ = -(handY - 0.5f) * moveRangeZ;

                Vector3 targetPosition = new Vector3(targetX, transform.position.y, targetZ);
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
            }
        }
        else if (gesture == "Custom_MiddleFinger")
        {
            List<Landmark> landmarks = handClient.activeLandmarks;
            if (landmarks != null && landmarks.Count > 0)
            {
                float handY = landmarks[0].y;
                float targetY = -(handY - 0.5f) * moveRangeY + 2f;
                Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
            }
        }
        else if (gesture == "Custom_Rock")
        {
            List<Landmark> landmarks = handClient.activeLandmarks;
            if (landmarks != null && landmarks.Count > 0)
            {
                float handX = landmarks[0].x;
                float targetX = (handX - 0.5f) * moveRangeX;
                Vector3 targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
            }
        }
        else
        {
            myRenderer.material.color = Color.white;
            GetComponent<Transform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        PlaySoundByObject(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        PlaySoundByObject(other.gameObject);
    }

    private float GetGestureVolume()
    {
        if (handClient == null) return volumeDefault;
        return handClient.currentGesture switch
        {
            "Custom_Gun"          => volumeGun,
            "Custom_MiddleFinger" => volumeMiddleFinger,
            "Custom_Rock"         => volumeRock,
            _                     => volumeDefault,
        };
    }

    private void PlaySoundByObject(GameObject obj)
    {
        float volume = GetGestureVolume();
        string name = obj.name;
        if (name.Contains("Ceiling") || obj.CompareTag("Ceiling"))
        {
            if (ceilingSound != null && audioSourceCeiling != null)
                audioSourceCeiling.PlayOneShot(ceilingSound, volume);
        }
        else if (name.Contains("Side") || obj.CompareTag("Side"))
        {
            if (sideSound != null && audioSourceSide != null)
                audioSourceSide.PlayOneShot(sideSound, volume);
        }
        else if (name.Contains("Cube") || obj.CompareTag("Cube"))
        {
            if (collisionSound != null && audioSource != null)
                audioSource.PlayOneShot(collisionSound, volume);
        }
    }
}
