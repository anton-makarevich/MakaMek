namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// One action offered by a UI state. Actions are enabled by default; a state may keep an action
/// visible but disabled and explain why, for example a Push with no target in front.
/// </summary>
public record StateAction(
    string Label,
    bool IsVisible,
    Action OnExecute,
    bool IsEnabled = true,
    string? DisabledReason = null);
