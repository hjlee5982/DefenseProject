using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(RectTransform))]
public class WorldSpaceUiCanvas : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float worldScale = 0.01f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector2 canvasSize = new Vector2(480f, 48f);
    [SerializeField] private int sortingOrder = 20;
    [SerializeField] private RectTransform content;

    private Canvas canvas;
    private RectTransform rectTransform;
    private float lastParentScale = -1f;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = (RectTransform)transform;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (content == null && transform.childCount > 0)
            content = transform.GetChild(0) as RectTransform;

        ApplyLayout();
    }

    private void LateUpdate()
    {
        float parentScale = GetParentScale();
        if (!Mathf.Approximately(parentScale, lastParentScale))
            ApplyScale(parentScale);
    }

    private void ApplyLayout()
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = canvasSize;
        rectTransform.anchoredPosition3D = localOffset;
        rectTransform.localRotation = Quaternion.identity;
        ApplyScale(GetParentScale());

        if (content != null)
        {
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.localPosition = Vector3.zero;
            content.localScale = Vector3.one;
            content.localRotation = Quaternion.identity;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 100f;
            scaler.referencePixelsPerUnit = 100f;
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
    }

    private void ApplyScale(float parentScale)
    {
        lastParentScale = parentScale;
        float local = worldScale / Mathf.Max(0.0001f, parentScale);
        rectTransform.localScale = new Vector3(local, local, local);
    }

    private float GetParentScale()
    {
        Transform parent = transform.parent;
        if (parent == null) return 1f;
        return Mathf.Abs(parent.lossyScale.x);
    }
}
