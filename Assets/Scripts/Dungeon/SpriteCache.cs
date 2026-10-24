using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Caches sprites loaded from the Assets/Sprites/frames/ folder.
/// Sprites must be placed in Assets/Resources/Sprites/frames/ for Resources.Load to work,
/// OR this class provides a fallback dictionary-based approach.
/// </summary>
public static class SpriteCache
{
    private static Dictionary<string, Sprite> cache = new();
    private static bool bulkLoaded = false;

    /// <summary>
    /// Load all sprites from Resources/Sprites/frames/ into cache.
    /// Call once at startup.
    /// </summary>
    public static void PreloadAll()
    {
        if (bulkLoaded) return;

        var allSprites = Resources.LoadAll<Sprite>("Sprites/frames");
        if (allSprites != null)
        {
            foreach (var s in allSprites)
            {
                if (!cache.ContainsKey(s.name))
                    cache[s.name] = s;
            }
        }
        bulkLoaded = true;
    }

    /// <summary>
    /// Load a single sprite by filename (without extension).
    /// </summary>
    public static Sprite LoadSprite(string spriteName)
    {
        if (!bulkLoaded) PreloadAll();

        if (cache.TryGetValue(spriteName, out var cached))
            return cached;

        // Attempt direct load
        var sprite = Resources.Load<Sprite>($"Sprites/frames/{spriteName}");
        if (sprite != null)
        {
            cache[spriteName] = sprite;
            return sprite;
        }

        return null;
    }

    /// <summary>
    /// Clear cache (call on scene transitions if needed to free memory).
    /// </summary>
    public static void Clear()
    {
        cache.Clear();
        bulkLoaded = false;
    }
}
