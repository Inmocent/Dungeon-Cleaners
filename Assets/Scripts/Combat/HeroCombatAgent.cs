using UnityEngine;
using System.Collections;

/// <summary>
/// Simple combat agent for a hero — moves toward nearest enemy, attacks it.
/// Uses colored circle sprites as placeholder art.
/// </summary>
public class HeroCombatAgent : MonoBehaviour
{
    public HeroInstance heroInstance { get; private set; }

    private CombatController combat;
    private SpriteRenderer sr;
    private float attackTimer;
    private bool dodgeRolling;
    private int gridIndex;

    public bool IsAlive => heroInstance != null && heroInstance.isAlive;

    public void Init(HeroInstance hero, int slotIdx, CombatController ctrl)
    {
        heroInstance = hero;
        combat = ctrl;
        gridIndex = slotIdx;

        sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = hero.data.classColor;

        // Position in a rough grid layout inside arena
        SetPositionFromGrid(slotIdx);
    }

    void SetPositionFromGrid(int idx)
    {
        int col = idx % 3; // 0=left, 1=mid, 2=right
        int row = idx / 3; // 0=top, 1=mid, 2=bottom

        // Left half of screen for heroes (x: -4 to -2)
        float x = -4f + col * 1f;
        float y = 2f - row * 2f;
        transform.localPosition = new Vector3(x, y, 0);
    }

    void Update()
    {
        if (!IsAlive || combat.IsPaused || dodgeRolling) return;

        var target = FindNearestEnemy();
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.transform.position);
        float attackRange = 1.2f;

        if (dist > attackRange)
        {
            // Move toward enemy
            Vector3 dir = (target.transform.position - transform.position).normalized;
            transform.position += dir * heroInstance.data.moveSpeed * Time.deltaTime;
        }
        else
        {
            // Attack
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / heroInstance.data.attackSpeed;
                Attack(target);
            }
        }
    }

    EnemyCombatAgent FindNearestEnemy()
    {
        EnemyCombatAgent nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var e in combat.EnemyAgents)
        {
            if (!e.IsAlive) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = e; }
        }
        return nearest;
    }

    void Attack(EnemyCombatAgent target)
    {
        int dmg = heroInstance.data.attackDamage;
        target.TakeDamage(dmg, this);

        // Energy drain on attack
        heroInstance.UseEnergy(5);
    }

    public void TakeDamage(int dmg)
    {
        heroInstance.TakeDamage(dmg);
        if (!IsAlive)
        {
            // Flash red and shrink to indicate death
            if (sr) sr.color = Color.red;
            StartCoroutine(DieEffect());
        }
    }

    IEnumerator DieEffect()
    {
        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t / 0.5f);
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

        float t = 0;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(startPos, rollPos, t / 0.4f);
            yield return null;
        }
        dodgeRolling = false;
    }

    // Called by healer
    public void ReceiveHeal(int amount)
    {
        heroInstance.currentHP = Mathf.Min(heroInstance.currentHP + amount, heroInstance.data.maxHP);
    }
}