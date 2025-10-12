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

        // Return the component info - inheritance checking will be done in Execute
        // This is a preliminary filter to reduce the number of types to process
        return new ComponentInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol);
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
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("CPG002", "Components not found",
                        "compilation.GetTypeByMetadataName did not find any components.",
                        "SourceGenerator", DiagnosticSeverity.Error, true),
                    Location.None));
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
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("CPG003", "Components not found",
                        "No Components were added to the list",
                        "SourceGenerator", DiagnosticSeverity.Error, true),
                    Location.None));
                // Generate empty implementation
                var emptySource = GenerateEmptyProvider();
                context.AddSource("ClassicBattletechComponentProvider.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Generate the source code
            var source = GenerateProvider(componentTypes, weaponDefinitions, context, compilation);
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

    private class ComponentInfo(string name, string ns, INamedTypeSymbol symbol)
    {
        public string Name { get; } = name;
        public string Namespace { get; } = ns;
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
        Dictionary<string, IFieldSymbol> weaponDefinitions, SourceProductionContext context, Compilation compilation)
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
        GenerateDefinitionsMethod(sb, componentTypes, weaponDefinitions, context, compilation);

        sb.AppendLine();

        // Generate InitializeGeneratedFactories method
        GenerateFactoriesMethod(sb, componentTypes, weaponDefinitions, context, compilation);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateDefinitionsMethod(StringBuilder sb, List<ComponentInfo> componentTypes,
        Dictionary<string, IFieldSymbol> weaponDefinitions, SourceProductionContext context, Compilation compilation)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component definitions dictionary with auto-discovered mappings");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"definitions\">Dictionary to populate with component definitions</param>");
        sb.AppendLine("        partial void InitializeGeneratedDefinitions(");
        sb.AppendLine("            Dictionary<MakaMekComponent, ComponentDefinition> definitions)");
        sb.AppendLine("        {");

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

            // Extract the ComponentType enum value from the Definition field
            var componentEnumValue = ExtractComponentTypeFromDefinition(definitionField, compilation);
            if (componentEnumValue != null)
            {
                sb.AppendLine($"            definitions[MakaMekComponent.{componentEnumValue}] = {component.Name}.Definition;");

                // Check if this is a weapon with ammo
                if (weaponDefinitions.ContainsKey(component.Name))
                {
                    var ammoEnumValue = ExtractAmmoComponentTypeFromDefinition(definitionField, compilation);
                    if (ammoEnumValue != null)
                    {
                        ammoMappings.Add((ammoEnumValue, component.Name));
                    }
                }
            }
            else
            {
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("CPG004", "ComponentType not found",
                        $"ComponentDefinition for {component.Name} does not have a ComponentType specified",
                        "SourceGenerator", DiagnosticSeverity.Warning, true),
                    Location.None));
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
        Dictionary<string, IFieldSymbol> weaponDefinitions, SourceProductionContext context, Compilation compilation)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes component factories dictionary with auto-discovered constructors");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"factories\">Dictionary to populate with component factory functions</param>");
        sb.AppendLine("        partial void InitializeGeneratedFactories(");
        sb.AppendLine("            Dictionary<MakaMekComponent, Func<ComponentData?, Component?>> factories)");
        sb.AppendLine("        {");

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

            // Extract the ComponentType enum value from the Definition field
            // For Engine, we use "Engine" directly since it has dynamic definition
            var componentEnumValue = component.Name == "Engine"
                ? "Engine"
                : ExtractComponentTypeFromDefinition(definitionField!, compilation);

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
                    var ammoEnumValue = ExtractAmmoComponentTypeFromDefinition(definitionField!, compilation);
                    if (ammoEnumValue != null)
                    {
                        ammoMappings.Add((ammoEnumValue, component.Name));
                    }
                }
            }
            else
            {
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("CPG005", "ComponentType not found",
                        $"ComponentDefinition for {component.Name} does not have a ComponentType specified (factories)",
                        "SourceGenerator", DiagnosticSeverity.Warning, true),
                    Location.None));
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

    /// <summary>
    /// Extracts the ComponentType enum value from a component's static Definition field
    /// Uses semantic analysis to robustly identify MakaMekComponent enum members
    /// </summary>
    private static string? ExtractComponentTypeFromDefinition(IFieldSymbol definitionField, Compilation compilation)
    {
        // Get the field declaration syntax
        var syntaxRef = definitionField.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return null;

        var syntax = syntaxRef.GetSyntax();

        // The syntax should be a VariableDeclaratorSyntax
        if (syntax is not VariableDeclaratorSyntax declarator)
            return null;

        if (declarator.Initializer == null)
            return null;

        // Get the semantic model for this syntax tree
        var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

        // Handle both explicit (new TypeName(...)) and implicit (new(...)) object creation
        ArgumentListSyntax? argumentList;

        if (declarator.Initializer.Value is ObjectCreationExpressionSyntax objectCreation)
        {
            argumentList = objectCreation.ArgumentList;
        }
        else if (declarator.Initializer.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            argumentList = implicitCreation.ArgumentList;
        }
        else
        {
            return null;
        }

        if (argumentList == null)
            return null;

        // Search through all arguments for a MakaMekComponent enum value
        // Look for the FIRST MakaMekComponent enum member that's not AmmoComponentType
        foreach (var argument in argumentList.Arguments)
        {
            // Get the argument name - could be from NameColon (old style) or from the argument itself
            string? argName = null;

            // Check for named argument with colon (e.g., Name: "value")
            if (argument.NameColon != null)
            {
                argName = argument.NameColon.Name.Identifier.Text;
            }

            // Skip AmmoComponentType - we want ComponentType or WeaponComponentType
            if (argName == "AmmoComponentType")
                continue;

            // Use semantic analysis to get the symbol information
            var symbolInfo = semanticModel.GetSymbolInfo(argument.Expression);

            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
            {
                // Check if it's a MakaMekComponent enum member
                if (fieldSymbol.ContainingType?.Name == "MakaMekComponent" &&
                    fieldSymbol.ContainingType.TypeKind == TypeKind.Enum)
                {
                    // Return the enum member name
                    return fieldSymbol.Name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the AmmoComponentType enum value from a WeaponDefinition field
    /// Uses semantic analysis to robustly identify MakaMekComponent enum members
    /// </summary>
    private static string? ExtractAmmoComponentTypeFromDefinition(IFieldSymbol definitionField, Compilation compilation)
    {
        // Get the field declaration syntax
        var syntaxRef = definitionField.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return null;

        var fieldDecl = syntaxRef.GetSyntax() as VariableDeclaratorSyntax;
        if (fieldDecl?.Initializer == null)
            return null;

        // Get the semantic model for this syntax tree
        var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

        // Handle both explicit and implicit object creation
        ArgumentListSyntax? argumentList;

        if (fieldDecl.Initializer.Value is ObjectCreationExpressionSyntax objectCreation)
        {
            argumentList = objectCreation.ArgumentList;
        }
        else if (fieldDecl.Initializer.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            argumentList = implicitCreation.ArgumentList;
        }
        else
        {
            return null;
        }

        // Look for the AmmoComponentType parameter (only in WeaponDefinition)
        if (argumentList == null)
            return null;

        foreach (var argument in argumentList.Arguments)
        {
            // Only look for named parameter "AmmoComponentType"
            var argName = argument.NameColon?.Name.Identifier.Text;
            if (argName == "AmmoComponentType")
            {
                // Use semantic analysis to get the symbol information
                var symbolInfo = semanticModel.GetSymbolInfo(argument.Expression);

                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // Check if it's a MakaMekComponent enum member
                    if (fieldSymbol.ContainingType?.Name == "MakaMekComponent" &&
                        fieldSymbol.ContainingType.TypeKind == TypeKind.Enum)
                    {
                        // Return the enum member name
                        return fieldSymbol.Name;
                    }
                }
            }
        }

        return null;
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
