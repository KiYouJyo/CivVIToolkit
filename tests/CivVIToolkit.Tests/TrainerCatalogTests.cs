using CivVIToolkit.Core.Trainer;

namespace CivVIToolkit.Tests;

public sealed class TrainerCatalogTests
{
    [Fact]
    public void ClassicProfileContainsTwentyTwoFeatures()
    {
        Assert.Equal(22, TrainerCatalog.All.Count);
    }

    [Fact]
    public void FeatureIdsAndShortcutsAreUnique()
    {
        Assert.Equal(TrainerCatalog.All.Count, TrainerCatalog.All.Select(feature => feature.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(TrainerCatalog.All.Count, TrainerCatalog.All.Select(feature => feature.Shortcut).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ValueActionsHaveDefaultValues()
    {
        Assert.All(
            TrainerCatalog.All.Where(feature => feature.Kind == TrainerFeatureKind.ValueAction),
            feature => Assert.NotNull(feature.DefaultValue));
    }
}
