using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private const int AdditionalUnlockGrade = 4;
    private const int AdditionalEnhancedGrade = 5;
    private const int IceAdditionalDirectionCount = 8;
    private const int IceAdditionalEnhancedDirectionCount = 16;
    private const int FireAdditionalWaveCount = 5;
    private const int FireAdditionalEnhancedWaveCount = 7;
    private const int FireAdditionalLineCount = 3;
    private const int FireAdditionalEnhancedLineCount = 5;
    private const float FireAdditionalWaveInterval = 0.06f;
    private const float FireAdditionalBaseScale = 0.75f;
    private const float FireAdditionalScaleStep = 0.35f;
    private const float FireAdditionalBaseRadius = 0.35f;
    private const float FireAdditionalRadiusStep = 0.45f;
    private const float FireAdditionalSideAngleDegrees = 25f;
    private const float WindPullSpeed = 1f;
    private const float WindPullRadius = 3f;
    private const float WaterKnockbackDistance = 0.625f;
    private const float WaterKnockbackDuration = 0.15f;
    private const float RockRootDuration = 0.75f;
    private const int GrimoireMultiHitCount = 15;
    private const float GrimoireMultiHitStep = 0.5f;
    private const float GrimoireHitSpeedReduce = 5.0f;
    private const float GrimoireMonsterSlowMultiplier = 0.5f;
    private const float GrimoireMonsterSlowDuration = 0.5f;
    private const float CriticalDamageMultiplier = 2f;

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
    private int sourceItemGrade;
    private float activeWaterKnockbackDistance = WaterKnockbackDistance;
    private float activeRockRootDuration = RockRootDuration;
    private float activeWindPullSpeed = WindPullSpeed;
    private bool freeFlight;
    private bool instantLinger;
    private bool windPull;
    private bool rockRootOnHit;
    private bool grimoireMultiHit;
    private int multiHitRemaining;
    private bool multiHitInProgress;
    private Monster grimoireFocusMonster;
    private float baseMoveSpeed;
    private float currentMoveSpeed;
    private float fullFlightSpeed;
    private float grimoireDistanceSinceHit;
    private float criticalChance;
    private bool lastHitWasCritical;
    private bool singleTargetHit;
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
        baseMoveSpeed = moveSpeed;
        fullFlightSpeed = moveSpeed;
        currentMoveSpeed = moveSpeed;
    }

    public void ApplyDamage(int newDamage)
    {
        damage = newDamage;
    }

    public void ApplyCriticalChance(float chance)
    {
        criticalChance = Mathf.Clamp01(chance);
    }

    private int RollHitDamage()
    {
        lastHitWasCritical = false;
        int amount = Mathf.Max(0, damage);
        if (amount <= 0 || criticalChance <= 0f) return amount;
        if (Random.value >= criticalChance) return amount;

        lastHitWasCritical = true;
        return Mathf.Max(1, Mathf.RoundToInt(amount * CriticalDamageMultiplier));
    }

    public void EnableWindPull(float pullSpeed = -1f)
    {
        windPull = true;
        activeWindPullSpeed = pullSpeed > 0f ? pullSpeed : WindPullSpeed;
    }

    private bool HasEnhancedAdditional => sourceItemGrade >= AdditionalEnhancedGrade;

    public void ConfigureAdditionalOnHit(
        Projectile additionalPrefab,
        int itemGrade,
        Item.ItemCategory category)
    {
        sourceCategory = category;
        sourceItemGrade = itemGrade;
        grimoireMultiHit = false;
        multiHitRemaining = 0;

        if (category == Item.ItemCategory.Grimoire && itemGrade >= AdditionalUnlockGrade && !isAdditional)
        {
            additionalProjectilePrefab = null;
            spawnAdditionalOnHit = false;
            grimoireMultiHit = true;
            multiHitRemaining = HasEnhancedAdditional
                ? GrimoireMultiHitCount * 2
                : GrimoireMultiHitCount;
            return;
        }

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
        reservedAmount = RollHitDamage();
        if (targetMonster == null || reservedAmount <= 0) return false;
        if (!targetMonster.TryReserve(this, reservedAmount)) return false;

        target = targetMonster;
        freeFlight = false;
        instantLinger = false;
        singleTargetHit = false;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        EnsureFlightSpeeds();
        currentMoveSpeed = fullFlightSpeed;
        FaceToward(target.transform.position);
        initialized = true;
        SpawnEffect();

        if (isInstant)
        {
            reservationConsumed = true;
            target.TryApplyReservedHit(this, lastHitWasCritical);
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
        singleTargetHit = false;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        EnsureFlightSpeeds();
        currentMoveSpeed = fullFlightSpeed;
        initialized = true;
        SpawnEffect();
        return true;
    }

    public bool InitInstantInPlace()
    {
        target = null;
        freeFlight = false;
        instantLinger = true;
        singleTargetHit = false;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        initialized = true;
        SpawnInstantLingerEffect();
        ApplyOverlapHits();
        StartCoroutine(FinishAfterEffectAnimation());
        return true;
    }

    public bool InitWaterAdditional(Monster hitMonster, float knockbackDistance = -1f)
    {
        if (hitMonster == null || hitMonster.IsDead) return false;

        activeWaterKnockbackDistance = knockbackDistance > 0f ? knockbackDistance : WaterKnockbackDistance;
        target = hitMonster;
        freeFlight = false;
        instantLinger = true;
        singleTargetHit = true;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        piercedMonsters.Add(hitMonster);
        initialized = true;

        int amount = RollHitDamage();
        if (amount > 0 && hitMonster.CanBeTargeted && hitMonster.TryReserve(this, amount))
            hitMonster.TryApplyReservedHit(this, lastHitWasCritical);

        if (!hitMonster.IsDead)
            hitMonster.ApplyKnockback(Vector3.up, activeWaterKnockbackDistance, WaterKnockbackDuration);

        SpawnInstantLingerEffect();
        StartCoroutine(FinishAfterEffectAnimation());
        return true;
    }

    public bool InitRockAdditional(float rootDuration = -1f)
    {
        activeRockRootDuration = rootDuration > 0f ? rootDuration : RockRootDuration;
        target = null;
        freeFlight = false;
        instantLinger = true;
        rockRootOnHit = true;
        singleTargetHit = false;
        reservationConsumed = true;
        spawnPosition = transform.position;
        piercedMonsters.Clear();
        initialized = true;

        ApplyRockColliderHits();
        SpawnInstantLingerEffect();
        StartCoroutine(FinishAfterEffectAnimation());
        return true;
    }

    private void ApplyRockColliderHits()
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
                TryRockHit(monster);
        }
    }

    private void TryRockHit(Monster monster)
    {
        if (monster == null || monster.IsDead || !monster.CanBeTargeted) return;
        if (!piercedMonsters.Add(monster)) return;

        int amount = RollHitDamage();
        if (amount > 0 && monster.TryReserve(this, amount))
            monster.TryApplyReservedHit(this, lastHitWasCritical);

        if (!monster.IsDead)
            monster.ApplyTemporaryStop(activeRockRootDuration);
    }

    public void OnTargetLost()
    {
        reservationConsumed = true;
        target = null;
        if (multiHitInProgress || instantLinger) return;
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

        if (grimoireMultiHit)
        {
            UpdateGrimoireFlight();
            return;
        }

        if (freeFlight)
        {
            transform.position += direction * currentMoveSpeed * Time.deltaTime;
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
        transform.position += direction * currentMoveSpeed * Time.deltaTime;

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

            monster.ApplyConstantWindPull(pullOrigin, activeWindPullSpeed);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || finished) return;

        if (instantLinger)
        {
            if (singleTargetHit) return;

            Monster lingerHit = other.GetComponent<Monster>();
            if (lingerHit == null) return;

            if (rockRootOnHit)
                TryRockHit(lingerHit);
            else
                TryPierceHit(lingerHit);
            return;
        }

        if (freeFlight)
        {
            Monster pierced = other.GetComponent<Monster>();
            if (pierced == null) return;

            if (grimoireMultiHit)
                ApplyGrimoireMultiHit(pierced);
            else
                TryPierceHit(pierced);
            return;
        }

        Monster monster = other.GetComponent<Monster>();
        if (monster == null || monster != target) return;

        if (grimoireMultiHit)
        {
            ApplyGrimoireMultiHit(monster);
            return;
        }

        Vector3 hitPosition = transform.position;
        TrySpawnAdditionalProjectile(monster, hitPosition);

        if (!reservationConsumed)
        {
            reservationConsumed = true;
            monster.TryApplyReservedHit(this, lastHitWasCritical);
        }

        Finish();
    }

    private void ApplyGrimoireMultiHit(Monster monster)
    {
        if (!grimoireMultiHit || monster == null || monster.IsDead || multiHitRemaining <= 0) return;
        if (multiHitInProgress) return;

        BeginGrimoireMultiHit(monster);
    }

    private void BeginGrimoireMultiHit(Monster monster)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;
        else
            direction.Normalize();

        grimoireFocusMonster = monster;
        freeFlight = false;
        multiHitInProgress = true;
        grimoireDistanceSinceHit = 0f;

        // Off-track: slow to S-k and deal first hit at contact.
        EnsureFlightSpeeds();
        currentMoveSpeed = GetGrimoireSlowSpeed();

        ApplyGrimoireFocusHit();
        multiHitRemaining--;

        if (multiHitRemaining <= 0)
        {
            EndGrimoireMultiHit(destroyProjectile: true);
            return;
        }

        if (IsGrimoireFocusDead() || !IsOverlappingFocus())
            ResumeGrimoireFullSpeed();
    }

    private void UpdateGrimoireFlight()
    {
        EnsureFlightSpeeds();

        // Off-track while engaged with a living overlapping monster.
        if (multiHitInProgress)
        {
            if (IsGrimoireFocusDead() || !IsOverlappingFocus())
            {
                // Back on-track: restore full speed immediately.
                ResumeGrimoireFullSpeed();
            }
            else
            {
                UpdateGrimoireMultiHitMovement();
                return;
            }
        }

        // On-track: always fly at launch speed S.
        currentMoveSpeed = GetFullFlightSpeed();

        if (freeFlight || target == null)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.up;

            transform.position += direction * currentMoveSpeed * Time.deltaTime;
            Physics2D.SyncTransforms();

            if (IsOffScreen())
            {
                Finish();
                return;
            }

            // Re-enter off-track if we are still/again touching a monster.
            TryBeginGrimoireMultiHitFromOverlap();
            return;
        }

        FaceToward(target.transform.position);
        transform.position += direction * currentMoveSpeed * Time.deltaTime;

        if (Vector3.Distance(spawnPosition, transform.position) > range)
            Finish();
    }

    private void UpdateGrimoireMultiHitMovement()
    {
        currentMoveSpeed = GetGrimoireSlowSpeed();

        float travel = currentMoveSpeed * Time.deltaTime;
        transform.position += direction * travel;
        grimoireDistanceSinceHit += travel;
        Physics2D.SyncTransforms();

        if (IsOffScreen())
        {
            EndGrimoireMultiHit(destroyProjectile: true);
            return;
        }

        while (multiHitRemaining > 0 && grimoireDistanceSinceHit >= GrimoireMultiHitStep)
        {
            if (!IsOverlappingFocus() || IsGrimoireFocusDead())
                break;

            grimoireDistanceSinceHit -= GrimoireMultiHitStep;
            ApplyGrimoireFocusHit();
            multiHitRemaining--;

            if (multiHitRemaining <= 0)
            {
                EndGrimoireMultiHit(destroyProjectile: true);
                return;
            }
        }

        if (multiHitRemaining <= 0)
            return;

        if (IsGrimoireFocusDead() || !IsOverlappingFocus())
            ResumeGrimoireFullSpeed();
    }

    private void TryBeginGrimoireMultiHitFromOverlap()
    {
        if (multiHitInProgress || multiHitRemaining <= 0) return;

        Collider2D selfCollider = GetComponent<Collider2D>();
        if (selfCollider == null) return;

        overlapBuffer.Clear();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.NoFilter();
        Physics2D.OverlapCollider(selfCollider, filter, overlapBuffer);

        for (int i = 0; i < overlapBuffer.Count; i++)
        {
            Collider2D other = overlapBuffer[i];
            if (other == null) continue;

            Monster monster = other.GetComponent<Monster>();
            if (monster == null || monster.IsDead) continue;

            BeginGrimoireMultiHit(monster);
            return;
        }
    }

    private bool IsOverlappingFocus()
    {
        if (grimoireFocusMonster == null || grimoireFocusMonster.IsDead) return false;

        Collider2D selfCollider = GetComponent<Collider2D>();
        if (selfCollider == null) return false;

        Collider2D[] focusColliders = grimoireFocusMonster.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < focusColliders.Length; i++)
        {
            Collider2D other = focusColliders[i];
            if (other != null && other.enabled && selfCollider.IsTouching(other))
                return true;
        }

        return false;
    }

    private float GetGrimoireSlowSpeed()
    {
        return Mathf.Max(0.1f, GetFullFlightSpeed() - GrimoireHitSpeedReduce);
    }

    private void ApplyGrimoireFocusHit()
    {
        Monster monster = grimoireFocusMonster;
        // Do not gate on CanBeTargeted: Init may already reserve enough damage that
        // ExpectedRemainingHp becomes 0, which would skip applying the reserved hit.
        if (monster == null || monster.IsDead) return;

        monster.ApplyTemporarySlow(GrimoireMonsterSlowMultiplier, GrimoireMonsterSlowDuration);

        int amount = RollHitDamage();
        if (amount <= 0) return;

        TryApplyOneGrimoireHit(monster, amount);
    }

    private bool TryApplyOneGrimoireHit(Monster monster, int amount)
    {
        if (!reservationConsumed && target == monster)
        {
            reservationConsumed = true;
            return monster.TryApplyReservedHit(this, lastHitWasCritical);
        }

        // Multi-hit ticks after the first: apply directly so overkill reservation
        // / ExpectedRemainingHp == 0 cannot block subsequent multi-hit ticks.
        if (monster.IsDead || monster.Hp <= 0) return false;
        monster.ReleaseReservation(this);
        monster.ApplyDamage(amount, lastHitWasCritical);
        return true;
    }

    private bool IsGrimoireFocusDead()
    {
        return grimoireFocusMonster == null || grimoireFocusMonster.IsDead;
    }

    private void ResumeGrimoireFullSpeed()
    {
        multiHitInProgress = false;
        grimoireFocusMonster = null;
        target = null;
        freeFlight = true;
        reservationConsumed = true;
        currentMoveSpeed = GetFullFlightSpeed();
        grimoireDistanceSinceHit = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;
    }

    private void EndGrimoireMultiHit(bool destroyProjectile)
    {
        multiHitInProgress = false;
        grimoireFocusMonster = null;
        currentMoveSpeed = GetFullFlightSpeed();

        if (destroyProjectile && !finished)
            Finish();
    }

    private void EnsureFlightSpeeds()
    {
        if (baseMoveSpeed <= 0f)
            baseMoveSpeed = moveSpeed;
        if (fullFlightSpeed <= 0f)
            fullFlightSpeed = baseMoveSpeed > 0f ? baseMoveSpeed : moveSpeed;
    }

    private float GetFullFlightSpeed()
    {
        EnsureFlightSpeeds();
        return fullFlightSpeed;
    }

    private void TryPierceHit(Monster monster)
    {
        if (monster == null || !monster.CanBeTargeted) return;
        if (!piercedMonsters.Add(monster)) return;

        int amount = RollHitDamage();
        if (amount <= 0) return;
        if (!monster.TryReserve(this, amount)) return;

        monster.TryApplyReservedHit(this, lastHitWasCritical);
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

        if (sourceCategory == Item.ItemCategory.Water)
        {
            SpawnWaterAdditionalProjectile(hitPosition, hitMonster);
            return;
        }

        if (sourceCategory == Item.ItemCategory.Rock)
        {
            SpawnRockAdditionalProjectile(hitPosition);
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
        ConfigureSpawnedAdditional(additional);

        Monster nextTarget = FindNearestMonster(hitPosition, additional.Range, hitMonster);
        if (nextTarget == null || !additional.Init(nextTarget))
            Destroy(additional.gameObject);
    }

    private void SpawnInstantAdditionalProjectile(Vector3 hitPosition)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        ConfigureSpawnedAdditional(additional);
        if (!additional.InitInstantInPlace())
            Destroy(additional.gameObject);
    }

    private void SpawnWindAdditionalProjectile(Vector3 hitPosition)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        ConfigureSpawnedAdditional(additional);
        float pullSpeed = HasEnhancedAdditional ? WindPullSpeed * 2f : WindPullSpeed;
        additional.EnableWindPull(pullSpeed);
        if (!additional.InitInstantInPlace())
            Destroy(additional.gameObject);
    }

    private void SpawnWaterAdditionalProjectile(Vector3 hitPosition, Monster hitMonster)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        ConfigureSpawnedAdditional(additional);
        float knockback = HasEnhancedAdditional ? WaterKnockbackDistance * 2f : WaterKnockbackDistance;
        if (!additional.InitWaterAdditional(hitMonster, knockback))
            Destroy(additional.gameObject);
    }

    private void SpawnRockAdditionalProjectile(Vector3 hitPosition)
    {
        Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
        ConfigureSpawnedAdditional(additional);
        float rootDuration = HasEnhancedAdditional ? RockRootDuration * 2f : RockRootDuration;
        if (!additional.InitRockAdditional(rootDuration))
            Destroy(additional.gameObject);
    }

    private void SpawnFireAdditionalProjectiles(Vector3 hitPosition, Vector3 facingDirection)
    {
        Vector3 facing = facingDirection.sqrMagnitude > 0.0001f
            ? facingDirection.normalized
            : Vector3.up;

        int waveCount = HasEnhancedAdditional ? FireAdditionalEnhancedWaveCount : FireAdditionalWaveCount;
        int lineCount = HasEnhancedAdditional ? FireAdditionalEnhancedLineCount : FireAdditionalLineCount;

        GameObject host = new GameObject("FireAdditionalSequence");
        FireAdditionalSequence sequence = host.AddComponent<FireAdditionalSequence>();
        sequence.Play(
            additionalProjectilePrefab,
            damage,
            criticalChance,
            hitPosition,
            facing,
            waveCount,
            lineCount,
            FireAdditionalWaveInterval,
            FireAdditionalBaseScale,
            FireAdditionalScaleStep,
            FireAdditionalBaseRadius,
            FireAdditionalRadiusStep,
            FireAdditionalSideAngleDegrees);
    }

    private void SpawnIceAdditionalProjectiles(Vector3 hitPosition)
    {
        int directionCount = HasEnhancedAdditional
            ? IceAdditionalEnhancedDirectionCount
            : IceAdditionalDirectionCount;
        float stepDegrees = 360f / directionCount;
        float startAngleDegrees = Random.Range(0f, stepDegrees);

        for (int i = 0; i < directionCount; i++)
        {
            float angleRad = (startAngleDegrees + stepDegrees * i) * Mathf.Deg2Rad;
            Vector3 moveDirection = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

            Projectile additional = Instantiate(additionalProjectilePrefab, hitPosition, Quaternion.identity);
            ConfigureSpawnedAdditional(additional);
            if (!additional.InitFreeFlight(moveDirection))
                Destroy(additional.gameObject);
        }
    }

    private void ConfigureSpawnedAdditional(Projectile additional)
    {
        additional.ApplyDamage(damage);
        additional.ApplyCriticalChance(criticalChance);
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
        float criticalChance,
        Vector3 position,
        Vector3 facing,
        int waveCount,
        int lineCount,
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
            criticalChance,
            position,
            facing,
            waveCount,
            lineCount,
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
        float criticalChance,
        Vector3 position,
        Vector3 facing,
        int waveCount,
        int lineCount,
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
        float[] directionAngles = BuildFireDirectionAngles(lineCount, sideAngleDegrees);

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
                additional.ApplyCriticalChance(criticalChance);
                additional.transform.localScale = prefabScale * scaleMultiplier;

                if (!additional.InitInstantInPlace())
                    Destroy(additional.gameObject);
            }

            if (wave < waveCount - 1)
                yield return new WaitForSeconds(waveInterval);
        }

        Destroy(gameObject);
    }

    private static float[] BuildFireDirectionAngles(int lineCount, float sideAngleDegrees)
    {
        int count = Mathf.Max(1, lineCount);
        float[] angles = new float[count];
        if (count == 1)
        {
            angles[0] = 0f;
            return angles;
        }

        int half = count / 2;
        int index = 0;
        for (int i = -half; i <= half; i++)
        {
            // Skip duplicate center when even count.
            if (count % 2 == 0 && i == 0) continue;
            if (index >= count) break;
            angles[index++] = i * sideAngleDegrees;
        }

        return angles;
    }
}
