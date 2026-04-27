using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Sanet.MakaMek.SourceGenerators;

[Generator]
public class PilotingSkillRollContextTypeResolverGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all record declarations that could be PilotingSkillRollContext derived types
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
        // Look for record declarations that might inherit from PilotingSkillRollContext
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

        // Return all non-abstract record declarations; Execute's InheritsFrom is the authoritative gatekeeper
        return new TypeInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.ToDisplayString(),
            typeSymbol);
    }

    private static void Execute(Compilation compilation, ImmutableArray<TypeInfo> types, SourceProductionContext context)
    {
        try
        {
            // Find the PilotingSkillRollContext base type
            var pilotingSkillRollContextSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts.PilotingSkillRollContext");

            if (pilotingSkillRollContextSymbol is null)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyTypeResolverExtension("Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts");
                context.AddSource("PilotingSkillRollContextTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Filter records that inherit from PilotingSkillRollContext
            var derivedTypes = new List<TypeInfo>();

            foreach (var typeInfo in types)
            {
                if (InheritsFrom(typeInfo.Symbol, pilotingSkillRollContextSymbol))
                {
                    derivedTypes.Add(typeInfo);
                }
            }

            if (derivedTypes.Count == 0)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyTypeResolverExtension(pilotingSkillRollContextSymbol.ContainingNamespace.ToDisplayString());
                context.AddSource("PilotingSkillRollContextTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Generate the source code
            var source = GenerateTypeResolverExtension(pilotingSkillRollContextSymbol.ContainingNamespace.ToDisplayString(), derivedTypes);
            context.AddSource("PilotingSkillRollContextTypeResolverExtension.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("PSRCTRG001", "Source generator error",
                    $"Error in PilotingSkillRollContextTypeResolverGenerator: {ex.Message}\nStack trace: {ex.StackTrace}", "SourceGenerator",
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

    private static string GenerateTypeResolverExtension(string pilotingSkillRollContextNamespace, List<TypeInfo> derivedTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine($"using {pilotingSkillRollContextNamespace};");

        // Add imports for all namespaces containing derived types
        var namespaces = derivedTypes
            .Select(t => t.Namespace)
            .Distinct()
            .Where(ns => ns != pilotingSkillRollContextNamespace)
            .OrderBy(ns => ns);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Data.Serialization");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for PilotingSkillRollContextTypeResolver with auto-generated derived records");
        sb.AppendLine("    /// Generated automatically by PilotingSkillRollContextTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class PilotingSkillRollContextTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known PilotingSkillRollContext derived records");
        sb.AppendLine("        /// Generated automatically by PilotingSkillRollContextTypeResolverGenerator");
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

    private static string GenerateEmptyTypeResolverExtension(string pilotingSkillRollContextNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine($"using {pilotingSkillRollContextNamespace};");
        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Data.Serialization");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for PilotingSkillRollContextTypeResolver with auto-generated derived records");
        sb.AppendLine("    /// Generated automatically by PilotingSkillRollContextTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class PilotingSkillRollContextTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known PilotingSkillRollContext derived records");
        sb.AppendLine("        /// Generated automatically by PilotingSkillRollContextTypeResolverGenerator");
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
