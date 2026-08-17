using CivVIToolkit.Core.Trainer;

namespace CivVIToolkit.Tests;

public sealed class TrainerCatalogTests
{
    [Fact]
    public void ClassicTrainerCatalogContainsExactly22Features()
    {
        Assert.Equal(22, TrainerCatalog.All.Count);
        Assert.Equal(22, TrainerCatalog.All.Select(feature => feature.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(22, TrainerCatalog.All.Select(feature => feature.Shortcut).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ClassicTrainerHotkeysRemainStable()
    {
        var shortcuts = TrainerCatalog.All.ToDictionary(feature => feature.Id, feature => feature.Shortcut, StringComparer.Ordinal);

        Assert.Equal("Num 1", shortcuts["player.unlimited-gold"]);
        Assert.Equal("Num 2", shortcuts["player.unlimited-faith"]);
        Assert.Equal("Num 3", shortcuts["player.instant-research"]);
        Assert.Equal("Num 4", shortcuts["player.instant-civic"]);
        Assert.Equal("Num 5", shortcuts["player.add-influence"]);
        Assert.Equal("Num 6", shortcuts["unit.unlimited-movement"]);
        Assert.Equal("Num 7", shortcuts["unit.unlimited-health"]);
        Assert.Equal("Num 8", shortcuts["city.instant-production"]);
        Assert.Equal("Num 9", shortcuts["player.unlimited-resources"]);
        Assert.Equal("Num 0", shortcuts["unit.always-upgrade"]);
        Assert.Equal("Num .", shortcuts["city.max-population"]);
        Assert.Equal("PageUp", shortcuts["player.add-gold"]);
        Assert.Equal("PageDown", shortcuts["unit.unlimited-builder-charges"]);
        Assert.Equal("Alt+Num 1", shortcuts["ai.zero-gold"]);
        Assert.Equal("Alt+Num 9", shortcuts["ai.zero-resources"]);
    }

    [Fact]
    public void ValueActionsKeepExpectedDefaultAmounts()
    {
        var addInfluence = TrainerCatalog.All.Single(feature => feature.Id == "player.add-influence");
        var addGold = TrainerCatalog.All.Single(feature => feature.Id == "player.add-gold");

        Assert.Equal(TrainerFeatureKind.ValueAction, addInfluence.Kind);
        Assert.Equal(1_000, addInfluence.DefaultValue);
        Assert.Equal(TrainerFeatureKind.ValueAction, addGold.Kind);
        Assert.Equal(10_000, addGold.DefaultValue);
        Assert.Equal(20, TrainerCatalog.All.Count(feature => feature.Kind == TrainerFeatureKind.Toggle));
    }
}
