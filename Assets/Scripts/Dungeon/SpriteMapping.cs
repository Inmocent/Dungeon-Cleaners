using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines which character sprite prefix maps to each hero or enemy type.
/// </summary>
public static class SpriteMapping
{
    // Hero sprite prefixes (used by CharacterAnimator)
    // These map to file patterns like: knight_m_idle_anim_f0, knight_m_run_anim_f0, etc.
    private static readonly Dictionary<string, string> heroSpriteMap = new()
    {
        { "Knight Alden", "knight_m" },
        { "Knight Cedric", "knight_f" },
        { "Mage Rowan", "wizzard_m" },
        { "Mage Vesper", "wizzard_f" },
        { "Healer Lynne", "elf_f" },
        // Mercenary & Special Operatives
        { "Doc Vance", "doc" },
        { "Sister Teresa", "angel" },
        { "Ronin Silas", "dwarf_m" },
        { "Thora Ironclad", "dwarf_f" },
        { "Pyromancer Jax", "wogol" },
        { "Gladiator Brutus", "orc_warrior" },
        { "Vespera Void", "lizard_f" },
        { "Monk Cedric", "doc" },
        { "Jack the Brawler", "pumpkin_dude" },
        { "Kaelen Shadowstep", "elf_m" },
        { "Gorzog Hex-Weaver", "orc_shaman" },
        { "Saurian Skirmisher", "lizard_m" },
        { "Masked Marauder", "masked_orc" }
    };

    // Enemy types available for spawning
    public static readonly string[] regularEnemyPrefixes = { "skelet", "goblin" };
    public static readonly string[] bossEnemyPrefixes = { "big_demon" };

    /// <summary>
    /// Register or override a hero's sprite prefix at runtime.
    /// </summary>
    public static void RegisterHero(string heroName, string spritePrefix)
    {
        if (!string.IsNullOrEmpty(heroName) && !string.IsNullOrEmpty(spritePrefix))
        {
            heroSpriteMap[heroName] = spritePrefix;
        }
    }

    /// <summary>
    /// Get the sprite prefix for a hero by their name and optional explicit prefix.
    /// Returns a fallback if not found.
    /// </summary>
    public static string GetHeroSpritePrefix(string heroName, string explicitPrefix = null)
    {
        if (!string.IsNullOrEmpty(explicitPrefix))
            return explicitPrefix;

        if (!string.IsNullOrEmpty(heroName) && heroSpriteMap.TryGetValue(heroName, out var prefix))
            return prefix;

        // Fallback based on class
        return "knight_m";
    }

    /// <summary>
    /// Get a random regular enemy sprite prefix.
    /// </summary>
    public static string GetRandomEnemyPrefix()
    {
        return regularEnemyPrefixes[Random.Range(0, regularEnemyPrefixes.Length)];
    }

    /// <summary>
    /// Get the boss sprite prefix.
    /// </summary>
    public static string GetBossPrefix()
    {
        return bossEnemyPrefixes[0];
    }
}
