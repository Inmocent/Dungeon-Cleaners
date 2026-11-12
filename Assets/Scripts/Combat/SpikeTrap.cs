using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Interactive floor spike trap.
/// Cycles through Dormant -> Warning rumble -> Lethal active burst.
/// Damages any unit (Hero or Enemy) standing within trigger radius during active burst.
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] frames = new Sprite[4];
    private float timer;
    private int state = 0; // 0 = dormant, 1 = warning, 2 = lethal active
    private float damageCooldown = 0f;

    private const int TRAP_DAMAGE = 10;
    private const float DAMAGE_RADIUS = 0.85f;

    public void Init(Vector2 position)
    {
        transform.position = position;
        transform.localScale = Vector3.one * 1.35f;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 4; // Above floor (0), below units (10+)

        // Preload 4 animation frames from Resources
        for (int i = 0; i < 4; i++)
        {
            frames[i] = Resources.Load<Sprite>($"Sprites/frames/floor_spikes_anim_f{i}");
        }

        if (frames[0] != null) sr.sprite = frames[0];

        // Randomize initial phase so multiple traps in a room don't fire simultaneously
        timer = Random.Range(0.5f, 2.5f);
        state = 0;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (damageCooldown > 0f) damageCooldown -= Time.deltaTime;

        if (state == 0) // DORMANT
        {
            if (timer <= 0f)
            {
                state = 1;
                timer = 0.6f; // Warning telegraph
                if (frames[1] != null) sr.sprite = frames[1];
                sr.color = new Color(1f, 0.75f, 0.3f); // Amber warning tint
            }
        }
        else if (state == 1) // WARNING
        {
            if (timer <= 0f)
            {
                state = 2;
                timer = 1.3f; // Lethal duration
                if (frames[3] != null) sr.sprite = frames[3];
                sr.color = new Color(1f, 0.35f, 0.35f); // Red hazard tint
                TriggerTrapDamage();
            }
        }
        else if (state == 2) // LETHAL ACTIVE
        {
            if (damageCooldown <= 0f)
            {
                TriggerTrapDamage();
                damageCooldown = 0.6f;
            }

            if (timer <= 0f)
            {
                state = 0;
                timer = Random.Range(2.2f, 3.2f);
                if (frames[0] != null) sr.sprite = frames[0];
                sr.color = Color.white;
            }
        }
    }

    void TriggerTrapDamage()
    {
        var ctrl = Object.FindAnyObjectByType<DungeonCombatController>();
        if (ctrl == null) return;

        // Check damage against heroes
        if (ctrl.HeroAgents != null)
        {
            foreach (var h in ctrl.HeroAgents)
            {
                if (h != null && h.IsAlive)
                {
                    if (Vector2.Distance(transform.position, h.transform.position) <= DAMAGE_RADIUS)
                    {
                        h.TakeDamage(TRAP_DAMAGE);
                        CombatCameraController.Instance?.AddTrauma(0.18f);
                        CombatFeedback.Instance?.ShowText(h.transform.position, $"SPIKE! -{TRAP_DAMAGE}", new Color(1f, 0.25f, 0.2f), 3f);
                    }
                }
            }
        }

        // Check damage against enemies in room
        if (ctrl.EnemyAgents != null)
        {
            foreach (var e in ctrl.EnemyAgents)
            {
                if (e != null && e.IsAlive)
                {
                    if (Vector2.Distance(transform.position, e.transform.position) <= DAMAGE_RADIUS)
                    {
                        e.TakeDamage(TRAP_DAMAGE);
                        CombatFeedback.Instance?.ShowText(e.transform.position, $"SPIKE! -{TRAP_DAMAGE}", new Color(1f, 0.45f, 0.2f), 3f);
                    }
                }
            }
        }
    }
}

