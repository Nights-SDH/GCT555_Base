using Unity.VisualScripting;
using UnityEngine;

public class GestureEffect : MonoBehaviour
{
    private StreamClient handClient;
    private Renderer myRenderer; // <== Variable to control the object's color

    void Start()
    {
        // Get the Renderer component attached to THIS object when the game starts
        myRenderer = GetComponent<Renderer>();
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
                GetComponent<Transform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            else if (gesture == "Custom_MiddleFinger")
            {
                GetComponent<Transform>().localScale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                myRenderer.material.color = Color.white; // Default color is white
                GetComponent<Transform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
        }
    }
}