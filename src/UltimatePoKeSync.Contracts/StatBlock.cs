namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Sestina di valori indicizzata per statistica. Usata per stat base, IV, EV e stat calcolate.
/// </summary>
public readonly record struct StatBlock(int Hp, int Attack, int Defense, int SpecialAttack, int SpecialDefense, int Speed)
{
    public int Total => Hp + Attack + Defense + SpecialAttack + SpecialDefense + Speed;

    public int this[Stat stat] => stat switch
    {
        Stat.Hp => Hp,
        Stat.Attack => Attack,
        Stat.Defense => Defense,
        Stat.SpecialAttack => SpecialAttack,
        Stat.SpecialDefense => SpecialDefense,
        Stat.Speed => Speed,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };
}

public enum Stat
{
    Hp = 0,
    Attack = 1,
    Defense = 2,
    SpecialAttack = 3,
    SpecialDefense = 4,
    Speed = 5,
}
