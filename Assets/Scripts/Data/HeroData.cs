using UnityEngine;

[CreateAssetMenu(fileName = "New Hero", menuName = "Dungeon Cleaners/Hero Data")]
public class HeroData : ScriptableObject
{
    public string heroName;
    public HeroClass heroClass;
    public int maxHP;
    public int maxEnergy;
    public int attackDamage;
    public float attackSpeed;
    public float moveSpeed;
    public Color classColor;

    [TextArea]
    public string description;
}

public enum HeroClass
{
    Knight,
    Mage,
    Healer
}