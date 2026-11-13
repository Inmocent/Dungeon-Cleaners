using UnityEngine;

/// <summary>
/// Runtime state of a hero during a run. Wraps HeroData with live HP/Energy values.
/// Clones the HeroData ScriptableObject per instance so run upgrades never mutate project assets.
/// </summary>
[System.Serializable]
public class HeroInstance
{
    public HeroData data;
    public string displayName;
    public int currentHP;
    public int currentEnergy;
    public bool isAlive => currentHP > 0;
    public bool isFatigued => currentEnergy <= 0;

    // Position in the 3x3 grid (0-8), -1 = on bench
    public int gridIndex = -1;

    // Upgrade kits applied during the run
    public System.Collections.Generic.List<string> upgrades = new();

    public HeroInstance(HeroData heroData)
    {
        if (heroData != null)
        {
            data = Object.Instantiate(heroData);
            data.name = heroData.name;
            displayName = data.heroName;
            currentHP = data.maxHP;
            currentEnergy = data.maxEnergy;
        }
    }

    public void RestoreEnergy(float percent)
    {
        int amount = Mathf.RoundToInt(data.maxEnergy * percent);
        currentEnergy = Mathf.Min(currentEnergy + amount, data.maxEnergy);
    }

    public void TakeDamage(int dmg)
    {
        currentHP = Mathf.Max(0, currentHP - dmg);
    }

    public void UseEnergy(int cost)
    {
        currentEnergy = Mathf.Max(0, currentEnergy - cost);
    }
}