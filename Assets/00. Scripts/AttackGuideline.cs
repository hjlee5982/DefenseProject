using UnityEngine;

public static class AttackGuideline
{
    public static void Show(Effector prefab, Vector3 from, Vector3 to, float rotationOffset = 0f)
    {
        if (prefab == null) return;

        from.z = 0f;
        to.z = 0f;

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.0001f) return;

        Effector effect = Object.Instantiate(prefab);
        effect.PlayOnce();
        FitBetween(effect, from, to, rotationOffset);
    }

    private static void FitBetween(Effector effect, Vector3 from, Vector3 to, float rotationOffset)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        Vector3 mid = (from + to) * 0.5f;
        mid.z = 0f;

        effect.transform.position = mid;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        effect.transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        float spriteWidth = GetSpriteWidth(effect);
        Vector3 scale = effect.transform.localScale;
        scale.x = distance / spriteWidth;
        effect.transform.localScale = scale;
    }

    private static float GetSpriteWidth(Effector effect)
    {
        SpriteRenderer spriteRenderer = effect.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return Mathf.Max(0.0001f, spriteRenderer.sprite.bounds.size.x);

        return 1f;
    }
}
