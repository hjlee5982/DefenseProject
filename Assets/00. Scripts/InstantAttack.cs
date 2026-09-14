using UnityEngine;

public class InstantAttack : MonoBehaviour
{
    [SerializeField] private float range = 5f;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private int damage = 1;
    [SerializeField] private Effector effectPrefab;
    [SerializeField] private bool showGuideline = true;
    [SerializeField] private Effector guidelinePrefab;
    [SerializeField] private float guidelineRotationOffset;

    private bool reservationConsumed;
    private Monster target;

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

    public bool Init(Monster targetMonster, Vector3 origin)
    {
        target = targetMonster;
        int amount = Mathf.Max(0, damage);
        if (target == null || amount <= 0) return false;
        if (!target.TryReserve(this, amount)) return false;

        Vector3 hitPosition = target.transform.position;
        if (showGuideline)
            AttackGuideline.Show(guidelinePrefab, origin, hitPosition, guidelineRotationOffset);

        SpawnEffect(hitPosition);

        if (target.TryApplyReservedHit(this))
            reservationConsumed = true;

        Destroy(gameObject);
        return true;
    }

    private void OnDestroy()
    {
        if (reservationConsumed) return;
        if (target != null)
            target.ReleaseReservation(this);
    }

    private void SpawnEffect(Vector3 position)
    {
        if (effectPrefab == null) return;

        Effector effect = Instantiate(effectPrefab, position, Quaternion.identity);
        if (effect.UseAnimator)
            effect.PlayEnd();
        else
            effect.PlayOnce();
    }
}
