using UnityEngine;
using System.Collections;

public enum EnemyArchetype
{
    StandardMelee,
    Boss,
    Necromancer
}

/// <summary>
/// Enemy AI with animated sprites.
/// Moves toward nearest hero and attacks. Spawns within a specific dungeon room.
/// Supports regular enemies (skeletons, goblins), necromancer spawners, and bosses.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyCombatAgent : MonoBehaviour
{
    private ICombatController combat;
    private CharacterAnimator animator;
    private SpriteRenderer sr;
    private bool isBoss;
    public bool IsBoss => isBoss;
    private float attackTimer;
    private int roomIndex;  // which room this enemy belongs to
    private bool isTelegraphing;
    private int bossAttackCounter;

    // Necromancer fields
    public EnemyArchetype Archetype { get; private set; } = EnemyArchetype.StandardMelee;
    private float summonTimer = 4.0f;
    private int summonedCount = 0;
    private const int MAX_NECRO_SUMMONS = 3;

    public int maxHP;
    public int currentHP;
    public int attackDamage = 12;
    public float moveSpeed = 1.5f;
    public float attackSpeed = 0.8f;
    public float attackRange = 1.0f;

    public bool IsAlive => currentHP > 0;
    public int RoomIndex => roomIndex;
    public bool IsElite { get; private set; }

    public void ApplyModifiers(float hpMult, float dmgMult, bool elite = false)
    {
        IsElite = elite;
        maxHP = Mathf.Max(1, Mathf.RoundToInt(maxHP * hpMult));
        currentHP = maxHP;
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * dmgMult));

        if (elite)
        {
            transform.localScale = Vector3.one * 1.55f;
            if (sr != null)
            {
                sr.color = new Color(1f, 0.65f, 0.75f, 1f); // Elite champion crimson tint
            }
        }
    }

    public void Init(bool boss, ICombatController ctrl, int roomIdx = 0, EnemyArchetype archetype = EnemyArchetype.StandardMelee)
    {
        isBoss = boss;
        combat = ctrl;
        roomIndex = roomIdx;
        Archetype = boss ? EnemyArchetype.Boss : archetype;

        if (boss)
        {
            maxHP = 500;
            attackDamage = 25;
            moveSpeed = 0.8f;
            attackRange = 1.8f;
        }
        else if (Archetype == EnemyArchetype.Necromancer)
        {
            maxHP = 85;
            attackDamage = 10;
            moveSpeed = 1.4f;
            attackRange = 4.2f;
        }
        else
        {
            maxHP = Random.Range(40, 80);
            attackDamage = Random.Range(8, 15);
            moveSpeed = Random.Range(1.0f, 2.0f);
        }

        currentHP = maxHP;
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        // Setup animated sprite
        animator = gameObject.AddComponent<CharacterAnimator>();
        string spritePrefix = boss ?
            SpriteMapping.GetBossPrefix() :
            (Archetype == EnemyArchetype.Necromancer ? "necromancer" : SpriteMapping.GetRandomEnemyPrefix());

        animator.SetupEnemy(spritePrefix);

        if (boss)
        {
            transform.localScale = Vector3.one * 2f;
        }
        else if (Archetype == EnemyArchetype.Necromancer)
        {
            transform.localScale = Vector3.one * 1.35f;
        }
        else
        {
            transform.localScale = Vector3.one * 1.2f;
        }

        // Face left (toward heroes)
        animator.SetFacing(false);
    }

    void LateUpdate()
    {
        if (sr != null)
        {
            sr.sortingOrder = 100 - Mathf.RoundToInt(transform.position.y * 10);
        }
    }

    void Update()
    {
        if (!IsAlive || combat == null || combat.IsPaused || isTelegraphing) return;
        if (!combat.IsInCombatPhase) return;

        var target = FindNearestHero();
        if (target == null)
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            return;
        }

        if (Archetype == EnemyArchetype.Necromancer)
        {
            HandleNecromancerCombat(target);
            ClampToRoomBounds();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.transform.position);

        if (dist > attackRange)
        {
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(dir.x >= 0);
        }
        else
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / attackSpeed;

                if (isBoss)
                {
                    bossAttackCounter++;
                    if (bossAttackCounter % 3 == 0)
                    {
                        StartCoroutine(BossHeavySlamTelegraph(target));
                        return;
                    }
                }

                target.TakeDamage(attackDamage);
                StartCoroutine(AttackLunge(target.transform.position));
            CombatCameraController.Instance?.AddTrauma(0.55f);
            }
        }

        // Keep enemy inside the room boundaries
        ClampToRoomBounds();
    }

    // ---- Necromancer AI: Kites heroes and channels skeleton summoning spell ----
    void HandleNecromancerCombat(HeroCombatAgent target)
    {
        float dist = Vector2.Distance(transform.position, target.transform.position);
        Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        // 1. Kite away if heroes get uncomfortably close
        if (dist < 2.6f)
        {
            Vector2 awayDir = ((Vector2)transform.position - (Vector2)target.transform.position).normalized;
            transform.position += (Vector3)(awayDir * (moveSpeed * 1.15f) * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0);
        }
        else if (dist > 5.5f)
        {
            transform.position += (Vector3)(toTarget * moveSpeed * Time.deltaTime);
            animator.SetState(CharacterAnimator.AnimState.Run);
            animator.SetFacing(toTarget.x >= 0);
        }
        else
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
            animator.SetFacing(toTarget.x >= 0);
        }

        // 2. Summoning spell channel
        summonTimer -= Time.deltaTime;
        if (summonTimer <= 0f && summonedCount < MAX_NECRO_SUMMONS)
        {
            summonTimer = 6.0f;
            SpawnNecroMinion();
        }

        // 3. Shadow bolt projectile
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && dist <= attackRange)
        {
            attackTimer = 2.2f;
            CastShadowBolt(target);
        }
    }

    void SpawnNecroMinion()
    {
        if (transform.parent == null) return;
        var go = new GameObject($"NecroMinion_{summonedCount}");
        go.transform.SetParent(transform.parent, false);

        var spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 10;

        var minion = go.AddComponent<EnemyCombatAgent>();
        minion.Init(false, combat, roomIndex, EnemyArchetype.StandardMelee);
        minion.maxHP = 35;
        minion.currentHP = 35;
        minion.attackDamage = 9;

        Vector2 spawnPos = (Vector2)transform.position + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f));
        go.transform.position = spawnPos;

        combat?.EnemyAgents.Add(minion);
        CombatFeedback.Instance?.ShowText(transform.position, "RISE, MY MINION! ☠", new Color(0.7f, 0.25f, 0.95f), 3.2f);
        summonedCount++;
    }

    void CastShadowBolt(HeroCombatAgent target)
    {
        int dmg = attackDamage;
        CombatProjectile.Spawn(
            transform.position,
            target.transform,
            new Color(0.65f, 0.2f, 0.95f), // shadow violet bolt
            7.5f,
            () =>
            {
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(dmg);
                }
            },
            0.55f
        );
    }

    IEnumerator BossHeavySlamTelegraph(HeroCombatAgent target)
    {
        isTelegraphing = true;
        animator?.SetState(CharacterAnimator.AnimState.Idle);

        // Flash bright red warning and show telegraph text
        CombatFeedback.Instance?.ShowText(transform.position + Vector3.up * 1.4f, "⚠ HEAVY SLAM! ⚠", new Color(1f, 0.15f, 0.15f), 2.0f);
        if (sr != null) sr.color = new Color(1f, 0.25f, 0.25f);

        // Telegraph charge-up pause (0.7s)
        yield return new WaitForSeconds(0.7f);

        if (sr != null) sr.color = Color.white;
        isTelegraphing = false;

        if (IsAlive && target != null)
        {
            float slamRadius = 2.4f;
            int heavyDmg = Mathf.RoundToInt(attackDamage * 1.8f);

            StartCoroutine(AttackLunge(target.transform.position));
            CombatCameraController.Instance?.AddTrauma(0.55f);

            if (combat?.HeroAgents != null)
            {
                foreach (var h in combat.HeroAgents)
                {
                    if (h != null && h.IsAlive && Vector2.Distance(transform.position, h.transform.position) <= slamRadius)
                    {
                        h.TakeDamage(heavyDmg);
                        CombatFeedback.Instance?.ShowText(h.transform.position, $"-{heavyDmg} 💥", new Color(1f, 0.15f, 0.15f), 3.5f);
                    }
                }
            }
        }
    }

    HeroCombatAgent FindNearestHero()
    {
        HeroCombatAgent nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var h in combat.HeroAgents)
        {
            if (!h.IsAlive) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = h; }
        }
        return nearest;
    }

    IEnumerator AttackLunge(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        Vector3 lungePos = Vector3.Lerp(startPos, targetPos, 0.25f);

        float t = 0;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, lungePos, t / 0.08f);
            yield return null;
        }
        t = 0;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(lungePos, startPos, t / 0.08f);
            yield return null;
        }
    }

    public void TakeDamage(int dmg, HeroCombatAgent attacker = null)
    {
        currentHP -= dmg;
        if (animator) animator.FlashHit();

        if (isBoss && attacker != null)
            combat.DamageBoss(dmg);

        if (currentHP <= 0)
        {
            currentHP = 0;
            StartCoroutine(DieEffect());
        }
    }

    public void Kill()
    {
        currentHP = 0;
        gameObject.SetActive(false);
    }

    IEnumerator DieEffect()
    {
        // Flash red, fade out, shrink
        if (sr) sr.color = new Color(1f, 0.2f, 0.2f, 1f);

        float t = 0;
        Vector3 startScale = transform.localScale;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float progress = t / 0.4f;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            if (sr) sr.color = new Color(1f, 0.2f, 0.2f, 1f - progress);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Clamps enemy position to the current room's inner walkable area during combat.
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