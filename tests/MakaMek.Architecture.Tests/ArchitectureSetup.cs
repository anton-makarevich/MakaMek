using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Mono.Cecil;
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

    public static readonly ArchModel Architecture = BuildArchitecture();

    /// <summary>
    /// Builds the architecture once per test run from copies of the production assemblies,
    /// because ArchUnitNET cannot read some compiler-emitted metadata (see
    /// <see cref="CopyForArchUnitNet"/>).
    /// </summary>
    private static ArchModel BuildArchitecture()
    {
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "makamek-architecture");

        var assemblies = new[]
        {
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
            AvaloniaControlsFrameworkAssembly
        };

        Directory.CreateDirectory(stagingDirectory);
        foreach (var assembly in assemblies)
        {
            CopyForArchUnitNet(assembly, stagingDirectory);
        }

        try
        {
            return new ArchLoader()
                .LoadFilteredDirectory(stagingDirectory, "*.dll")
                .Build();
        }
        finally
        {
            TryDeleteStagingDirectory(stagingDirectory);
        }
    }

    /// <summary>
    /// Best-effort clean-up: Mono.Cecil keeps the analysed modules memory-mapped until they are
    /// finalized, so deleting the staging directory can fail. A leftover directory is harmless -
    /// the next run overwrites its files.
    /// </summary>
    private static void TryDeleteStagingDirectory(string stagingDirectory)
    {
        try
        {
            Directory.Delete(stagingDirectory, true);
        }
        catch (IOException)
        {
            // Files are still memory-mapped; they are overwritten on the next run.
        }
    }

    /// <summary>
    /// Copies <paramref name="assembly"/> into the analysis staging directory. Avalonia 12.2's
    /// XAML compiler emits <c>CompiledAvaloniaXaml.XamlIlHelpers..cctor</c> (and a few sibling
    /// methods) with the compiler-controlled access flag (<c>MemberAccessMask == 0</c>). The CLR
    /// executes those methods, but ArchUnitNET's Mono.Cecil loader rejects them with
    /// "The method definition seems to have no visibility", so the flag is reset to
    /// <c>private</c> in the copy.
    /// </summary>
    private static void CopyForArchUnitNet(Assembly assembly, string stagingDirectory)
    {
        var destination = Path.Combine(stagingDirectory, Path.GetFileName(assembly.Location));

        using var module = ModuleDefinition.ReadModule(assembly.Location);
        var needsRepair = false;
        foreach (var method in module.GetTypes().SelectMany(type => type.Methods))
        {
            if ((method.Attributes & MethodAttributes.MemberAccessMask) != 0)
            {
                continue;
            }

            method.Attributes |= MethodAttributes.Private;
            needsRepair = true;
        }

        if (needsRepair)
        {
            module.Write(destination);
            return;
        }

        // Keep untouched assemblies byte-identical instead of round-tripping them through Cecil.
        File.Copy(assembly.Location, destination, true);
    }

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

