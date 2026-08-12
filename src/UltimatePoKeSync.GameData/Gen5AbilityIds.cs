namespace UltimatePoKeSync.GameData;

/// <summary>
/// Gen 5 ability identifiers whose effects alter aggregate type effectiveness.
/// </summary>
/// <remarks>
/// Ability numbers do not change between generations, so the Gen 3 ones keep their values
/// here. What changed is which abilities exist and what they do: Lightning Rod and Storm
/// Drain only became immunities in Gen 5, having merely redirected the move before.
/// </remarks>
public static class Gen5AbilityIds
{
    public const int VoltAbsorb = 10;
    public const int WaterAbsorb = 11;
    public const int FlashFire = 18;
    public const int WonderGuard = 25;
    public const int Levitate = 26;
    public const int LightningRod = 31;
    public const int ThickFat = 47;
    public const int MotorDrive = 78;
    public const int Heatproof = 85;
    public const int DrySkin = 87;
    public const int Filter = 111;
    public const int StormDrain = 114;
    public const int SolidRock = 116;
    public const int SapSipper = 157;
}
