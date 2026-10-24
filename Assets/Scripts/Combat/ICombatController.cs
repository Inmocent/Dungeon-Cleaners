using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>
    /// Returns the walkable inner bounds of the current combat room (with wall margin).
    /// Returns null if no room is active (e.g. Arena mode).
    /// </summary>
    Rect? GetCurrentRoomBounds();
}
