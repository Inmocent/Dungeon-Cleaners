using UnityEngine;

public enum RewardType
{
    Rest,           // Open rest modal - pick 2 heroes, restore 50% energy
    UpgradeKit,     // Stat/ability upgrade
    WeaponBoost,    // Themed weapon style with stat buffs & trade-offs
    GoldBonus,      // Extra gold
}

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
    public float attackMult = 1.0f;
    public float speedMult = 1.0f;
    public float attackSpeedMult = 1.0f;

    public HeroClass targetClass; // which class benefits
    public string weaponStyle = "";
    public string tradeOffText = "";
}