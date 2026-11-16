using UnityEngine;

/// <summary>
/// 스프라이트 프레임을 한 번 재생하고 스스로 사라지는 간단한 이펙트(폭발 등).
/// Animator 없이 프레임 배열만으로 동작한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 24f;

    private SpriteRenderer spriteRenderer;
    private float age;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        int index = Mathf.FloorToInt(age * framesPerSecond);
        if (index >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer.sprite = frames[index];
    }
}
