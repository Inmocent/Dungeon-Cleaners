using System.Collections.Generic;

/// <summary>
/// Common interface for combat controllers (both Arena and procedural Dungeon modes).
/// </summary>
public interface ICombatController
{
    List<HeroCombatAgent> HeroAgents { get; }
    List<EnemyCombatAgent> EnemyAgents { get; }
    bool IsPaused { get; }
    bool IsInCombatPhase { get; }
    void DamageBoss(int dmg);
    void RefreshPortraits();
}
