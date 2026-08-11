using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.App.ViewModels;
using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// What a party slot holding an egg is allowed to show. The species is in the bytes and
/// the game hides it on purpose, so the window hides it too. See D-036.
/// </summary>
public sealed class EggSlotTests
{
    [Fact]
    public void TheTileSaysEggAndNotWhatIsInside()
    {
        PokemonSlotViewModel slot = EggSlot();

        Assert.Equal("Egg", slot.SpeciesName);
        Assert.Equal("EGG", slot.Initials);
        Assert.DoesNotContain("TORCHIC", slot.SpeciesName, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(slot.NicknameText);
        Assert.False(slot.HasNickname);
    }

    [Fact]
    public void NoTypeIsClaimedForSomethingThatCannotBattle()
    {
        PokemonSlotViewModel slot = EggSlot();

        Assert.False(slot.ShowsBattleData);
        Assert.True(slot.IsEgg);
        Assert.Empty(slot.TypeChips);
        Assert.Empty(slot.Weaknesses);
        Assert.Empty(slot.Resistances);
        Assert.Empty(slot.Immunities);
        Assert.DoesNotContain("Fire", slot.TypeText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The one number an egg does have. See D-036.</summary>
    [Fact]
    public void WhatIsShownInsteadOfStatsIsHowFarThereIsLeftToWalk()
    {
        Assert.Equal("About 2,560 steps to go (10 cycles).", EggSlot().EggProgressText);
    }

    private static PokemonSlotViewModel EggSlot()
    {
        PokemonSnapshot egg = Egg();
        var party = new PartySnapshot(
            new GameIdentity("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3),
            [egg],
            DateTimeOffset.UnixEpoch,
            1,
            []);

        return new PokemonSlotViewModel(egg, new TeamAnalyzer().Analyze(party), null);
    }

    private static PokemonSnapshot Egg() => new()
    {
        SlotIndex = 0,
        SpeciesId = 255,
        SpeciesName = "TORCHIC",
        Nickname = "EGG",
        Level = 5,
        PrimaryType = PokemonType.Fire,
        SecondaryType = PokemonType.None,
        BaseStats = new StatBlock(45, 60, 40, 70, 50, 45),
        IndividualValues = new StatBlock(31, 31, 31, 31, 31, 31),
        EffortValues = new StatBlock(0, 0, 0, 0, 0, 0),
        CurrentStats = new StatBlock(20, 12, 10, 13, 11, 10),
        NatureId = 0,
        NatureName = "Hardy",
        AbilityId = 0,
        AbilityName = "-",
        HeldItemId = 0,
        HeldItemName = "-",
        Moves = [],
        IsEgg = true,
        IsShiny = false,
        PersonalityValue = 1,
        CurrentHp = 20,
        Status = StatusCondition.None,
        Friendship = 10,
        Experience = 0,
    };
}
