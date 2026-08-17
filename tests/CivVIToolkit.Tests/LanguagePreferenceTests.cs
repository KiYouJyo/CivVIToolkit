using CivVIToolkit.Core.Localization;

namespace CivVIToolkit.Tests;

public sealed class LanguagePreferenceTests
{
    [Theory]
    [InlineData("zh-cn", "zh-CN")]
    [InlineData("JA-jp", "ja-JP")]
    [InlineData("en-US", "en-US")]
    [InlineData("unknown", "system")]
    [InlineData("", "system")]
    public void Normalize_returns_canonical_values(string input, string expected)
    {
        Assert.Equal(expected, LanguagePreference.Normalize(input));
    }

    [Fact]
    public void System_language_maps_supported_families()
    {
        Assert.Equal("ja-JP", LanguagePreference.ResolveSystemLanguage(["ja-JP"]));
        Assert.Equal("en-US", LanguagePreference.ResolveSystemLanguage(["en-GB"]));
        Assert.Equal("zh-CN", LanguagePreference.ResolveSystemLanguage(["zh-Hans-CN"]));
    }

    [Fact]
    public void Unsupported_system_language_falls_back_to_chinese()
    {
        Assert.Equal("zh-CN", LanguagePreference.ResolveSystemLanguage(["fr-FR"]));
    }

    [Fact]
    public void Explicit_override_wins_over_system_language()
    {
        Assert.Equal("en-US", LanguagePreference.ResolveEffectiveLanguage("en-US", ["ja-JP"]));
    }
}
