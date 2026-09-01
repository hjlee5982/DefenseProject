using UnityEngine;

public class AttackGuideline : MonoBehaviour
{
    public static void Show(Vector3 from, Vector3 to, float width, Color color, float duration)
    {
        GameObject guidelineObject = new GameObject("AttackGuideline");
        LineRenderer line = guidelineObject.AddComponent<LineRenderer>();

        from.z = 0f;
        to.z = 0f;

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.TransformZ;
        line.sortingOrder = 50;
        line.material = CreateLineMaterial(color);

        AttackGuideline lifetime = guidelineObject.AddComponent<AttackGuideline>();
        lifetime.remainingDuration = Mathf.Max(0.01f, duration);
    }

    private float remainingDuration;

    private void Update()
    {
        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
            Destroy(gameObject);
    }

    private static Material CreateLineMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }
}
