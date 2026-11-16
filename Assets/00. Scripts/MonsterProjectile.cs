using UnityEngine;

/// <summary>
/// 몬스터(원거리 공격형)가 방벽을 향해 쏘는 투사체.
/// 방벽(Barrier)에 닿으면 데미지를 주고 사라진다. 플레이어의 Projectile과는 별개다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 4f;
    [Tooltip("이 시간(초) 안에 아무것도 못 맞히면 사라진다.")]
    [SerializeField] private float maxLifetime = 6f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("재생할 프레임. 비어 있으면 SpriteRenderer의 스프라이트를 그대로 쓴다.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 12f;
    [Tooltip("스프라이트가 기본적으로 오른쪽을 보고 있으면 체크. 날아가는 방향으로 회전시키는 기준이 된다.")]
    [SerializeField] private bool spriteFacesRight = true;
    [Tooltip("방벽에 맞았을 때 생성할 이펙트(선택).")]
    [SerializeField] private GameObject impactEffectPrefab;

    private int damage;
    private Vector3 direction = Vector3.down;
    private float age;
    private bool finished;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // 방벽은 트리거 콜라이더이므로 투사체 콜라이더도 트리거여야 OnTriggerEnter2D가 호출된다.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = true;
    }

    /// <summary>방벽에서 가장 가까운 지점을 향해 발사한다.</summary>
    public void Launch(Barrier barrier, int hitDamage)
    {
        damage = Mathf.Max(0, hitDamage);

        Vector3 origin = transform.position;
        Collider2D barrierCollider = barrier != null ? barrier.GetComponent<Collider2D>() : null;
        if (barrierCollider != null)
        {
            Vector2 target = barrierCollider.ClosestPoint(origin);
            Vector3 toTarget = (Vector3)target - origin;
            toTarget.z = 0f;
            direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.down;
        }
        else
        {
            direction = Vector3.down;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (!spriteFacesRight)
            angle += 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        ApplyFrame(0f);
    }

    private void Update()
    {
        if (finished) return;

        age += Time.deltaTime;
        if (age >= maxLifetime)
        {
            Destroy(gameObject);
            finished = true;
            return;
        }

        transform.position += direction * speed * Time.deltaTime;
        ApplyFrame(age);
    }

    private void ApplyFrame(float time)
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;

        int index = Mathf.FloorToInt(time * framesPerSecond) % frames.Length;
        spriteRenderer.sprite = frames[index];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (finished) return;

        Barrier barrier = other.GetComponent<Barrier>();
        if (barrier == null) return;

        finished = true;
        barrier.ApplyContactHit(damage);

        if (impactEffectPrefab != null)
            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
