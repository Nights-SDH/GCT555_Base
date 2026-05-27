using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class WIMControllerAnnotationTool : MonoBehaviour
{
    public enum ControllerButton
    {
        PrimaryButton,
        SecondaryButton,
        GripButton,
        TriggerButton,
        ThumbstickClick
    }

    [Header("Ray")]
    public Transform rayOrigin;
    public XRNode controllerNode = XRNode.RightHand;
    public LayerMask targetSurfaceMask = Physics.DefaultRaycastLayers;
    public float maxDistance = 10f;
    public float surfaceLift = 0.01f;

    [Header("Buttons")]
    [Tooltip("Quest right controller default: A")]
    public ControllerButton placeMarkerButton = ControllerButton.PrimaryButton;

    [Tooltip("Quest right controller default: side grip, hold to draw")]
    public ControllerButton drawButton = ControllerButton.GripButton;

    [Tooltip("Quest right controller default: B")]
    public ControllerButton eraseButton = ControllerButton.SecondaryButton;

    [Tooltip("Quest right controller default: thumbstick click")]
    public ControllerButton clearAllButton = ControllerButton.ThumbstickClick;

    [Header("Marker")]
    public GameObject markerPrefab;
    public Transform annotationParent;
    public float markerScale = 0.05f;
    public Color markerColor = Color.magenta;
    public float eraseRadius = 0.12f;

    [Header("Drawing")]
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public Color lineColor = Color.yellow;
    public float minPointDistance = 0.015f;

    [Header("Debug Ray")]
    public LineRenderer debugRay;
    public Color missRayColor = Color.red;
    public Color hitRayColor = Color.green;

    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<LineRenderer> strokes = new List<LineRenderer>();
    private readonly Dictionary<ControllerButton, bool> previousButtonStates = new Dictionary<ControllerButton, bool>();

    private LineRenderer currentStroke;
    private readonly List<Vector3> currentStrokePoints = new List<Vector3>();

    void Update()
    {
        Transform origin = rayOrigin != null ? rayOrigin : transform;
        bool hasHit = Physics.Raycast(
            origin.position,
            origin.forward,
            out RaycastHit hit,
            maxDistance,
            targetSurfaceMask,
            QueryTriggerInteraction.Collide);

        UpdateDebugRay(origin, hasHit, hit);

        if (GetButtonDown(placeMarkerButton) && hasHit)
            PlaceMarker(hit);

        if (GetButton(drawButton) && hasHit)
            AddDrawPoint(hit);

        if (GetButtonUp(drawButton))
            EndStroke();

        if (GetButtonDown(eraseButton))
            EraseAtHitOrLast(hasHit, hit);

        if (GetButtonDown(clearAllButton))
            ClearAllAnnotations();
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
        marker.transform.position = position;
        marker.transform.rotation = rotation;
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

    private void UpdateDebugRay(Transform origin, bool hasHit, RaycastHit hit)
    {
        if (debugRay == null)
            return;

        Vector3 start = origin.position;
        Vector3 end = hasHit ? hit.point : start + origin.forward * maxDistance;
        Color color = hasHit ? hitRayColor : missRayColor;

        debugRay.useWorldSpace = true;
        debugRay.positionCount = 2;
        debugRay.startWidth = lineWidth;
        debugRay.endWidth = lineWidth;
        debugRay.startColor = color;
        debugRay.endColor = color;
        debugRay.SetPosition(0, start);
        debugRay.SetPosition(1, end);
    }

    private bool GetButtonDown(ControllerButton button)
    {
        bool current = GetButton(button);
        bool previous = previousButtonStates.TryGetValue(button, out bool value) && value;
        previousButtonStates[button] = current;
        return current && !previous;
    }

    private bool GetButtonUp(ControllerButton button)
    {
        bool current = GetButton(button);
        bool previous = previousButtonStates.TryGetValue(button, out bool value) && value;
        previousButtonStates[button] = current;
        return !current && previous;
    }

    private bool GetButton(ControllerButton button)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid)
            return false;

        switch (button)
        {
            case ControllerButton.PrimaryButton:
                return TryGetButton(device, CommonUsages.primaryButton);
            case ControllerButton.SecondaryButton:
                return TryGetButton(device, CommonUsages.secondaryButton);
            case ControllerButton.GripButton:
                return TryGetButton(device, CommonUsages.gripButton) ||
                       TryGetAxisPressed(device, CommonUsages.grip);
            case ControllerButton.TriggerButton:
                return TryGetButton(device, CommonUsages.triggerButton) ||
                       TryGetAxisPressed(device, CommonUsages.trigger);
            case ControllerButton.ThumbstickClick:
                return TryGetButton(device, CommonUsages.primary2DAxisClick);
            default:
                return false;
        }
    }

    private bool TryGetButton(InputDevice device, InputFeatureUsage<bool> feature)
    {
        return device.TryGetFeatureValue(feature, out bool value) && value;
    }

    private bool TryGetAxisPressed(InputDevice device, InputFeatureUsage<float> feature)
    {
        return device.TryGetFeatureValue(feature, out float value) && value >= 0.5f;
    }
}
