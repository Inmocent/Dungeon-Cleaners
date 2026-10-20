using UnityEngine;
using System.Collections;

/// <summary>
/// Simple enemy AI — moves toward the nearest hero and attacks.
/// Placeholder art: red circle.
/// </summary>
public class EnemyCombatAgent : MonoBehaviour
{
    private CombatController combat;
    private SpriteRenderer sr;
    private bool isBoss;
    private float attackTimer;

    public int maxHP;
    public int currentHP;
    public int attackDamage = 12;
    public float moveSpeed = 1.5f;
    public float attackSpeed = 0.8f;

    public bool IsAlive => currentHP > 0;

    public void Init(bool boss, CombatController ctrl)
    {
        isBoss = boss;
        combat = ctrl;

        if (boss) { maxHP = 500; attackDamage = 25; moveSpeed = 0.8f; }
        else      { maxHP = Random.Range(40, 80); attackDamage = Random.Range(8, 15); }

        currentHP = maxHP;
        sr = GetComponent<SpriteRenderer>();

        if (sr)
        {
            sr.color = boss ? new Color(0.5f, 0f, 0.5f) : new Color(0.8f, 0.1f, 0.1f);
            transform.localScale = boss ? Vector3.one * 2f : Vector3.one;
        }

        // Spawn on right half of arena
        float x = Random.Range(1.5f, 4f);
        float y = Random.Range(-2f, 2f);
        transform.localPosition = new Vector3(x, y, 0);
    }

    void Update()
    {
        if (!IsAlive || combat.IsPaused) return;

        var target = FindNearestHero();
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.transform.position);
        float range = 1.2f;

        if (dist > range)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
        else
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = 1f / attackSpeed;
                target.TakeDamage(attackDamage);
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
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = h; }
        }
        return nearest;
    }

    public void TakeDamage(int dmg, HeroCombatAgent attacker = null)
    {
        currentHP -= dmg;

        // Flash on hit
        if (sr) StartCoroutine(FlashHit());

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

    IEnumerator FlashHit()
    {
        sr.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        sr.color = isBoss ? new Color(0.5f, 0f, 0.5f) : new Color(0.8f, 0.1f, 0.1f);
    }

    IEnumerator DieEffect()
    {
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, t / 0.3f);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}