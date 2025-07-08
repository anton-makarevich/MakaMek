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
        // Find all class declarations that could be RollModifier derived types
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetClassInfo(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Combine with compilation to get semantic information
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate the source
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        // Look for class declarations that might inherit from RollModifier
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    private static ClassInfo? GetClassInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Get the semantic model to check inheritance
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol is null || classSymbol.IsAbstract)
            return null;

        // Only consider classes that might be related to RollModifier
        var fullName = classSymbol.ToDisplayString();
        if (fullName.Contains("RollModifier") || fullName.Contains("Modifier"))
        {
            return new ClassInfo(
                classSymbol.Name,
                classSymbol.ContainingNamespace.ToDisplayString(),
                classSymbol.ToDisplayString(),
                classSymbol);
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassInfo> classes, SourceProductionContext context)
    {
        try
        {
            // Add diagnostic to confirm the generator is running
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG100", "Source generator started",
                    $"RollModifierTypeResolverGenerator started execution with {classes.Length} candidate classes", "SourceGenerator",
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

            // Filter classes that inherit from RollModifier
            var derivedClasses = new List<ClassInfo>();

            foreach (var classInfo in classes)
            {
                if (InheritsFrom(classInfo.Symbol, rollModifierSymbol))
                {
                    derivedClasses.Add(classInfo);
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RMTRG103", "Found derived class",
                            $"Found RollModifier derived class: {classInfo.FullName}", "SourceGenerator",
                            DiagnosticSeverity.Info, true),
                        Location.None));
                }
            }

            if (derivedClasses.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("RMTRG104", "No derived classes found",
                        "No classes found that inherit from RollModifier", "SourceGenerator",
                        DiagnosticSeverity.Warning, true),
                    Location.None));

                // Generate empty implementation
                var emptySource = GenerateEmptyTypeResolverExtension(rollModifierSymbol.ContainingNamespace.ToDisplayString());
                context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("RMTRG105", "Generating source",
                    $"Generating source code for {derivedClasses.Count} derived classes", "SourceGenerator",
                    DiagnosticSeverity.Info, true),
                Location.None));

            // Generate the source code
            var source = GenerateTypeResolverExtension(rollModifierSymbol.ContainingNamespace.ToDisplayString(), derivedClasses);
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

    private class ClassInfo
    {
        public string Name { get; }
        public string Namespace { get; }
        public string FullName { get; }
        public INamedTypeSymbol Symbol { get; }

        public ClassInfo(string name, string @namespace, string fullName, INamedTypeSymbol symbol)
        {
            Name = name;
            Namespace = @namespace;
            FullName = fullName;
            Symbol = symbol;
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol classSymbol, INamedTypeSymbol baseTypeSymbol)
    {
        // Check if the class directly inherits from the base type
        if (classSymbol.BaseType != null &&
            SymbolEqualityComparer.Default.Equals(classSymbol.BaseType, baseTypeSymbol))
        {
            return true;
        }

        // Check inheritance chain
        var currentSymbol = classSymbol.BaseType;
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

        // Check if the class implements the base type as an interface
        foreach (var interfaceSymbol in classSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaceSymbol, baseTypeSymbol))
                return true;
        }

        return false;
    }

    private static string GenerateTypeResolverExtension(string rollModifierNamespace, List<ClassInfo> derivedClasses)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine($"using {rollModifierNamespace};");

        // Add imports for all namespaces containing derived classes
        var namespaces = derivedClasses
            .Select(c => c.Namespace)
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
        sb.AppendLine("    /// Extension for RollModifierTypeResolver with auto-generated derived types");
        sb.AppendLine("    /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class RollModifierTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known RollModifier derived types");
        sb.AppendLine("        /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Add all known derived types generated by the source generator");

        // Add a line for each derived class
        foreach (var derivedClass in derivedClasses.OrderBy(c => c.Name))
        {
            sb.AppendLine(
                $"            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof({derivedClass.FullName}), \"{derivedClass.Name}\"));");
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
        sb.AppendLine("    /// Extension for RollModifierTypeResolver with auto-generated derived types");
        sb.AppendLine("    /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class RollModifierTypeResolver");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known RollModifier derived types");
        sb.AppendLine("        /// Generated automatically by RollModifierTypeResolverGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo)");
        sb.AppendLine("        {");
        sb.AppendLine("            // No derived types found by the source generator");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }


}