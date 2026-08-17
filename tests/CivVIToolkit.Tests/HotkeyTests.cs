using CivVIToolkit.Platform.Windows.Hotkeys;

namespace CivVIToolkit.Tests;

public sealed class HotkeyTests
{
    [Theory]
    [InlineData("Num 0", 0x60u)]
    [InlineData("Num 9", 0x69u)]
    [InlineData("Num .", 0x6Eu)]
    [InlineData("PageUp", 0x21u)]
    [InlineData("PageDown", 0x22u)]
    [InlineData("F12", 0x7Bu)]
    public void VirtualKeyResolverMapsTrainerKeys(string key, uint expected)
    {
        Assert.Equal(expected, VirtualKeyResolver.Resolve(key));
    }
}
