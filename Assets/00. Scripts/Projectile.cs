using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float range = 5f;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private Effector effectPrefab;
    [SerializeField] private bool rotateToDirection = true;
    [SerializeField] private float projectileRotationOffset;
    [SerializeField] private float effectRotationOffset;

    private Vector3 direction;
    private Vector3 spawnPosition;
    private bool initialized;
    private Effector spawnedEffect;
    private bool finished;
    private Monster target;
    private int reservedAmount;
    private bool reservationConsumed;

    public float Range => range;
    public float FireInterval => fireInterval;
    public int Damage => damage;
    public Effector EffectPrefab => effectPrefab;

    public void ApplyCombatStats(float newRange, int newDamage, float newFireInterval)
    {
        range = newRange;
        damage = newDamage;
        fireInterval = newFireInterval;
    }

    public bool Init(Monster targetMonster)
    {
        reservedAmount = Mathf.Max(0, damage);
        if (targetMonster == null || reservedAmount <= 0) return false;
        if (!targetMonster.TryReserve(this, reservedAmount)) return false;

        target = targetMonster;
        spawnPosition = transform.position;
        FaceToward(target.transform.position);
        initialized = true;
        SpawnEffect();
        return true;
    }

    public void OnTargetLost()
    {
        reservationConsumed = true;
        target = null;
        Finish();
    }

    private void SpawnEffect()
    {
        if (effectPrefab == null) return;

        spawnedEffect = Instantiate(effectPrefab, transform.position, transform.rotation, transform);
        spawnedEffect.transform.localScale = effectPrefab.transform.localScale;
        spawnedEffect.transform.localRotation *= Quaternion.Euler(0f, 0f, effectRotationOffset);
        if (spawnedEffect.UseAnimator)
            spawnedEffect.PlayMoveLoop();
        else
            spawnedEffect.Play();
    }

    private void Update()
    {
        if (!initialized || finished) return;

        if (target == null)
        {
            Finish();
            return;
        }

        FaceToward(target.transform.position);
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(spawnPosition, transform.position) > range)
            Finish();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || finished) return;

        Monster monster = other.GetComponent<Monster>();
        if (monster == null || monster != target) return;

        if (!reservationConsumed)
        {
            reservationConsumed = true;
            monster.TryApplyReservedHit(this);
        }

        Finish();
    }

    private void OnDestroy()
    {
        ReleaseReservation();
    }

    private void FaceToward(Vector3 worldPosition)
    {
        Vector3 toTarget = worldPosition - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        direction = toTarget.normalized;
        if (!rotateToDirection) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + projectileRotationOffset);
    }

    private void ReleaseReservation()
    {
        if (reservationConsumed) return;
        reservationConsumed = true;

        if (target != null)
            target.ReleaseReservation(this);
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        ReleaseReservation();

        if (spawnedEffect != null)
        {
            if (spawnedEffect.UseAnimator)
            {
                spawnedEffect.transform.SetParent(null);
                spawnedEffect.PlayEnd();
            }
            else
            {
                Destroy(spawnedEffect.gameObject);
            }
        }

        Destroy(gameObject);
    }
}
