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
        string gesture = handClient.currentGesture;

        // 3. Change THIS object's color based on the gesture! 🎨
        if (myRenderer != null)
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