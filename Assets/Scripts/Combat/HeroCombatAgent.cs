using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hero combat agent with role-specific positioning, animated sprites, and actions:
/// - Knights: Frontline melee attackers with attack lunges.
/// - Mages: Backline casters that shoot fireballs and kite away from close enemies.
/// - Healers: Backline supports that cast healing orbs on injured allies and update HP bars.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HeroCombatAgent : MonoBehaviour
{
    public HeroInstance heroInstance { get; private set; }

    private ICombatController combat;
    private CharacterAnimator animator;
    private SpriteRenderer sr;
    private float attackTimer;
    private bool dodgeRolling;
    private bool isDying;
    private int gridIndex;
    private float baseUniformScale = 1.2f;

    // Movement & Range
    private Vector2 targetPosition;
    private bool isMovingToTarget;
    private float attackRange = 1.2f;
    private float kiteDistance = 0f;
    private Vector2 formationOffset;

    // Multi-waypoint corridor pathing
    private Vector2[] waypointPath;
    private int currentWaypointIndex;
    private bool isFollowingPath;

    // Dynamic combat strafing
    private float strafeSign = 1f;
    private float strafeTimer;

    // Coroutine handles to prevent race conditions during x2 speedup
    private Coroutine lungeCoroutine;
    private Coroutine castCoroutine;
    private Coroutine healFlashCoroutine;
    private Coroutine dieCoroutine;
    private Coroutine dodgeCoroutine;

    public bool IsAlive => heroInstance != null && heroInstance.isAlive;
    public HeroClass HeroClass => heroInstance?.data != null ? heroInstance.data.heroClass : HeroClass.Knight;

    public float EffectiveMoveSpeed
    {
        get
        {
            float speed = heroInstance?.data != null ? heroInstance.data.moveSpeed : 4f;
            if (GameRunData.Instance != null && GameRunData.Instance.IsSpeedBuffActive)
            {
                speed *= 1.4f;
            }
            return speed;
        }
    }

    public int EffectiveAttackDamage
    {
        get
        {
            int dmg = heroInstance?.data != null ? heroInstance.data.attackDamage : 10;
            if (GameRunData.Instance != null && GameRunData.Instance.IsPowerBuffActive)
            {
                dmg = Mathf.RoundToInt(dmg * 1.35f);
            }
            return dmg;
        }
    }

    public float EffectiveAttackSpeed
    {
        get
        {
            float spd = heroInstance?.data != null ? heroInstance.data.attackSpeed : 1.2f;
            if (GameRunData.Instance != null && GameRunData.Instance.IsSpeedBuffActive)
            {
                spd *= 1.2f;
            }
            return spd;
        }
    }

    public void SetInitiallyDead()
    {
        StopAllCoroutines();
        lungeCoroutine = null;
        castCoroutine = null;
        healFlashCoroutine = null;
        dieCoroutine = null;
        dodgeCoroutine = null;
        isDying = false;
        dodgeRolling = false;
        isFollowingPath = false;
        isMovingToTarget = false;
        waypointPath = null;
        gameObject.SetActive(false);
    }

    public void Revive(Vector2 spawnPos)
    {
        if (heroInstance == null || heroInstance.data == null) return;

        StopAllCoroutines();
        lungeCoroutine = null;
        castCoroutine = null;
        healFlashCoroutine = null;
        dieCoroutine = null;
        dodgeCoroutine = null;

        isDying = false;
        dodgeRolling = false;
        isFollowingPath = false;
        isMovingToTarget = false;
        waypointPath = null;
        currentWaypointIndex = 0;
        attackTimer = 0f;

        heroInstance.currentHP = Mathf.Max(1, Mathf.RoundToInt(heroInstance.data.maxHP * 0.5f));

        Rect? bounds = combat?.GetCurrentRoomBounds();
        if (bounds.HasValue)
        {
            Rect b = bounds.Value;
            spawnPos.x = Mathf.Clamp(spawnPos.x, b.xMin, b.xMax);
            spawnPos.y = Mathf.Clamp(spawnPos.y, b.yMin, b.yMax);
        }

        transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
        transform.localScale = Vector3.one * baseUniformScale;

        if (sr != null)
        {
            sr.color = Color.white;
        }

        gameObject.SetActive(true);

        if (animator != null)
        {
            animator.ResetTint();
            animator.SetState(CharacterAnimator.AnimState.Idle);
            animator.SetFacing(true);
            animator.FlashHit();
        }

        CombatFeedback.Instance?.ShowText(transform.position, "REVIVED! 💖", new Color(0.2f, 1f, 0.4f), 4f);
        combat?.RefreshPortraits();
    }

    public void UpdateFormationSlot(int newSlotIdx)
    {
        gridIndex = newSlotIdx;
        if (heroInstance != null)
        {
            heroInstance.gridIndex = newSlotIdx;
        }
        int col = Mathf.Clamp(newSlotIdx, 0, 8) % 3;
        int row = Mathf.Clamp(newSlotIdx, 0, 8) / 3;
        formationOffset = new Vector2(col * 1.3f - 1.3f, (1 - row) * 1.0f);
    }

    public void SnapToPosition(Vector2 position)
    {
        Vector2 target = position + formationOffset;
        Rect? bounds = combat?.GetCurrentRoomBounds();
        if (bounds.HasValue)
        {
            Rect b = bounds.Value;
            target.x = Mathf.Clamp(target.x, b.xMin, b.xMax);
            target.y = Mathf.Clamp(target.y, b.yMin, b.yMax);
        }
        transform.position = new Vector3(target.x, target.y, 0f);
        isFollowingPath = false;
        isMovingToTarget = false;
        waypointPath = null;
    }

    public void ClearMovementTargets()
    {
        isFollowingPath = false;
        isMovingToTarget = false;
        waypointPath = null;
        currentWaypointIndex = 0;
        if (animator != null && gameObject.activeInHierarchy)
            animator.SetState(CharacterAnimator.AnimState.Idle);
    }

    public void SnapToPathEnd()
    {
        if (isFollowingPath && waypointPath != null && waypointPath.Length > 0)
        {
            Vector2 end = waypointPath[waypointPath.Length - 1];
            transform.position = new Vector3(end.x, end.y, 0f);
        }
        else if (isMovingToTarget)
        {
            transform.position = new Vector3(targetPosition.x, targetPosition.y, 0f);
        }
        ClearMovementTargets();
    }

    public void Init(HeroInstance hero, int slotIdx, ICombatController ctrl)
    {
        heroInstance = hero;
        combat = ctrl;
        gridIndex = Mathf.Clamp(slotIdx, 0, 8);

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        animator = GetComponent<CharacterAnimator>() ?? gameObject.AddComponent<CharacterAnimator>();
        string spritePrefix = SpriteMapping.GetHeroSpritePrefix(hero.data.heroName, hero.data.spritePrefix);
        animator.Setup(spritePrefix);

        switch (hero.data.heroClass)
        {
            case HeroClass.Knight:
                attackRange = 1.3f;
                kiteDistance = 0f;
                break;
            case HeroClass.Mage:
                attackRange = 3.8f;
                kiteDistance = 2.2f;
                break;
            case HeroClass.Healer:
                attackRange = 4.0f;
                kiteDistance = 2.4f;
                break;
        }

        strafeSign = (gridIndex % 2 == 0) ? 1f : -1f;
        strafeTimer = Random.Range(1.2f, 2.4f);

        int col = gridIndex % 3;
        int row = gridIndex / 3;
        formationOffset = new Vector2(col * 1.3f - 1.3f, (1 - row) * 1.0f);

        transform.localScale = Vector3.one * baseUniformScale;
    }

    void LateUpdate()
    {
        // Dynamic top-down Y-sorting: lower Y = closer to foreground = higher sortingOrder
        if (sr != null)
        {
            sr.sortingOrder = 2000 - Mathf.RoundToInt(transform.position.y * 10);
        }
    }

    void Update()
    {
        if (!IsAlive || isDying || combat == null || combat.IsPaused || dodgeRolling) return;

        if (combat.IsInCombatPhase)
        {
            HandleCombat();
            ClampToRoomBounds();
        }
        else if (isFollowingPath)
        {
            FollowPath();
        }
        else if (isMovingToTarget)
        {
            MoveToTarget();
        }
    }

    void HandleCombat()
    {
        attackTimer -= Time.deltaTime;

        // 1. Role-specific behavior: HEALER
        if (HeroClass == HeroClass.Healer)
        {
            HandleHealerCombat();
            return;
        }

        // 2. Role-specific behavior: MAGE
        if (HeroClass == HeroClass.Mage)
        {
            HandleMageCombat();
            return;
        }

        // 3. Role-specific behavior: KNIGHT (Melee)
        HandleKnightCombat();
    }

    // ---- Knight Behavior (Frontline Melee) ----
    void HandleKnightCombat()
    {
        var target = FindNearestEnemy();
        if (target == null)
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        float dist = Vector2.Distance(transform.position, target.transform.position);
        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        if (dist > attackRange)
        {
            transform.position += (Vector3)(dir * EffectiveMoveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(dir.x >= 0);
        }
        else
        {
            // Close melee engagement — face enemy and strike
            animator.SetFacing(dir.x >= 0);
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / Mathf.Max(0.5f, EffectiveAttackSpeed);
                PerformMeleeAttack(target);
            }
            else
            {
                // Active melee combat stance
                animator.SetState(CharacterAnimator.AnimState.Idle);
            }
        }
    }

    void PerformMeleeAttack(EnemyCombatAgent target)
    {
        if (target == null || !target.IsAlive || !gameObject.activeInHierarchy) return;

        int dmg = EffectiveAttackDamage;
        target.TakeDamage(dmg, this);
        heroInstance.UseEnergy(5);

        // Floating combat text on target
        CombatFeedback.Instance?.ShowText(target.transform.position, $"-{dmg}", Color.white, 3.2f);

        if (lungeCoroutine != null) StopCoroutine(lungeCoroutine);
        lungeCoroutine = StartCoroutine(AttackLunge(target.transform.position));
    }

    // ---- Mage Behavior (Dynamic Fireball Caster — Never Stays Frozen!) ----
    void HandleMageCombat()
    {
        var target = FindNearestEnemy();
        if (target == null)
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        float dist = Vector2.Distance(transform.position, target.transform.position);
        Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        // Strafe timer alternation
        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeTimer = Random.Range(1.3f, 2.5f);
            strafeSign *= -1f;
        }

        // 1. Kite backwards if enemy gets dangerously close
        if (dist < kiteDistance)
        {
            Vector2 awayDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            transform.position += (Vector3)(awayDir * (EffectiveMoveSpeed * 1.1f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0); // Keep facing enemy to fire
        }
        // 2. Advance forward if enemy is out of casting range
        else if (dist > attackRange)
        {
            transform.position += (Vector3)(toTarget * EffectiveMoveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0);
        }
        // 3. In comfortable casting range — DYNAMIC LATERAL STRAFE (Never stand frozen!)
        else
        {
            // Perpendicular orbital strafe
            Vector2 perp = new Vector2(-toTarget.y, toTarget.x) * strafeSign;
            // Balance toward 3.0m sweet spot
            float distDiff = dist - 3.0f;
            Vector2 moveDir = (perp * 0.85f + toTarget * (distDiff * 0.5f)).normalized;

            transform.position += (Vector3)(moveDir * (EffectiveMoveSpeed * 0.7f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0);
        }

        // Cast fireball when ready
        if (attackTimer <= 0f && dist <= attackRange + 0.6f)
        {
            attackTimer = 1f / Mathf.Max(0.5f, EffectiveAttackSpeed);
            CastFireball(target);
        }
    }

    void CastFireball(EnemyCombatAgent target)
    {
        if (target == null || !target.IsAlive || !gameObject.activeInHierarchy) return;

        heroInstance.UseEnergy(10);
        int dmg = Mathf.RoundToInt(EffectiveAttackDamage * 1.35f);

        if (castCoroutine != null) StopCoroutine(castCoroutine);
        castCoroutine = StartCoroutine(CastAnimation());

        var controller = combat;
        CombatProjectile.Spawn(
            transform.position,
            target.transform,
            new Color(1f, 0.45f, 0.1f), // fiery orange
            9.5f,
            () =>
            {
                if (controller != null && controller.IsInCombatPhase && target != null && target.IsAlive)
                {
                    target.TakeDamage(dmg, this);
                    CombatFeedback.Instance?.ShowText(target.transform.position, $"-{dmg} 💥", new Color(1f, 0.6f, 0.1f), 3.8f);
                }
            },
            0.65f
        );
    }

    // ---- Healer Behavior (Support, Healing & Light Bolts) ----
    void HandleHealerCombat()
    {
        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeTimer = Random.Range(1.4f, 2.6f);
            strafeSign *= -1f;
        }

        // 1. Check if any ally needs healing
        var woundedAlly = FindLowestHPAlly();
        bool allyNeedsHeal = woundedAlly != null && woundedAlly.heroInstance.currentHP < woundedAlly.heroInstance.data.maxHP * 0.95f;

        if (allyNeedsHeal)
        {
            float distToAlly = Vector2.Distance(transform.position, woundedAlly.transform.position);
            Vector2 toAlly = ((Vector2)woundedAlly.transform.position - (Vector2)transform.position).normalized;

            if (distToAlly > 2.8f)
            {
                // Advance toward injured comrade
                transform.position += (Vector3)(toAlly * EffectiveMoveSpeed * Time.deltaTime);
                animator.SetState(CharacterAnimator.AnimState.Run);
                animator.SetFacing(toAlly.x >= 0);
            }
            else
            {
                // Hover near ally while casting
                Vector2 perp = new Vector2(-toAlly.y, toAlly.x) * strafeSign;
                transform.position += (Vector3)(perp * (EffectiveMoveSpeed * 0.5f) * Time.deltaTime);
                animator.SetState(CharacterAnimator.AnimState.Run);
                animator.SetFacing(toAlly.x >= 0);

                if (attackTimer <= 0f)
                {
                    attackTimer = 2.4f; // Heal cooldown
                    CastHealingSpell(woundedAlly);
                }
            }
            return;
        }

        // 2. All allies healthy: maintain backline support position and fire light bolts
        var target = FindNearestEnemy();
        if (target == null)
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        float distToEnemy = Vector2.Distance(transform.position, target.transform.position);
        Vector2 toEnemy = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        if (distToEnemy < kiteDistance)
        {
            Vector2 awayDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            transform.position += (Vector3)(awayDir * EffectiveMoveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toEnemy.x >= 0);
        }
        else if (distToEnemy > attackRange)
        {
            transform.position += (Vector3)(toEnemy * (EffectiveMoveSpeed * 0.85f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toEnemy.x >= 0);
        }
        else
        {
            // Supportive lateral strafe
            Vector2 perp = new Vector2(-toEnemy.y, toEnemy.x) * strafeSign;
            transform.position += (Vector3)(perp * (EffectiveMoveSpeed * 0.55f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toEnemy.x >= 0);

            if (attackTimer <= 0f)
            {
                attackTimer = 1.6f;
                CastLightBolt(target);
            }
        }
    }

    void CastHealingSpell(HeroCombatAgent targetAlly)
    {
        if (targetAlly == null || !targetAlly.IsAlive || !gameObject.activeInHierarchy) return;

        heroInstance.UseEnergy(15);
        int healAmount = 25;

        if (castCoroutine != null) StopCoroutine(castCoroutine);
        castCoroutine = StartCoroutine(CastAnimation());

        var controller = combat;
        CombatProjectile.Spawn(
            transform.position,
            targetAlly.transform,
            new Color(0.2f, 0.95f, 0.4f), // emerald green
            7.5f,
            () =>
            {
                if (controller != null && controller.IsInCombatPhase && targetAlly != null && targetAlly.IsAlive)
                {
                    targetAlly.ReceiveHeal(healAmount);
                }
            },
            0.6f
        );
    }

    void CastLightBolt(EnemyCombatAgent target)
    {
        if (target == null || !target.IsAlive || !gameObject.activeInHierarchy) return;

        int dmg = Mathf.RoundToInt(EffectiveAttackDamage * 0.75f);
        if (castCoroutine != null) StopCoroutine(castCoroutine);
        castCoroutine = StartCoroutine(CastAnimation());

        var controller = combat;
        CombatProjectile.Spawn(
            transform.position,
            target.transform,
            new Color(0.6f, 0.9f, 1f), // light blue bolt
            8.5f,
            () =>
            {
                if (controller != null && controller.IsInCombatPhase && target != null && target.IsAlive)
                {
                    target.TakeDamage(dmg, this);
                    CombatFeedback.Instance?.ShowText(target.transform.position, $"-{dmg}", new Color(0.6f, 0.9f, 1f), 2.8f);
                }
            },
            0.45f
        );
    }

    IEnumerator CastAnimation()
    {
        Vector3 baseScale = Vector3.one * baseUniformScale;
        float t = 0f;
        while (t < 0.16f)
        {
            t += Time.deltaTime;
            float factor = 1f + 0.22f * Mathf.Sin((t / 0.16f) * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * factor, baseScale.y * (2f - factor), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
        castCoroutine = null;
    }

    // ---- Targeting Helpers ----
    EnemyCombatAgent FindNearestEnemy()
    {
        EnemyCombatAgent nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var e in combat.EnemyAgents)
        {
            if (!e.IsAlive) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = e; }
        }
        return nearest;
    }

    HeroCombatAgent FindLowestHPAlly()
    {
        HeroCombatAgent lowest = null;
        float minRatio = 1.1f;
        foreach (var h in combat.HeroAgents)
        {
            if (!h.IsAlive) continue;
            float ratio = (float)h.heroInstance.currentHP / h.heroInstance.data.maxHP;
            if (ratio < minRatio)
            {
                minRatio = ratio;
                lowest = h;
            }
        }
        return lowest;
    }

    IEnumerator AttackLunge(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        Vector3 lungePos = Vector3.Lerp(startPos, targetPos, 0.3f);

        float t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, lungePos, t / 0.1f);
            yield return null;
        }
        t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(lungePos, startPos, t / 0.1f);
            yield return null;
        }
        transform.position = startPos;
        lungeCoroutine = null;
    }

    public void TakeDamage(int dmg)
    {
        if (!IsAlive || isDying) return;

        heroInstance.TakeDamage(dmg);
        if (animator) animator.FlashHit();

        // Show floating damage above hero
        CombatFeedback.Instance?.ShowText(transform.position, $"-{dmg}", new Color(1f, 0.3f, 0.3f), 3.2f);

        // Immediately update HUD portraits
        combat?.RefreshPortraits();

        if (!IsAlive)
        {
            isDying = true;
            if (lungeCoroutine != null) StopCoroutine(lungeCoroutine);
            if (castCoroutine != null) StopCoroutine(castCoroutine);
            if (dodgeCoroutine != null) StopCoroutine(dodgeCoroutine);
            if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
            if (dieCoroutine != null) StopCoroutine(dieCoroutine);
            dieCoroutine = StartCoroutine(DieEffect());
        }
    }

    IEnumerator DieEffect()
    {
        isDying = true;
        if (sr) sr.color = new Color(1f, 0.3f, 0.3f, 1f);
        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
            if (sr) sr.color = new Color(1f, 0.3f, 0.3f, alpha);
            transform.localScale = Vector3.Lerp(Vector3.one * baseUniformScale, Vector3.one * 0.3f, t / 0.5f);
            yield return null;
        }
        isDying = false;
        dieCoroutine = null;
        gameObject.SetActive(false);
    }

    public void TriggerDodgeRoll()
    {
        if (!gameObject.activeInHierarchy || isDying) return;
        if (dodgeCoroutine != null) StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeRoll());
    }

    IEnumerator DodgeRoll()
    {
        dodgeRolling = true;
        Vector3 startPos = transform.position;
        Vector3 rollPos = startPos + Vector3.right * 2f + Vector3.up;
        animator.SetState(CharacterAnimator.AnimState.Run);

        float t = 0;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(startPos, rollPos, t / 0.4f);
            yield return null;
        }
        dodgeRolling = false;
        dodgeCoroutine = null;
    }

    public void ReceiveHeal(int amount)
    {
        if (!IsAlive || isDying) return;

        heroInstance.currentHP = Mathf.Min(heroInstance.currentHP + amount, heroInstance.data.maxHP);

        // Green floating heal text
        CombatFeedback.Instance?.ShowText(transform.position, $"+{amount} HP", new Color(0.25f, 0.95f, 0.3f), 3.6f);

        // Immediately update HUD portraits so the player sees HP bar move!
        combat?.RefreshPortraits();

        if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
        healFlashCoroutine = StartCoroutine(HealFlash());
    }

    IEnumerator HealFlash()
    {
        if (sr)
        {
            sr.color = new Color(0.3f, 1f, 0.4f, 1f);
            yield return new WaitForSeconds(0.2f);
            sr.color = Color.white;
        }
        healFlashCoroutine = null;
    }

    public void MoveTo(Vector2 position)
    {
        targetPosition = position + formationOffset;
        isMovingToTarget = true;
        isFollowingPath = false;
        animator.SetState(CharacterAnimator.AnimState.Run);
        animator.SetFacing(targetPosition.x > transform.position.x);
    }

    /// <summary>
    /// Commands this hero to follow a multi-waypoint path (e.g. through a corridor).
    /// The final waypoint uses the hero's formation offset.
    /// </summary>
    public void MoveAlongPath(Vector2[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // Apply formation offset only to the final destination
        waypointPath = new Vector2[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            waypointPath[i] = (i == waypoints.Length - 1)
                ? waypoints[i] + formationOffset
                : waypoints[i];
        }

        currentWaypointIndex = 0;
        isFollowingPath = true;
        isMovingToTarget = false;
        animator.SetState(CharacterAnimator.AnimState.Run);
    }

    void FollowPath()
    {
        if (waypointPath == null || currentWaypointIndex >= waypointPath.Length)
        {
            isFollowingPath = false;
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        Vector2 target = waypointPath[currentWaypointIndex];
        float dist = Vector2.Distance(transform.position, target);
        float step = EffectiveMoveSpeed * Time.deltaTime;

        if (dist <= Mathf.Max(0.18f, step))
        {
            // Snap and advance to next waypoint
            transform.position = new Vector3(target.x, target.y, 0f);
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypointPath.Length)
            {
                isFollowingPath = false;
                animator.SetState(CharacterAnimator.AnimState.Idle);
            }
            return;
        }

        Vector2 dir = (target - (Vector2)transform.position).normalized;
        transform.position += (Vector3)(dir * step);
        animator.SetFacing(dir.x >= 0);
    }

    void MoveToTarget()
    {
        float dist = Vector2.Distance(transform.position, targetPosition);
        float step = EffectiveMoveSpeed * Time.deltaTime;
        if (dist <= Mathf.Max(0.18f, step))
        {
            transform.position = (Vector3)targetPosition;
            isMovingToTarget = false;
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        Vector2 dir = (targetPosition - (Vector2)transform.position).normalized;
        transform.position += (Vector3)(dir * step);
        animator.SetFacing(dir.x >= 0);
    }

    public bool HasReachedTarget()
    {
        if (isFollowingPath) return false;
        if (!isMovingToTarget) return true;
        return Vector2.Distance(transform.position, targetPosition) < 0.15f;
    }

    /// <summary>
    /// Clamps hero position to the current room's inner walkable area during combat.
    /// Prevents mage strafing/kiting from drifting outside room walls.
    /// </summary>
    void ClampToRoomBounds()
    {
        Rect? bounds = combat?.GetCurrentRoomBounds();
        if (bounds == null) return;

        Rect b = bounds.Value;
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, b.xMin, b.xMax);
        pos.y = Mathf.Clamp(pos.y, b.yMin, b.yMax);
        transform.position = pos;
    }
}