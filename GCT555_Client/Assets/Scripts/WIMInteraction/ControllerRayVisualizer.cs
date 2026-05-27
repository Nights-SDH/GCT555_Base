using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ControllerRayVisualizer : MonoBehaviour
{
    public Transform rayOrigin;
    public LayerMask targetSurfaceMask = Physics.DefaultRaycastLayers;
    public float maxDistance = 10f;
    public float lineWidth = 0.01f;
    public Color normalColor = Color.white;
    public Color hitColor = Color.cyan;

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
    }

    void Update()
    {
        Transform origin = rayOrigin != null ? rayOrigin : transform;
        Vector3 start = origin.position;
        Vector3 end = start + origin.forward * maxDistance;
        Color color = normalColor;

        if (Physics.Raycast(
                start,
                origin.forward,
                out RaycastHit hit,
                maxDistance,
                targetSurfaceMask,
                QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            color = hitColor;
        }

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}
