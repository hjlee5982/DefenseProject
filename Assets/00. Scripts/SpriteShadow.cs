using UnityEngine;

[DisallowMultipleComponent]
public class SpriteShadow : MonoBehaviour
{
    [SerializeField] private SpriteRenderer source;
    [SerializeField] private Vector2 offset = new Vector2(0.1f, -0.15f);
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Vector2 scale = Vector2.one;
    [SerializeField] private int sortingOrderOffset = -1;

    private SpriteRenderer shadowRenderer;

    public static SpriteShadow Attach(GameObject target, Vector2 offset, Color color, Vector2 scale)
    {
        if (target == null) return null;

        SpriteShadow shadow = target.GetComponent<SpriteShadow>();
        if (shadow == null)
            shadow = target.AddComponent<SpriteShadow>();

        shadow.offset = offset;
        shadow.color = color;
        shadow.scale = scale;
        shadow.EnsureSetup();
        shadow.Sync();
        return shadow;
    }

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnDestroy()
    {
        if (shadowRenderer != null)
            Destroy(shadowRenderer.gameObject);
    }

    private void LateUpdate()
    {
        Sync();
    }

    private void EnsureSetup()
    {
        if (source == null)
            source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        if (shadowRenderer == null)
        {
            Transform existing = transform.Find("Shadow");
            if (existing != null)
                shadowRenderer = existing.GetComponent<SpriteRenderer>();
        }

        if (shadowRenderer != null) return;

        GameObject shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(transform, false);
        shadowObject.transform.SetAsFirstSibling();
        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
    }

    private void Sync()
    {
        if (source == null || shadowRenderer == null) return;

        shadowRenderer.sprite = source.sprite;
        shadowRenderer.flipX = source.flipX;
        shadowRenderer.flipY = source.flipY;
        shadowRenderer.sharedMaterial = source.sharedMaterial;
        shadowRenderer.color = color;
        shadowRenderer.sortingLayerID = source.sortingLayerID;
        shadowRenderer.sortingOrder = source.sortingOrder + sortingOrderOffset;
        shadowRenderer.enabled = source.enabled && source.sprite != null;

        Transform shadowTransform = shadowRenderer.transform;
        shadowTransform.localPosition = new Vector3(offset.x, offset.y, 0f);
        shadowTransform.localRotation = Quaternion.identity;
        shadowTransform.localScale = new Vector3(scale.x, scale.y, 1f);
    }
}
