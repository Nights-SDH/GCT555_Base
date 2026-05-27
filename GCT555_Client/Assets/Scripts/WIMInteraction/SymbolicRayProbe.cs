using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SymbolicRayProbe : MonoBehaviour
{
    public Transform rayOrigin;
    public LayerMask targetSurfaceMask = Physics.DefaultRaycastLayers;
    public Transform targetMarker;
    public float maxDistance = 10f;
    public float markerLift = 0.01f;
    public float lineWidth = 0.01f;
    public Color missColor = Color.red;
    public Color hitColor = Color.green;
    public bool logHits = false;

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
        Vector3 direction = origin.forward;
        Vector3 end = start + direction * maxDistance;
        Color color = missColor;

        bool hitSurface = Physics.Raycast(
            start,
            direction,
            out RaycastHit hit,
            maxDistance,
            targetSurfaceMask,
            QueryTriggerInteraction.Collide);

        if (hitSurface)
        {
            end = hit.point;
            color = hitColor;

            if (targetMarker != null)
            {
                targetMarker.gameObject.SetActive(true);
                targetMarker.position = hit.point + hit.normal * markerLift;
                targetMarker.rotation = Quaternion.LookRotation(hit.normal);
            }

            if (logHits)
                Debug.Log($"[SymbolicRayProbe] Hit {hit.collider.name} at {hit.point:F3}");
        }
        else
        {
            if (targetMarker != null)
                targetMarker.gameObject.SetActive(false);

            if (logHits)
                Debug.Log($"[SymbolicRayProbe] Miss from {start:F3}, forward {direction:F3}, mask {targetSurfaceMask.value}");
        }

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}
