using UltimatePoKeSync.App.Services;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// What the window remembers between runs, and what it refuses to remember. See D-038.
/// </summary>
public sealed class AppSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"upks-settings-{Guid.NewGuid():N}");

    private string Path_ => Path.Combine(_directory, "settings.json");

    [Fact]
    public void WhatIsSavedIsWhatComesBack()
    {
        new AppSettings
        {
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowX = 120,
            WindowY = 60,
            CompetitiveProfile = true,
        }.Save(Path_);

        AppSettings read = AppSettings.Load(Path_);

        Assert.Equal(1400, read.WindowWidth);
        Assert.Equal(900, read.WindowHeight);
        Assert.Equal(120, read.WindowX);
        Assert.Equal(60, read.WindowY);
        Assert.True(read.CompetitiveProfile);
        Assert.True(read.HasUsableSize);
        Assert.True(read.HasPosition);
    }

    [Fact]
    public void NoFileMeansDefaultsRatherThanAFailure()
    {
        AppSettings settings = AppSettings.Load(Path_);

        Assert.False(settings.HasUsableSize);
        Assert.False(settings.HasPosition);
        Assert.False(settings.CompetitiveProfile);
    }

    /// <summary>Nothing here is worth a crash on startup.</summary>
    [Fact]
    public void ARuinedFileMeansDefaultsRatherThanAFailure()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path_, "{ this is not json");

        AppSettings settings = AppSettings.Load(Path_);

        Assert.Null(settings.WindowWidth);
        Assert.False(settings.HasUsableSize);
    }

    /// <summary>
    /// A window saved at 40×20 — by a crash, or a hand-edited file — must not reopen at
    /// 40×20, because there is no way to get out of it.
    /// </summary>
    [Fact]
    public void ASizeTooSmallToUseIsNotRestored()
    {
        new AppSettings { WindowWidth = 40, WindowHeight = 20 }.Save(Path_);

        Assert.False(AppSettings.Load(Path_).HasUsableSize);
    }

    [Fact]
    public void AMaximisedWindowStoresNoSizeToGoBackTo()
    {
        new AppSettings { WindowMaximised = true }.Save(Path_);

        AppSettings read = AppSettings.Load(Path_);

        Assert.True(read.WindowMaximised);
        Assert.False(read.HasUsableSize);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
