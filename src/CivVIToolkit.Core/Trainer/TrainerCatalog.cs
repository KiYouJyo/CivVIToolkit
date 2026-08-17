namespace CivVIToolkit.Core.Trainer;

public static class TrainerCatalog
{
    public static IReadOnlyList<TrainerFeatureDefinition> All { get; } =
    [
        new("player.unlimited-gold", "Player", "Unlimited Gold", "Num 1", TrainerFeatureKind.Toggle),
        new("player.unlimited-faith", "Player", "Unlimited Faith", "Num 2", TrainerFeatureKind.Toggle),
        new("player.instant-research", "Player", "One-Turn Research", "Num 3", TrainerFeatureKind.Toggle),
        new("player.instant-civic", "Player", "One-Turn Civic", "Num 4", TrainerFeatureKind.Toggle),
        new("player.add-influence", "Player", "Add Influence", "Num 5", TrainerFeatureKind.ValueAction, 1000),
        new("unit.unlimited-movement", "Units", "Unlimited Movement", "Num 6", TrainerFeatureKind.Toggle),
        new("unit.unlimited-health", "Units", "Unlimited Health", "Num 7", TrainerFeatureKind.Toggle),
        new("city.instant-production", "Cities", "One-Turn Construction / Recruitment", "Num 8", TrainerFeatureKind.Toggle),
        new("player.unlimited-resources", "Player", "All Luxury / Strategic Resources", "Num 9", TrainerFeatureKind.Toggle),
        new("unit.always-upgrade", "Units", "Units Can Always Upgrade", "Num 0", TrainerFeatureKind.Toggle),
        new("city.max-population", "Cities", "Maximum City Population", "Num .", TrainerFeatureKind.Toggle),
        new("player.add-gold", "Player", "Add Gold", "PageUp", TrainerFeatureKind.ValueAction, 10000),
        new("unit.unlimited-builder-charges", "Units", "Unlimited Builder Charges", "PageDown", TrainerFeatureKind.Toggle),
        new("ai.zero-gold", "AI", "Set AI Gold to Zero", "Alt+Num 1", TrainerFeatureKind.Toggle),
        new("ai.zero-faith", "AI", "Set AI Faith to Zero", "Alt+Num 2", TrainerFeatureKind.Toggle),
        new("ai.block-research", "AI", "AI Cannot Complete Research", "Alt+Num 3", TrainerFeatureKind.Toggle),
        new("ai.block-civic", "AI", "AI Cannot Complete Civics", "Alt+Num 4", TrainerFeatureKind.Toggle),
        new("ai.zero-influence", "AI", "Set AI Influence to Zero", "Alt+Num 5", TrainerFeatureKind.Toggle),
        new("ai.block-movement", "AI", "AI Units Cannot Move", "Alt+Num 6", TrainerFeatureKind.Toggle),
        new("combat.one-hit-kill", "Combat", "One-Hit Kill", "Alt+Num 7", TrainerFeatureKind.Toggle),
        new("ai.block-production", "AI", "AI Cannot Complete Construction / Recruitment", "Alt+Num 8", TrainerFeatureKind.Toggle),
        new("ai.zero-resources", "AI", "Clear AI Luxury / Strategic Resources", "Alt+Num 9", TrainerFeatureKind.Toggle),
    ];
}
