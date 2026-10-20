using UnityEngine;

[CreateAssetMenu(fileName = "New Reward", menuName = "Dungeon Cleaners/Reward Data")]
public class RewardData : ScriptableObject
{
    public string rewardName;
    public RewardType rewardType;
    [TextArea]
    public string description;

    // Stat modifier values
    public int hpBonus;
    public int energyBonus;
    public int attackBonus;
    public HeroClass targetClass; // which class benefits
}

public enum RewardType
{
    Rest,           // Open rest modal - pick 2 heroes, restore 50% energy
    UpgradeKit,     // Stat/ability upgrade
    GoldBonus,      // Extra gold
}