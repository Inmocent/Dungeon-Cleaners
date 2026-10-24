using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy AI with animated sprites.
/// Moves toward nearest hero and attacks. Spawns within a specific dungeon room.
/// Supports regular enemies (skeletons, goblins) and bosses.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyCombatAgent : MonoBehaviour
{
    private ICombatController combat;
    private CharacterAnimator animator;
    private SpriteRenderer sr;
    private bool isBoss;
    private float attackTimer;
    private int roomIndex;  // which room this enemy belongs to

    public int maxHP;
    public int currentHP;
    public int attackDamage = 12;
    public float moveSpeed = 1.5f;
    public float attackSpeed = 0.8f;
    public float attackRange = 1.0f;

    public bool IsAlive => currentHP > 0;
    public int RoomIndex => roomIndex;

    public void Init(bool boss, ICombatController ctrl, int roomIdx = 0)
    {
        isBoss = boss;
        combat = ctrl;
        roomIndex = roomIdx;

        if (boss)
        {
            maxHP = 500;
            attackDamage = 25;
            moveSpeed = 0.8f;
            attackRange = 1.5f;
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
            SpriteMapping.GetRandomEnemyPrefix();

        animator.SetupEnemy(spritePrefix);

        if (boss)
        {
            transform.localScale = Vector3.one * 2f;
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
        if (!IsAlive || combat == null || combat.IsPaused) return;
        if (!combat.IsInCombatPhase) return;

        var target = FindNearestHero();
        if (target == null)
        {
            animator.SetState(CharacterAnimator.AnimState.Idle);
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
                target.TakeDamage(attackDamage);

                // Attack lunge
                StartCoroutine(AttackLunge(target.transform.position));
            }
        }

        // Keep enemy inside the room boundaries
        ClampToRoomBounds();
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