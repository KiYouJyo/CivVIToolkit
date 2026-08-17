using CivVIToolkit.Core.Hotkeys;
using CivVIToolkit.Core.Modules;
using CivVIToolkit.Platform.Windows.Memory;

namespace CivVIToolkit.Tests;

public sealed class ArchitectureContractTests
{
    [Fact]
    public void FutureToolkitModulesAreReserved()
    {
        var ids = ToolkitModuleCatalog.All.Select(module => module.Id).ToHashSet();
        Assert.Contains(ToolkitModuleId.Trainer, ids);
        Assert.Contains(ToolkitModuleId.Saves, ids);
        Assert.Contains(ToolkitModuleId.MapsAndGameInfo, ids);
        Assert.Contains(ToolkitModuleId.Mods, ids);
        Assert.Contains(ToolkitModuleId.Launcher, ids);
    }

    [Theory]
    [InlineData("Num 1", ShortcutModifiers.None, "Num 1")]
    [InlineData("Alt+Num 1", ShortcutModifiers.Alt, "Num 1")]
    [InlineData("Ctrl+Shift+F8", ShortcutModifiers.Control | ShortcutModifiers.Shift, "F8")]
    public void ShortcutParserUnderstandsTrainerGestures(string text, ShortcutModifiers modifiers, string key)
    {
        var gesture = ShortcutGesture.Parse(text);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
    }

    [Fact]
    public void AobPatternParsesWildcards()
    {
        var pattern = AobPattern.Parse("48 8B ?? 01 ? FF");
        Assert.Equal(6, pattern.Length);
        Assert.True(pattern.Wildcards[2]);
        Assert.True(pattern.Wildcards[4]);
        Assert.False(pattern.Wildcards[0]);
        Assert.Equal(0x48, pattern.Bytes[0]);
    }
}
