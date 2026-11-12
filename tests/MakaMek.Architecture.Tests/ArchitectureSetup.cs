using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace Sanet.MakaMek.Architecture.Tests;

/// <summary>
/// Loads the production assemblies once per test run and exposes the layers used by the rules.
/// </summary>
internal static class ArchitectureSetup
{
    public static readonly Assembly CoreAssembly = typeof(IGameCommand).Assembly;
    public static readonly Assembly MapAssembly = typeof(Hex).Assembly;
    public static readonly Assembly ServicesAssembly = typeof(IClipboardService).Assembly;
    public static readonly Assembly LocalizationAssembly = typeof(LocalizationResourcesAnchor).Assembly;
    public static readonly Assembly AssetsAssembly = typeof(BucketDefaults).Assembly;
    public static readonly Assembly BotsAssembly = typeof(BotSettings).Assembly;
    public static readonly Assembly PresentationAssembly = typeof(IUiState).Assembly;
    public static readonly Assembly MvvmAssembly = typeof(BindableBase).Assembly;
    public static readonly Assembly AvaloniaAssembly = typeof(Avalonia.App).Assembly;
    public static readonly Assembly AvaloniaControlsAssembly =
        typeof(Avalonia.Controls.HexMap).Assembly;
    public static readonly Assembly ServicesAvaloniaAssembly =
        typeof(global::Sanet.MakaMek.Services.Avalonia.AvaloniaClipboardService).Assembly;

    public static readonly Assembly AvaloniaBaseAssembly = typeof(global::Avalonia.AvaloniaObject).Assembly;
    public static readonly Assembly AvaloniaControlsFrameworkAssembly = typeof(global::Avalonia.Controls.Control).Assembly;

    public static readonly ArchModel Architecture = new ArchLoader()
        .LoadAssemblies(
            CoreAssembly,
            MapAssembly,
            ServicesAssembly,
            LocalizationAssembly,
            AssetsAssembly,
            BotsAssembly,
            PresentationAssembly,
            MvvmAssembly,
            AvaloniaAssembly,
            AvaloniaControlsAssembly,
            ServicesAvaloniaAssembly,
            AvaloniaBaseAssembly,
            AvaloniaControlsFrameworkAssembly)
        .Build();

    public static readonly IObjectProvider<IType> CoreLayer =
        Types().That().ResideInAssembly(CoreAssembly).As("Core");

    public static readonly IObjectProvider<IType> MapLayer =
        Types().That().ResideInAssembly(MapAssembly).As("Map");

    public static readonly IObjectProvider<IType> ServicesLayer =
        Types().That().ResideInAssembly(ServicesAssembly).As("Services");

    public static readonly IObjectProvider<IType> LocalizationLayer =
        Types().That().ResideInAssembly(LocalizationAssembly).As("Localization");

    public static readonly IObjectProvider<IType> PresentationLayer =
        Types().That().ResideInAssembly(PresentationAssembly).As("Presentation");

    /// <summary>Every UI-facing type: Presentation and all Avalonia-based assemblies plus Avalonia itself.</summary>
    public static readonly IObjectProvider<IType> UiLayer = Types().That()
        .ResideInAssembly(PresentationAssembly)
        .Or().ResideInAssembly(AvaloniaAssembly)
        .Or().ResideInAssembly(AvaloniaControlsAssembly)
        .Or().ResideInAssembly(ServicesAvaloniaAssembly)
        .Or().ResideInAssembly(AvaloniaBaseAssembly)
        .Or().ResideInAssembly(AvaloniaControlsFrameworkAssembly)
        .As("UI layer (Presentation + Avalonia)");

    public static readonly IObjectProvider<IType> AvaloniaLayer = Types().That()
        .ResideInAssembly(AvaloniaAssembly)
        .Or().ResideInAssembly(AvaloniaControlsAssembly)
        .Or().ResideInAssembly(ServicesAvaloniaAssembly)
        .Or().ResideInAssembly(AvaloniaBaseAssembly)
        .Or().ResideInAssembly(AvaloniaControlsFrameworkAssembly)
        .As("Avalonia layer");
}

