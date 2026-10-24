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
    private int gridIndex;

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

    public bool IsAlive => heroInstance != null && heroInstance.isAlive;
    public HeroClass HeroClass => heroInstance?.data.heroClass ?? HeroClass.Knight;

    public void Init(HeroInstance hero, int slotIdx, ICombatController ctrl)
    {
        heroInstance = hero;
        combat = ctrl;
        gridIndex = slotIdx;

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        // Setup animated sprite
        animator = gameObject.AddComponent<CharacterAnimator>();
        string spritePrefix = SpriteMapping.GetHeroSpritePrefix(hero.data.heroName);
        animator.Setup(spritePrefix);

        // Role-based ranges
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

        strafeSign = (slotIdx % 2 == 0) ? 1f : -1f;
        strafeTimer = Random.Range(1.2f, 2.4f);

        // Formation offset with generous spacing so heroes never overlap (1.3m X, 1.0m Y)
        int col = slotIdx % 3;
        int row = slotIdx / 3;
        formationOffset = new Vector2(col * 1.3f - 1.3f, (1 - row) * 1.0f);

        transform.localScale = Vector3.one * 1.2f;
    }

    void LateUpdate()
    {
        // Dynamic top-down Y-sorting: lower Y = closer to foreground = higher sortingOrder
        if (sr != null)
        {
            sr.sortingOrder = 100 - Mathf.RoundToInt(transform.position.y * 10);
        }
    }

    void Update()
    {
        if (!IsAlive || combat == null || combat.IsPaused || dodgeRolling) return;

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
            transform.position += (Vector3)(dir * heroInstance.data.moveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(dir.x >= 0);
        }
        else
        {
            // Close melee engagement — face enemy and strike
            animator.SetFacing(dir.x >= 0);
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / Mathf.Max(0.5f, heroInstance.data.attackSpeed);
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
        int dmg = heroInstance.data.attackDamage;
        target.TakeDamage(dmg, this);
        heroInstance.UseEnergy(5);

        // Floating combat text on target
        CombatFeedback.Instance.ShowText(target.transform.position, $"-{dmg}", Color.white, 3.2f);

        StartCoroutine(AttackLunge(target.transform.position));
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
            transform.position += (Vector3)(awayDir * (heroInstance.data.moveSpeed * 1.1f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0); // Keep facing enemy to fire
        }
        // 2. Advance forward if enemy is out of casting range
        else if (dist > attackRange)
        {
            transform.position += (Vector3)(toTarget * heroInstance.data.moveSpeed * Time.deltaTime);
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

            transform.position += (Vector3)(moveDir * (heroInstance.data.moveSpeed * 0.7f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0);
        }

        // Cast fireball when ready
        if (attackTimer <= 0f && dist <= attackRange + 0.6f)
        {
            attackTimer = 1f / Mathf.Max(0.5f, heroInstance.data.attackSpeed);
            CastFireball(target);
        }
    }

    void CastFireball(EnemyCombatAgent target)
    {
        heroInstance.UseEnergy(10);
        int dmg = Mathf.RoundToInt(heroInstance.data.attackDamage * 1.35f);

        // Spellcast hop/squash
        StartCoroutine(CastAnimation());

        // Spawn fireball projectile
        CombatProjectile.Spawn(
            transform.position,
            target.transform,
            new Color(1f, 0.45f, 0.1f), // fiery orange
            9.5f,
            () =>
            {
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(dmg, this);
                    CombatFeedback.Instance.ShowText(target.transform.position, $"-{dmg} 💥", new Color(1f, 0.6f, 0.1f), 3.8f);
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
                transform.position += (Vector3)(toAlly * heroInstance.data.moveSpeed * Time.deltaTime);
                animator.SetState(CharacterAnimator.AnimState.Run);
                animator.SetFacing(toAlly.x >= 0);
            }
            else
            {
                // Hover near ally while casting
                Vector2 perp = new Vector2(-toAlly.y, toAlly.x) * strafeSign;
                transform.position += (Vector3)(perp * (heroInstance.data.moveSpeed * 0.5f) * Time.deltaTime);
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
            transform.position += (Vector3)(awayDir * heroInstance.data.moveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toEnemy.x >= 0);
        }
        else if (distToEnemy > attackRange)
        {
            transform.position += (Vector3)(toEnemy * (heroInstance.data.moveSpeed * 0.85f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toEnemy.x >= 0);
        }
        else
        {
            // Supportive lateral strafe
            Vector2 perp = new Vector2(-toEnemy.y, toEnemy.x) * strafeSign;
            transform.position += (Vector3)(perp * (heroInstance.data.moveSpeed * 0.55f) * Time.deltaTime);
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
        heroInstance.UseEnergy(15);
        int healAmount = 25;

        StartCoroutine(CastAnimation());

        // Spawn emerald healing orb
        CombatProjectile.Spawn(
            transform.position,
            targetAlly.transform,
            new Color(0.2f, 0.95f, 0.4f), // emerald green
            7.5f,
            () =>
            {
                if (targetAlly != null && targetAlly.IsAlive)
                {
                    targetAlly.ReceiveHeal(healAmount);
                }
            },
            0.6f
        );
    }

    void CastLightBolt(EnemyCombatAgent target)
    {
        int dmg = Mathf.RoundToInt(heroInstance.data.attackDamage * 0.75f);
        StartCoroutine(CastAnimation());

        CombatProjectile.Spawn(
            transform.position,
            target.transform,
            new Color(0.6f, 0.9f, 1f), // light blue bolt
            8.5f,
            () =>
            {
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(dmg, this);
                    CombatFeedback.Instance.ShowText(target.transform.position, $"-{dmg}", new Color(0.6f, 0.9f, 1f), 2.8f);
                }
            },
            0.45f
        );
    }

    IEnumerator CastAnimation()
    {
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        while (t < 0.16f)
        {
            t += Time.deltaTime;
            float factor = 1f + 0.22f * Mathf.Sin((t / 0.16f) * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * factor, baseScale.y * (2f - factor), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
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
    }

    public void TakeDamage(int dmg)
    {
        heroInstance.TakeDamage(dmg);
        if (animator) animator.FlashHit();

        // Show floating damage above hero
        CombatFeedback.Instance.ShowText(transform.position, $"-{dmg}", new Color(1f, 0.3f, 0.3f), 3.2f);

        // Immediately update HUD portraits
        combat?.RefreshPortraits();

        if (!IsAlive)
        {
            StartCoroutine(DieEffect());
        }
    }

    IEnumerator DieEffect()
    {
        if (sr) sr.color = new Color(1f, 0.3f, 0.3f, 1f);
        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / 0.5f);
            if (sr) sr.color = new Color(1f, 0.3f, 0.3f, alpha);
            transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one * 0.3f, t / 0.5f);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    public void TriggerDodgeRoll()
    {
        StartCoroutine(DodgeRoll());
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
    }

    public void ReceiveHeal(int amount)
    {
        heroInstance.currentHP = Mathf.Min(heroInstance.currentHP + amount, heroInstance.data.maxHP);

        // Green floating heal text
        CombatFeedback.Instance.ShowText(transform.position, $"+{amount} HP", new Color(0.25f, 0.95f, 0.3f), 3.6f);

        // Immediately update HUD portraits so the player sees HP bar move!
        combat?.RefreshPortraits();

        StartCoroutine(HealFlash());
    }

    IEnumerator HealFlash()
    {
        if (sr)
        {
            sr.color = new Color(0.3f, 1f, 0.4f, 1f);
            yield return new WaitForSeconds(0.2f);
            sr.color = Color.white;
        }
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

        if (dist < 0.15f)
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
        transform.position += (Vector3)(dir * heroInstance.data.moveSpeed * Time.deltaTime);
        animator.SetFacing(dir.x >= 0);
    }

    void MoveToTarget()
    {
        float dist = Vector2.Distance(transform.position, targetPosition);
        if (dist < 0.1f)
        {
            transform.position = (Vector3)targetPosition;
            isMovingToTarget = false;
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        Vector2 dir = (targetPosition - (Vector2)transform.position).normalized;
        transform.position += (Vector3)(dir * heroInstance.data.moveSpeed * Time.deltaTime);
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