using UnityEngine;

public class FollowUiAnchor : MonoBehaviour
{
    [SerializeField] private RectTransform uiAnchor;
    [SerializeField] private Camera worldCamera;

    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Start()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        Apply();
    }

    private void Apply()
    {
        if (uiAnchor == null || worldCamera == null)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        Canvas.ForceUpdateCanvases();

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, uiAnchor.position);
        float depth = transform.position.z - worldCamera.transform.position.z;
        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        worldPoint.z = transform.position.z;
        transform.position = worldPoint;
    }
}
