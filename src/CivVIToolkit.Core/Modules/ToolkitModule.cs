namespace CivVIToolkit.Core.Modules;

public enum ToolkitModuleId
{
    Overview,
    Trainer,
    Saves,
    MapsAndGameInfo,
    Mods,
    Launcher,
    Settings,
}

public sealed record ToolkitModule(
    ToolkitModuleId Id,
    string Title,
    string Description,
    bool IsAvailableInV01);

public static class ToolkitModuleCatalog
{
    public static IReadOnlyList<ToolkitModule> All { get; } =
    [
        new(ToolkitModuleId.Overview, "Overview", "Game detection, installation status and diagnostics.", true),
        new(ToolkitModuleId.Trainer, "Trainer", "Single-player trainer built on versioned signatures.", true),
        new(ToolkitModuleId.Saves, "Save Manager", "Browse, back up, tag and restore Civilization VI saves.", false),
        new(ToolkitModuleId.MapsAndGameInfo, "Maps & Game Info", "Inspect map, ruleset and game metadata.", false),
        new(ToolkitModuleId.Mods, "Mod Manager", "Discover, validate, enable and organize mods.", false),
        new(ToolkitModuleId.Launcher, "Quick Launch", "Launch the detected store build with a selected renderer.", false),
        new(ToolkitModuleId.Settings, "Settings", "Toolkit preferences and diagnostics.", true),
    ];
}
