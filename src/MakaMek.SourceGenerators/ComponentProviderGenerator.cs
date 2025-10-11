using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Sanet.MakaMek.SourceGenerators;

[Generator]
public class ComponentProviderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations that could be Component-derived types
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateType(s),
                transform: static (ctx, _) => GetComponentInfo(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Combine with compilation to get semantic information
        var compilationAndTypes = context.CompilationProvider.Combine(typeDeclarations.Collect());

        // Generate the source
        context.RegisterSourceOutput(compilationAndTypes,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsCandidateType(SyntaxNode node)
    {
        // Look for class declarations that might inherit from Component
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    private static ComponentInfo? GetComponentInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Get the semantic model to check inheritance
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (typeSymbol is null || typeSymbol.IsAbstract)
            return null;

        // Only consider classes that might be related to Component
        var fullName = typeSymbol.ToDisplayString();
        if (fullName.Contains("Component") || fullName.Contains("Weapon") || 
            fullName.Contains("Actuator") || fullName.Contains("Gyro") ||
            fullName.Contains("Sensor") || fullName.Contains("Cockpit") ||
            fullName.Contains("LifeSupport") || fullName.Contains("HeatSink") ||
            fullName.Contains("JumpJet") || fullName.Contains("Engine") ||
            fullName.Contains("Masc"))
        {
            return new ComponentInfo(
                typeSymbol.Name,
                typeSymbol.ContainingNamespace.ToDisplayString(),
                typeSymbol.ToDisplayString(),
                typeSymbol);
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ComponentInfo> types, SourceProductionContext context)
    {
        try
        {
            // Find the Component base class
            var componentSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Models.Units.Components.Component");
            var weaponDefinitionSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Data.Units.Components.WeaponDefinition");

            if (componentSymbol is null)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyProvider();
                context.AddSource("ClassicBattletechComponentProvider.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Filter classes that inherit from Component
            var componentTypes = new List<ComponentInfo>();
            var weaponDefinitions = new Dictionary<string, IFieldSymbol>();

            foreach (var typeInfo in types)
            {
                if (InheritsFrom(typeInfo.Symbol, componentSymbol))
                {
                    componentTypes.Add(typeInfo);
                    
                    // Check for static Definition property of WeaponDefinition type
                    if (weaponDefinitionSymbol != null)
                    {
                        var definitionField = typeInfo.Symbol.GetMembers("Definition")
                            .OfType<IFieldSymbol>()
                            .FirstOrDefault(f => f.IsStatic && f.IsReadOnly);
                        
                        if (definitionField != null && 
                            SymbolEqualityComparer.Default.Equals(definitionField.Type, weaponDefinitionSymbol))
                        {
                            weaponDefinitions[typeInfo.Name] = definitionField;
                        }
                    }
                }
            }

            if (componentTypes.Count == 0)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyProvider();
                context.AddSource("ClassicBattletechComponentProvider.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Generate the source code
            var source = GenerateProvider(componentTypes, weaponDefinitions, compilation);
            context.AddSource("ClassicBattletechComponentProvider.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("CPG001", "Source generator error",
                    $"Error in ComponentProviderGenerator: {ex.Message}\nStack trace: {ex.StackTrace}", "SourceGenerator",
                    DiagnosticSeverity.Error, true),
                Location.None));
        }
    }

    private class ComponentInfo(string name, string ns, string fullName, INamedTypeSymbol symbol)
    {
        public string Name { get; } = name;
        public string Namespace { get; } = ns;
        public string FullName { get; } = fullName;
        public INamedTypeSymbol Symbol { get; } = symbol;
    }

    private static bool InheritsFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTypeSymbol)
    {
        // Check inheritance chain
        var currentSymbol = typeSymbol.BaseType;
        while (currentSymbol != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentSymbol, baseTypeSymbol))
                return true;

            currentSymbol = currentSymbol.BaseType;
        }

        return false;
    }

    private static string GenerateProvider(List<ComponentInfo> componentTypes,
        Dictionary<string, IFieldSymbol> weaponDefinitions, Compilation compilation)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file is automatically generated by ComponentProviderGenerator");
        sb.AppendLine("// Do not modify this file manually - changes will be overwritten");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Sanet.MakaMek.Core.Data.Units.Components;");
        sb.AppendLine("using Sanet.MakaMek.Core.Models.Units.Components;");

        // Add imports for all namespaces containing component types (excluding base namespace to avoid duplicate)
        var namespaces = componentTypes
            .Select(t => t.Namespace)
            .Distinct()
            .Where(ns => ns != "Sanet.MakaMek.Core.Models.Units.Components") // Avoid duplicate
            .OrderBy(ns => ns);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Models.Game.Rules");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated component mappings for ClassicBattletechComponentProvider");
        sb.AppendLine("    /// Generated by ComponentProviderGenerator source generator");
        sb.AppendLine($"    /// Total components: {componentTypes.Count}");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class ClassicBattletechComponentProvider");
        sb.AppendLine("    {");

        // Generate InitializeGeneratedDefinitions method
        GenerateDefinitionsMethod(sb, componentTypes, weaponDefinitions, compilation);

        sb.AppendLine();

        // Generate InitializeGeneratedFactories method
        GenerateFactoriesMethod(sb, componentTypes, weaponDefinitions, compilation);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateDefinitionsMethod(StringBuilder sb, List<ComponentInfo> componentTypes,
        Dictionary<string, IFieldSymbol> weaponDefinitions, Compilation compilation)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component definitions dictionary with auto-discovered mappings");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"definitions\">Dictionary to populate with component definitions</param>");
        sb.AppendLine("        partial void InitializeGeneratedDefinitions(");
        sb.AppendLine("            Dictionary<MakaMekComponent, ComponentDefinition> definitions)");
        sb.AppendLine("        {");

        // Get MakaMekComponent enum symbol
        var enumSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Data.Units.Components.MakaMekComponent");

        // Collect ammo mappings
        var ammoMappings = new List<(string AmmoEnumValue, string WeaponClassName)>();

        // Process regular components
        foreach (var component in componentTypes.OrderBy(c => c.Name))
        {
            // Skip Engine - it has dynamic definition
            if (component.Name == "Engine")
                continue;

            // Skip Ammo - it's handled separately
            if (component.Name == "Ammo")
                continue;

            // Find the static Definition field
            var definitionField = component.Symbol.GetMembers("Definition")
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.IsStatic && f.IsReadOnly);

            if (definitionField == null)
                continue;

            // Use name matching to determine enum value
            var componentEnumValue = MapClassNameToEnumValue(component.Name);
            if (componentEnumValue != null)
            {
                sb.AppendLine($"            definitions[MakaMekComponent.{componentEnumValue}] = {component.Name}.Definition;");

                // Check if this is a weapon with ammo
                if (weaponDefinitions.ContainsKey(component.Name))
                {
                    var ammoEnumValue = MapWeaponToAmmoEnumValue(component.Name);
                    if (ammoEnumValue != null)
                    {
                        ammoMappings.Add((ammoEnumValue, component.Name));
                    }
                }
            }
        }

        // Add ammo definitions
        if (ammoMappings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // Ammunition - generated from WeaponDefinition.AmmoComponentType");
            foreach (var (ammoEnum, weaponClass) in ammoMappings.OrderBy(a => a.AmmoEnumValue))
            {
                sb.AppendLine($"            definitions[MakaMekComponent.{ammoEnum}] = Ammo.CreateAmmoDefinition({weaponClass}.Definition);");
            }
        }

        sb.AppendLine("        }");
    }

    private static void GenerateFactoriesMethod(StringBuilder sb, List<ComponentInfo> componentTypes,
        Dictionary<string, IFieldSymbol> weaponDefinitions, Compilation compilation)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component factories dictionary with auto-discovered constructors");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"factories\">Dictionary to populate with component factory functions</param>");
        sb.AppendLine("        partial void InitializeGeneratedFactories(");
        sb.AppendLine("            Dictionary<MakaMekComponent, Func<ComponentData?, Component?>> factories)");
        sb.AppendLine("        {");

        // Get MakaMekComponent enum symbol
        var enumSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Data.Units.Components.MakaMekComponent");

        // Collect ammo mappings
        var ammoMappings = new List<(string AmmoEnumValue, string WeaponClassName)>();

        // Process regular components
        foreach (var component in componentTypes.OrderBy(c => c.Name))
        {
            // Skip Ammo - it's handled separately
            if (component.Name == "Ammo")
                continue;

            // Find the static Definition field (not needed for Engine)
            var definitionField = component.Symbol.GetMembers("Definition")
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.IsStatic && f.IsReadOnly);

            if (definitionField == null && component.Name != "Engine")
                continue;

            // Use name matching to determine enum value
            var componentEnumValue = component.Name == "Engine" ? "Engine" : MapClassNameToEnumValue(component.Name);
            if (componentEnumValue != null)
            {
                // Special handling for Engine
                if (component.Name == "Engine")
                {
                    sb.AppendLine($"            factories[MakaMekComponent.{componentEnumValue}] = data => ");
                    sb.AppendLine($"                data?.SpecificData is EngineStateData ? new {component.Name}(data) : null;");
                }
                else
                {
                    sb.AppendLine($"            factories[MakaMekComponent.{componentEnumValue}] = data => new {component.Name}(data);");
                }

                // Check if this is a weapon with ammo
                if (weaponDefinitions.ContainsKey(component.Name))
                {
                    var ammoEnumValue = MapWeaponToAmmoEnumValue(component.Name);
                    if (ammoEnumValue != null)
                    {
                        ammoMappings.Add((ammoEnumValue, component.Name));
                    }
                }
            }
        }

        // Add ammo factories
        if (ammoMappings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // Ammunition - generated from WeaponDefinition.AmmoComponentType");
            foreach (var (ammoEnum, weaponClass) in ammoMappings.OrderBy(a => a.AmmoEnumValue))
            {
                sb.AppendLine($"            factories[MakaMekComponent.{ammoEnum}] = data => new Ammo({weaponClass}.Definition, data);");
            }
        }

        sb.AppendLine("        }");
    }

    private static string? MapClassNameToEnumValue(string className)
    {
        // Map class names to MakaMekComponent enum values
        // Most follow a simple pattern, but some need special handling
        return className switch
        {
            // Actuators
            "ShoulderActuator" => "Shoulder",
            "UpperArmActuator" => "UpperArmActuator",
            "LowerArmActuator" => "LowerArmActuator",
            "HandActuator" => "HandActuator",
            "HipActuator" => "Hip",
            "UpperLegActuator" => "UpperLegActuator",
            "LowerLegActuator" => "LowerLegActuator",
            "FootActuator" => "FootActuator",

            // Internal Components
            "Gyro" => "Gyro",
            "LifeSupport" => "LifeSupport",
            "Sensors" => "Sensors",
            "Cockpit" => "Cockpit",

            // Equipment
            "HeatSink" => "HeatSink",
            "JumpJets" => "JumpJet",
            "Masc" => "Masc",

            // Energy Weapons
            "SmallLaser" => "SmallLaser",
            "MediumLaser" => "MediumLaser",
            "LargeLaser" => "LargeLaser",
            "Ppc" => "PPC",
            "Flamer" => "Flamer",

            // Ballistic Weapons
            "MachineGun" => "MachineGun",
            "Ac2" => "AC2",
            "Ac5" => "AC5",
            "Ac10" => "AC10",
            "Ac20" => "AC20",

            // Missile Weapons
            "Lrm5" => "LRM5",
            "Lrm10" => "LRM10",
            "Lrm15" => "LRM15",
            "Lrm20" => "LRM20",
            "Srm2" => "SRM2",
            "Srm4" => "SRM4",
            "Srm6" => "SRM6",

            // Melee Weapons
            "Hatchet" => "Hatchet",

            _ => null
        };
    }

    private static string? MapWeaponToAmmoEnumValue(string weaponClassName)
    {
        // Map weapon class names to their ammo enum values
        return weaponClassName switch
        {
            "MachineGun" => "ISAmmoMG",
            "Ac2" => "ISAmmoAC2",
            "Ac5" => "ISAmmoAC5",
            "Ac10" => "ISAmmoAC10",
            "Ac20" => "ISAmmoAC20",
            "Lrm5" => "ISAmmoLRM5",
            "Lrm10" => "ISAmmoLRM10",
            "Lrm15" => "ISAmmoLRM15",
            "Lrm20" => "ISAmmoLRM20",
            "Srm2" => "ISAmmoSRM2",
            "Srm4" => "ISAmmoSRM4",
            "Srm6" => "ISAmmoSRM6",
            _ => null
        };
    }

    private static string GenerateEmptyProvider()
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Sanet.MakaMek.Core.Data.Units.Components;");
        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Models.Game.Rules");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated component mappings for ClassicBattletechComponentProvider");
        sb.AppendLine("    /// Generated by ComponentProviderGenerator source generator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class ClassicBattletechComponentProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component definitions dictionary with auto-discovered mappings");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        partial void InitializeGeneratedDefinitions(");
        sb.AppendLine("            Dictionary<MakaMekComponent, ComponentDefinition> definitions)");
        sb.AppendLine("        {");
        sb.AppendLine("            // No components found by the source generator");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component factories dictionary with auto-discovered constructors");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        partial void InitializeGeneratedFactories(");
        sb.AppendLine("            Dictionary<MakaMekComponent, Func<ComponentData?, Component?>> factories)");
        sb.AppendLine("        {");
        sb.AppendLine("            // No components found by the source generator");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
