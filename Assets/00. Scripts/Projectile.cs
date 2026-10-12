using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private const int AdditionalUnlockGrade = 5;
    private const int IceAdditionalDirectionCount = 8;
    private const int FireAdditionalWaveCount = 5;
    private const float FireAdditionalWaveInterval = 0.06f;
    private const float FireAdditionalBaseScale = 0.75f;
    private const float FireAdditionalScaleStep = 0.35f;
    private const float FireAdditionalBaseRadius = 0.35f;
    private const float FireAdditionalRadiusStep = 0.45f;
    private const float FireAdditionalSideAngleDegrees = 25f;
    private const float WindPullSpeed = 1f;
    private const float WindPullRadius = 3f;

    [SerializeField] private float range = 5f;
    [SerializeField] private float fireInterval = 1f;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private bool isInstant;
    [SerializeField] private bool isAdditional;
    [SerializeField] private Effector effectPrefab;
    [SerializeField] private bool rotateToDirection = true;
    [SerializeField] private float projectileRotationOffset;
    [SerializeField] private float effectRotationOffset;
    [SerializeField] private bool castShadow = true;
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -0.08f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Vector2 shadowScale = Vector2.one;
    [SerializeField] private float offScreenMargin = 0.05f;

    private Vector3 direction;
    private Vector3 spawnPosition;
    private bool initialized;
    private Effector spawnedEffect;
    private bool finished;
    private Monster target;
    private int reservedAmount;
    private bool reservationConsumed;
    private Projectile additionalProjectilePrefab;
    private bool spawnAdditionalOnHit;
    private Item.ItemCategory sourceCategory;
    private bool freeFlight;
    private bool instantLinger;
    private bool windPull;
    private readonly HashSet<Monster> piercedMonsters = new();
    private readonly List<Collider2D> overlapBuffer = new();

    public float Range => range;
    public float FireInterval => fireInterval;
    public int Damage => damage;
    public bool IsInstant => isInstant;
    public bool IsAdditional => isAdditional;
    public Effector EffectPrefab => effectPrefab;

    public void ApplyCombatStats(float newRange, int newDamage, float newFireInterval)
    {
        range = newRange;
        damage = newDamage;
        fireInterval = newFireInterval;
    }

    public void ApplyDamage(int newDamage)
    {
        damage = newDamage;
    }

    public void EnableWindPull()
    {
        windPull = true;
    }

    public void ConfigureAdditionalOnHit(
        Projectile additionalPrefab,
        int itemGrade,
        Item.ItemCategory category)
    {
        sourceCategory = category;

        if (isAdditional || additionalPrefab == null || itemGrade < AdditionalUnlockGrade)
        {
            additionalProjectilePrefab = null;
            spawnAdditionalOnHit = false;
            return;
        }

        additionalProjectilePrefab = additionalPrefab;
        spawnAdditionalOnHit = true;
    }

    public bool Init(Monster targetMonster)
    {
        reservedAmount = Mathf.Max(0, damage);
        if (targetMonster == null || reservedAmount <= 0) return false;
        if (!targetMonster.TryReserve(this, reservedAmount)) return false;

        target = targetMonster;
        freeFlight = false;
        instantLinger = false;
        spawnPosition = transform.position;
        FaceToward(target.transform.position);
        initialized = true;
        SpawnEffect();

        if (isInstant)
        {
            reservationConsumed = true;
            target.TryApplyReservedHit(this);
            Finish();
        }

        return true;
    }

    public bool InitFreeFlight(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f) return false;

        direction = moveDirection.normalized;
        ApplyFacingFromDirection();
        target = null;
        freeFlight = true;
        instantLinger = false;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        initialized = true;
        SpawnEffect();
        return true;
    }

    public bool InitInstantInPlace()
    {
        target = null;
        freeFlight = false;
        instantLinger = true;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        initialized = true;
        SpawnInstantLingerEffect();
        ApplyOverlapHits();
        StartCoroutine(FinishAfterEffectAnimation());
        return true;
    }

    public void OnTargetLost()
    {
        reservationConsumed = true;
        target = null;
        if (!instantLinger)
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

        if (castShadow)
            SpriteShadow.Attach(spawnedEffect.gameObject, shadowOffset, shadowColor, shadowScale);
    }

    private void SpawnInstantLingerEffect()
    {
        if (effectPrefab == null) return;

        spawnedEffect = Instantiate(effectPrefab, transform.position, transform.rotation, transform);
        spawnedEffect.transform.localScale = effectPrefab.transform.localScale;
        spawnedEffect.transform.localRotation *= Quaternion.Euler(0f, 0f, effectRotationOffset);
        spawnedEffect.PlayOnce(destroyWhenDone: false);

        if (castShadow)
            SpriteShadow.Attach(spawnedEffect.gameObject, shadowOffset, shadowColor, shadowScale);
    }

    private void ApplyOverlapHits()
    {
        Collider2D myCollider = GetComponent<Collider2D>();
        if (myCollider == null) return;

        overlapBuffer.Clear();
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true
        };
        filter.NoFilter();
        Physics2D.OverlapCollider(myCollider, filter, overlapBuffer);

        for (int i = 0; i < overlapBuffer.Count; i++)
        {
            Collider2D other = overlapBuffer[i];
            if (other == null) continue;

            Monster monster = other.GetComponent<Monster>();
            if (monster != null)
                TryPierceHit(monster);
        }
    }

    private IEnumerator FinishAfterEffectAnimation()
    {
        if (spawnedEffect != null)
            yield return spawnedEffect.WaitUntilFinished();
        else
            yield return null;

        Finish();
    }

    private void Update()
    {
        if (!initialized || finished) return;

        if (instantLinger)
            return;

        if (freeFlight)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
            if (IsOffScreen())
                Finish();
            return;
        }

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

    private void LateUpdate()
    {
        if (!initialized || finished || !windPull) return;
        PullFieldMonsters();
    }

    private void PullFieldMonsters()
    {
        Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        Vector3 pullOrigin = transform.position;
        float radiusSqr = WindPullRadius * WindPullRadius;

        for (int i = 0; i < monsters.Length; i++)
        {
            Monster monster = monsters[i];
            if (monster == null || monster.IsDead) continue;

            float distanceSqr = (monster.transform.position - pullOrigin).sqrMagnitude;
            if (distanceSqr > radiusSqr) continue;

            monster.ApplyConstantWindPull(pullOrigin, WindPullSpeed);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || finished) return;

        if (instantLinger)
        {
            Monster lingerHit = other.GetComponent<Monster>();
            if (lingerHit != null)
                TryPierceHit(lingerHit);
            return;
        }

        if (freeFlight)
        {
            Monster pierced = other.GetComponent<Monster>();
            if (pierced != null)
                TryPierceHit(pierced);
            return;
        }

        Monster monster = other.GetComponent<Monster>();
        if (monster == null || monster != target) return;

        Vector3 hitPosition = transform.position;
        TrySpawnAdditionalProjectile(monster, hitPosition);

        if (!reservationConsumed)
        {
            reservationConsumed = true;
            monster.TryApplyReservedHit(this);
        }

        Finish();
    }

    private void TryPierceHit(Monster monster)
    {
        if (monster == null || !monster.CanBeTargeted) return;
        if (!piercedMonsters.Add(monster)) return;

        int amount = Mathf.Max(0, damage);
        if (amount <= 0) return;
        if (!monster.TryReserve(this, amount)) return;

        monster.TryApplyReservedHit(this);
    }

    private void TrySpawnAdditionalProjectile(Monster hitMonster, Vector3 hitPosition)
    {
        if (!spawnAdditionalOnHit || additionalProjectilePrefab == null) return;

        spawnAdditionalOnHit = false;

        if (sourceCategory == Item.ItemCategory.Fire)
        {
            SpawnFireAdditionalProjectiles(hitPosition, direction);
            return;
        }

        if (sourceCategory == Item.ItemCategory.Wind)
        {
            SpawnWindAdditionalProjectile(hitPosition);
            return;
        }

        if (additionalProjectilePrefab.IsInstant)
        {
            SpawnInstantAdditionalProjectile(hitPosition);
            return;
        }

        if (sourceCategory == Item.ItemCategory.Ice)
        {
            SpawnIceAdditionalProjectiles(hitPosition);
            return;
        }

        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        additional.ApplyDamage(damage);

        Monster nextTarget = FindNearestMonster(hitPosition, additional.Range, hitMonster);
        if (nextTarget == null || !additional.Init(nextTarget))
            Destroy(additional.gameObject);
    }

    private void SpawnInstantAdditionalProjectile(Vector3 hitPosition)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        additional.ApplyDamage(damage);
        if (!additional.InitInstantInPlace())
            Destroy(additional.gameObject);
    }

    private void SpawnWindAdditionalProjectile(Vector3 hitPosition)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        additional.ApplyDamage(damage);
        additional.EnableWindPull();
        if (!additional.InitInstantInPlace())
            Destroy(additional.gameObject);
    }

    private void SpawnFireAdditionalProjectiles(Vector3 hitPosition, Vector3 facingDirection)
    {
        Vector3 facing = facingDirection.sqrMagnitude > 0.0001f
            ? facingDirection.normalized
            : Vector3.up;

        GameObject host = new GameObject("FireAdditionalSequence");
        FireAdditionalSequence sequence = host.AddComponent<FireAdditionalSequence>();
        sequence.Play(
            additionalProjectilePrefab,
            damage,
            hitPosition,
            facing,
            FireAdditionalWaveCount,
            FireAdditionalWaveInterval,
            FireAdditionalBaseScale,
            FireAdditionalScaleStep,
            FireAdditionalBaseRadius,
            FireAdditionalRadiusStep,
            FireAdditionalSideAngleDegrees);
    }

    private void SpawnIceAdditionalProjectiles(Vector3 hitPosition)
    {
        float stepDegrees = 360f / IceAdditionalDirectionCount;
        float startAngleDegrees = Random.Range(0f, stepDegrees);

        for (int i = 0; i < IceAdditionalDirectionCount; i++)
        {
            float angleRad = (startAngleDegrees + stepDegrees * i) * Mathf.Deg2Rad;
            Vector3 moveDirection = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

            Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
            additional.ApplyDamage(damage);
            if (!additional.InitFreeFlight(moveDirection))
                Destroy(additional.gameObject);
        }
    }

    private static Monster FindNearestMonster(Vector3 origin, float searchRange, Monster exclude)
    {
        Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        Monster nearest = null;
        float nearestDistance = searchRange;

        for (int i = 0; i < monsters.Length; i++)
        {
            Monster monster = monsters[i];
            if (monster == null || monster == exclude || !monster.CanBeTargeted) continue;

            float distance = Vector2.Distance(origin, monster.transform.position);
            if (distance > nearestDistance) continue;

            nearestDistance = distance;
            nearest = monster;
        }

        return nearest;
    }

    private bool IsOffScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        return viewport.z < 0f
               || viewport.x < -offScreenMargin
               || viewport.x > 1f + offScreenMargin
               || viewport.y < -offScreenMargin
               || viewport.y > 1f + offScreenMargin;
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
        ApplyFacingFromDirection();
    }

    private void ApplyFacingFromDirection()
    {
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
            if (instantLinger)
            {
                Destroy(spawnedEffect.gameObject);
            }
            else if (spawnedEffect.UseAnimator)
            {
                SpriteShadow shadow = spawnedEffect.GetComponent<SpriteShadow>();
                if (shadow != null)
                    Destroy(shadow);

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

internal sealed class FireAdditionalSequence : MonoBehaviour
{
    public void Play(
        Projectile prefab,
        int damage,
        Vector3 position,
        Vector3 facing,
        int waveCount,
        float waveInterval,
        float baseScale,
        float scaleStep,
        float baseRadius,
        float radiusStep,
        float sideAngleDegrees)
    {
        StartCoroutine(Run(
            prefab,
            damage,
            position,
            facing,
            waveCount,
            waveInterval,
            baseScale,
            scaleStep,
            baseRadius,
            radiusStep,
            sideAngleDegrees));
    }

    private IEnumerator Run(
        Projectile prefab,
        int damage,
        Vector3 position,
        Vector3 facing,
        int waveCount,
        float waveInterval,
        float baseScale,
        float scaleStep,
        float baseRadius,
        float radiusStep,
        float sideAngleDegrees)
    {
        if (prefab == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 prefabScale = prefab.transform.localScale;
        float[] directionAngles = { 0f, sideAngleDegrees, -sideAngleDegrees };

        for (int wave = 0; wave < waveCount; wave++)
        {
            float radius = baseRadius + radiusStep * wave;
            float scaleMultiplier = baseScale + scaleStep * wave;

            for (int dir = 0; dir < directionAngles.Length; dir++)
            {
                Vector3 shotDirection = Quaternion.Euler(0f, 0f, directionAngles[dir]) * facing;
                float angle = Mathf.Atan2(shotDirection.y, shotDirection.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                Vector3 spawnPosition = position + shotDirection * radius;

                Projectile additional = Instantiate(prefab, spawnPosition, rotation);
                additional.ApplyDamage(damage);
                additional.transform.localScale = prefabScale * scaleMultiplier;

                if (!additional.InitInstantInPlace())
                    Destroy(additional.gameObject);
            }

            if (wave < waveCount - 1)
                yield return new WaitForSeconds(waveInterval);
        }

        Destroy(gameObject);
    }
}
