using UnityEngine;
using System.Collections;

/// <summary>
/// Healer AI — attacks enemies when possible, periodically heals the lowest HP ally.
/// </summary>
public class HealerCombatAgent : MonoBehaviour
{
    private HeroCombatAgent baseAgent;
    private CombatController combat;
    private float healTimer = 3f;
    private const float HEAL_INTERVAL = 4f;
    private const int HEAL_AMOUNT = 20;

    void Awake()
    {
        baseAgent = GetComponent<HeroCombatAgent>();
    }

    public void Init(CombatController ctrl)
    {
        combat = ctrl;
    }

    void Update()
    {
        if (!baseAgent.IsAlive || combat.IsPaused) return;

        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            healTimer = HEAL_INTERVAL;
            HealLowestAlly();
        }
    }

    void HealLowestAlly()
    {
        HeroCombatAgent target = null;
        int lowestHP = int.MaxValue;

        // Check for priority target first
        int priorityIdx = GameRunData.Instance?.healerPriorityIndex ?? -1;
        var squad = GameRunData.Instance?.squad;

        if (squad != null && priorityIdx >= 0 && priorityIdx < squad.Count)
        {
            var priHero = squad[priorityIdx];
            foreach (var a in combat.HeroAgents)
            {
                if (a.heroInstance == priHero && a.IsAlive)
                {
                    target = a;
                    break;
                }
            }
        }

        // Fallback: lowest HP living hero
        if (target == null)
        {
            foreach (var a in combat.HeroAgents)
            {
                if (!a.IsAlive) continue;
                if (a.heroInstance.currentHP < lowestHP)
                {
                    lowestHP = a.heroInstance.currentHP;
                    target = a;
                }
            }
        }

        if (target != null)
            target.ReceiveHeal(HEAL_AMOUNT);
    }
}