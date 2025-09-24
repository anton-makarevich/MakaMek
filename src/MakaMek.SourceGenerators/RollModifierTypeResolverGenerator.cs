using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Sanet.MakaMek.SourceGenerators;

[Generator]
public class RollModifierTypeResolverGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all record declarations that could be RollModifier derived types
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateType(s),
                transform: static (ctx, _) => GetTypeInfo(ctx))
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
        // Look for record declarations that might inherit from RollModifier
        return node is RecordDeclarationSyntax { BaseList: not null };
    }

    private static TypeInfo? GetTypeInfo(GeneratorSyntaxContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;

        // Get the semantic model to check inheritance
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(recordDeclaration);

        if (typeSymbol is null || typeSymbol.IsAbstract)
            return null;

        // Only consider records that might be related to RollModifier
        var fullName = typeSymbol.ToDisplayString();
        if (fullName.Contains("RollModifier") || fullName.Contains("Modifier"))
        {
            return new TypeInfo(
                typeSymbol.Name,
                typeSymbol.ContainingNamespace.ToDisplayString(),
                typeSymbol.ToDisplayString(),
                typeSymbol);
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<TypeInfo> types, SourceProductionContext context)
    {
        try
        {
            // Add diagnostic to confirm the generator is running
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG100", "Source generator started",
                    $"RollModifierTypeResolverGenerator started execution with {types.Length} candidate records", "SourceGenerator",
                    DiagnosticSeverity.Info, true),
                Location.None));

            // Find the RollModifier base type
            var rollModifierSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.RollModifier");

            if (rollModifierSymbol is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("RMTRG101", "RollModifier base type not found",
                        "Could not find RollModifier base type in compilation", "SourceGenerator",
                        DiagnosticSeverity.Warning, true),
                    Location.None));

                // Generate empty implementation
                var emptySource = GenerateEmptyTypeResolverExtension("Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers");
                context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG102", "RollModifier base type found",
                    $"Found RollModifier base type: {rollModifierSymbol.ToDisplayString()}", "SourceGenerator",
                    DiagnosticSeverity.Info, true),
                Location.None));

            // Filter records that inherit from RollModifier
            var derivedTypes = new List<TypeInfo>();

            foreach (var typeInfo in types)
            {
                if (InheritsFrom(typeInfo.Symbol, rollModifierSymbol))
                {
                    derivedTypes.Add(typeInfo);
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RMTRG103", "Found derived record",
                            $"Found RollModifier derived record: {typeInfo.FullName}", "SourceGenerator",
                            DiagnosticSeverity.Info, true),
                        Location.None));
                }
            }

            if (derivedTypes.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("RMTRG104", "No derived records found",
                        "No records found that inherit from RollModifier", "SourceGenerator",
                        DiagnosticSeverity.Warning, true),
                    Location.None));

                // Generate empty implementation
                var emptySource = GenerateEmptyTypeResolverExtension(rollModifierSymbol.ContainingNamespace.ToDisplayString());
                context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG105", "Generating source",
                    $"Generating source code for {derivedTypes.Count} derived records", "SourceGenerator",
                    DiagnosticSeverity.Info, true),
                Location.None));

            // Generate the source code
            var source = GenerateTypeResolverExtension(rollModifierSymbol.ContainingNamespace.ToDisplayString(), derivedTypes);
            context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(source, Encoding.UTF8));

            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG106", "Source generation completed",
                    "Successfully generated RollModifierTypeResolverExtension.g.cs", "SourceGenerator",
                    DiagnosticSeverity.Info, true),
                Location.None));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG001", "Source generator error",
                    $"Error in RollModifierTypeResolverGenerator: {ex.Message}\nStack trace: {ex.StackTrace}", "SourceGenerator",
                    DiagnosticSeverity.Error, true),
                Location.None));
        }
    }

    private class TypeInfo(string name, string ns, string fullName, INamedTypeSymbol symbol)
    {
        public string Name { get; } = name;
        public string Namespace { get; } = ns;
        public string FullName { get; } = fullName;
        public INamedTypeSymbol Symbol { get; } = symbol;
    }

    private static bool InheritsFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTypeSymbol)
    {
        // Check if the type directly inherits from the base type
        if (typeSymbol.BaseType != null &&
            SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, baseTypeSymbol))
        {
            return true;
        }

        // Check inheritance chain
        var currentSymbol = typeSymbol.BaseType;
        while (currentSymbol != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentSymbol, baseTypeSymbol))
                return true;

            // Check if the base type is a constructed generic type
            if (currentSymbol is { IsGenericType: true })
            {
                if (SymbolEqualityComparer.Default.Equals(currentSymbol.ConstructedFrom, baseTypeSymbol))
                    return true;
            }

            currentSymbol = currentSymbol.BaseType;
        }

        // Check if the type implements the base type as an interface
        foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaceSymbol, baseTypeSymbol))
                return true;
        }

        return false;
    }

    private static string GenerateTypeResolverExtension(string rollModifierNamespace, List<TypeInfo> derivedTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine($"using {rollModifierNamespace};");

        // Add imports for all namespaces containing derived types
        var namespaces = derivedTypes
            .Select(t => t.Namespace)
            .Distinct()
            .Where(ns => ns != rollModifierNamespace)
            .OrderBy(ns => ns);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Services.Transport");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for RollModifierTypeResolver with auto-generated derived records");
        sb.AppendLine("    /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class RollModifierTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known RollModifier derived records");
        sb.AppendLine("        /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Add all known derived records generated by the source generator");

        // Add a line for each derived record
        foreach (var derivedType in derivedTypes.OrderBy(t => t.Name))
        {
            sb.AppendLine(
                $"            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof({derivedType.FullName}), \"{derivedType.Name}\"));");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateEmptyTypeResolverExtension(string rollModifierNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine($"using {rollModifierNamespace};");
        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Services.Transport");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for RollModifierTypeResolver with auto-generated derived records");
        sb.AppendLine("    /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class RollModifierTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known RollModifier derived records");
        sb.AppendLine("        /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            // No derived records found by the source generator");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}