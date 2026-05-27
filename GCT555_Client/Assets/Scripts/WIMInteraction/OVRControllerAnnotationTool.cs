using System.Collections.Generic;
using UnityEngine;

public class OVRControllerAnnotationTool : MonoBehaviour
{
    [Header("Ray")]
    public Transform rayOrigin;
    public LayerMask targetSurfaceMask = Physics.DefaultRaycastLayers;
    public float maxDistance = 10f;
    public float surfaceLift = 0.01f;

    [Header("Controller")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public OVRInput.Button placeMarkerButton = OVRInput.Button.One;
    public OVRInput.Button drawButton = OVRInput.Button.PrimaryHandTrigger;
    public OVRInput.Button eraseButton = OVRInput.Button.Two;
    public OVRInput.Button clearAllButton = OVRInput.Button.PrimaryThumbstick;

    [Header("Marker")]
    public GameObject markerPrefab;
    public Transform annotationParent;
    public float markerScale = 0.05f;
    public Color markerColor = Color.magenta;
    public float eraseRadius = 0.12f;

    [Header("Aim Preview")]
    public bool showAimPreview = true;
    public bool requireControllerInHandForPreview = true;
    public LineRenderer aimLine;
    public Transform hoverMarker;
    public float aimLineWidth = 0.006f;
    public float hoverMarkerScale = 0.035f;
    public Color aimLineColor = new Color(0f, 0.85f, 1f, 0.85f);
    public Color hoverMarkerColor = new Color(0f, 1f, 1f, 0.9f);

    [Header("Drawing")]
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.yellow;
    public float minPointDistance = 0.015f;

    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<LineRenderer> strokes = new List<LineRenderer>();
    private LineRenderer currentStroke;
    private readonly List<Vector3> currentStrokePoints = new List<Vector3>();

    void Awake()
    {
        EnsureAimPreviewObjects();
    }

    void Update()
    {
        Transform origin = rayOrigin != null ? rayOrigin : transform;
        bool controllerAvailable = IsControllerAvailable();
        if (!controllerAvailable)
        {
            SetAimPreviewVisible(false);
            EndStroke();
            return;
        }

        bool hasHit = Physics.Raycast(
            origin.position,
            origin.forward,
            out RaycastHit hit,
            maxDistance,
            targetSurfaceMask,
            QueryTriggerInteraction.Collide);

        UpdateAimPreview(origin, hasHit, hit);

        if (OVRInput.GetDown(placeMarkerButton, controller) && hasHit)
            PlaceMarker(hit);

        if (OVRInput.Get(drawButton, controller) && hasHit)
            AddDrawPoint(hit);

        if (OVRInput.GetUp(drawButton, controller))
            EndStroke();

        if (OVRInput.GetDown(eraseButton, controller))
            EraseAtHitOrLast(hasHit, hit);

        if (OVRInput.GetDown(clearAllButton, controller))
            ClearAllAnnotations();
    }

    private bool IsControllerAvailable()
    {
        bool connected = OVRInput.IsControllerConnected(controller);
        bool tracked = OVRInput.GetControllerPositionTracked(controller);

        if (!connected && !tracked)
            return false;

        if (!requireControllerInHandForPreview)
            return true;

        OVRInput.Hand hand;
        if (!TryGetHandForController(controller, out hand))
            return tracked;

        return OVRInput.GetControllerIsInHandState(hand) == OVRInput.ControllerInHandState.ControllerInHand;
    }

    private bool TryGetHandForController(OVRInput.Controller controllerType, out OVRInput.Hand hand)
    {
        if ((controllerType & OVRInput.Controller.RTouch) != 0)
        {
            hand = OVRInput.Hand.HandRight;
            return true;
        }

        if ((controllerType & OVRInput.Controller.LTouch) != 0)
        {
            hand = OVRInput.Hand.HandLeft;
            return true;
        }

        hand = OVRInput.Hand.None;
        return false;
    }

    private void SetAimPreviewVisible(bool visible)
    {
        if (aimLine != null)
            aimLine.enabled = visible;

        if (hoverMarker != null)
            hoverMarker.gameObject.SetActive(visible);
    }

    private void EnsureAimPreviewObjects()
    {
        if (aimLine == null)
        {
            GameObject lineObject = new GameObject("WIM Aim Ray");
            lineObject.transform.SetParent(transform, false);
            aimLine = lineObject.AddComponent<LineRenderer>();
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.material = lineMaterial != null
                ? lineMaterial
                : new Material(Shader.Find("Sprites/Default"));
        }

        aimLine.startWidth = aimLineWidth;
        aimLine.endWidth = aimLineWidth;
        aimLine.startColor = aimLineColor;
        aimLine.endColor = aimLineColor;

        if (hoverMarker == null)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "WIM Hover Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * hoverMarkerScale;

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                Destroy(markerCollider);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = hoverMarkerColor;

            hoverMarker = marker.transform;
        }
    }

    private void UpdateAimPreview(Transform origin, bool hasHit, RaycastHit hit)
    {
        if (!showAimPreview)
        {
            SetAimPreviewVisible(false);
            return;
        }

        EnsureAimPreviewObjects();

        Vector3 endPoint = hasHit
            ? hit.point + hit.normal * surfaceLift
            : origin.position + origin.forward * maxDistance;

        aimLine.enabled = true;
        aimLine.SetPosition(0, origin.position);
        aimLine.SetPosition(1, endPoint);

        hoverMarker.gameObject.SetActive(hasHit);
        if (hasHit)
        {
            hoverMarker.position = endPoint;
            hoverMarker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            hoverMarker.localScale = Vector3.one * hoverMarkerScale;
        }
    }

    private void PlaceMarker(RaycastHit hit)
    {
        Vector3 position = hit.point + hit.normal * surfaceLift;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

        GameObject marker = markerPrefab != null
            ? Instantiate(markerPrefab, position, rotation, annotationParent)
            : CreateDefaultMarker(position, rotation);

        marker.name = "WIM Marker";
        markers.Add(marker);
    }

    private GameObject CreateDefaultMarker(Vector3 position, Quaternion rotation)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.SetParent(annotationParent, true);
        marker.transform.SetPositionAndRotation(position, rotation);
        marker.transform.localScale = Vector3.one * markerScale;

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
            Destroy(markerCollider);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = markerColor;

        return marker;
    }

    private void AddDrawPoint(RaycastHit hit)
    {
        Vector3 point = hit.point + hit.normal * surfaceLift;

        if (currentStroke == null)
            BeginStroke();

        if (currentStrokePoints.Count > 0 &&
            Vector3.Distance(currentStrokePoints[currentStrokePoints.Count - 1], point) < minPointDistance)
            return;

        currentStrokePoints.Add(point);
        currentStroke.positionCount = currentStrokePoints.Count;
        currentStroke.SetPositions(currentStrokePoints.ToArray());
    }

    private void BeginStroke()
    {
        GameObject strokeObject = new GameObject("WIM Stroke");
        strokeObject.transform.SetParent(annotationParent, true);

        currentStroke = strokeObject.AddComponent<LineRenderer>();
        currentStroke.useWorldSpace = true;
        currentStroke.positionCount = 0;
        currentStroke.startWidth = lineWidth;
        currentStroke.endWidth = lineWidth;
        currentStroke.startColor = lineColor;
        currentStroke.endColor = lineColor;
        currentStroke.material = lineMaterial != null
            ? lineMaterial
            : new Material(Shader.Find("Sprites/Default"));

        currentStrokePoints.Clear();
        strokes.Add(currentStroke);
    }

    private void EndStroke()
    {
        if (currentStroke != null && currentStroke.positionCount < 2)
        {
            strokes.Remove(currentStroke);
            Destroy(currentStroke.gameObject);
        }

        currentStroke = null;
        currentStrokePoints.Clear();
    }

    private void EraseAtHitOrLast(bool hasHit, RaycastHit hit)
    {
        if (hasHit && TryEraseNearestMarker(hit.point))
            return;

        if (hasHit && TryEraseNearestStroke(hit.point))
            return;

        if (markers.Count > 0)
        {
            GameObject marker = markers[markers.Count - 1];
            markers.RemoveAt(markers.Count - 1);
            Destroy(marker);
            return;
        }

        if (strokes.Count > 0)
        {
            LineRenderer stroke = strokes[strokes.Count - 1];
            strokes.RemoveAt(strokes.Count - 1);
            Destroy(stroke.gameObject);
        }
    }

    private bool TryEraseNearestMarker(Vector3 hitPoint)
    {
        int bestIndex = -1;
        float bestDistance = eraseRadius;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] == null)
                continue;

            float distance = Vector3.Distance(hitPoint, markers[i].transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        GameObject marker = markers[bestIndex];
        markers.RemoveAt(bestIndex);
        Destroy(marker);
        return true;
    }

    private bool TryEraseNearestStroke(Vector3 hitPoint)
    {
        int bestIndex = -1;
        float bestDistance = eraseRadius;

        for (int i = 0; i < strokes.Count; i++)
        {
            LineRenderer stroke = strokes[i];
            if (stroke == null)
                continue;

            for (int p = 0; p < stroke.positionCount; p++)
            {
                float distance = Vector3.Distance(hitPoint, stroke.GetPosition(p));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }
        }

        if (bestIndex < 0)
            return false;

        LineRenderer nearestStroke = strokes[bestIndex];
        strokes.RemoveAt(bestIndex);
        Destroy(nearestStroke.gameObject);
        return true;
    }

    private void ClearAllAnnotations()
    {
        foreach (GameObject marker in markers)
        {
            if (marker != null)
                Destroy(marker);
        }

        foreach (LineRenderer stroke in strokes)
        {
            if (stroke != null)
                Destroy(stroke.gameObject);
        }

        markers.Clear();
        strokes.Clear();
        currentStroke = null;
        currentStrokePoints.Clear();
    }
}
