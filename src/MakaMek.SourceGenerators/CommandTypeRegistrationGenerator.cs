using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Sanet.MakaMek.SourceGenerators;

[Generator]
public class CommandTypeRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all record struct declarations that could be IGameCommand implementations
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
        // Look for record struct declarations that might implement IGameCommand
        return node is RecordDeclarationSyntax { BaseList: not null };
    }

    private static TypeInfo? GetTypeInfo(GeneratorSyntaxContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;

        // Get the semantic model to check interface implementation
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(recordDeclaration);

        if (typeSymbol is null || typeSymbol.IsAbstract)
            return null;

        // Only consider records that might be related to IGameCommand
        var fullName = typeSymbol.ToDisplayString();
        if (fullName.Contains("Command"))
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
            // Find the IGameCommand interface
            var gameCommandSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Data.Game.Commands.IGameCommand");

            if (gameCommandSymbol is null)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyCommandRegistration();
                context.AddSource("CommandTransportAdapterExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Filter records that implement IGameCommand
            var commandTypes = new List<TypeInfo>();

            foreach (var typeInfo in types)
            {
                if (ImplementsInterface(typeInfo.Symbol, gameCommandSymbol))
                {
                    commandTypes.Add(typeInfo);
                }
            }

            if (commandTypes.Count == 0)
            {
                // Generate empty implementation
                var emptySource = GenerateEmptyCommandRegistration();
                context.AddSource("CommandTransportAdapterExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
                return;
            }

            // Generate the source code
            var source = GenerateCommandRegistration(commandTypes);
            context.AddSource("CommandTransportAdapterExtension.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("CTRG001", "Source generator error",
                    $"Error in CommandTypeRegistrationGenerator: {ex.Message}\nStack trace: {ex.StackTrace}", "SourceGenerator",
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

    private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
    {
        // Check if the type directly implements the interface
        foreach (var implementedInterface in typeSymbol.Interfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implementedInterface, interfaceSymbol))
                return true;
        }

        // Check all interfaces (including inherited ones)
        foreach (var implementedInterface in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implementedInterface, interfaceSymbol))
                return true;
        }

        return false;
    }

    private static string GenerateCommandRegistration(List<TypeInfo> commandTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");

        // Add imports for all namespaces containing command types
        var namespaces = commandTypes
            .Select(t => t.Namespace)
            .Distinct()
            .OrderBy(ns => ns);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Services.Transport");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for CommandTransportAdapter with auto-generated command type registration");
        sb.AppendLine("    /// Generated automatically by CommandTypeRegistrationGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class CommandTransportAdapter");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known IGameCommand types");
        sb.AppendLine("        /// Generated automatically by CommandTypeRegistrationGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        partial void RegisterGeneratedCommandTypes(Dictionary<string, Type> commandTypes)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Add all known command types generated by the source generator");

        // Add a line for each command type
        foreach (var commandType in commandTypes.OrderBy(t => t.Name))
        {
            sb.AppendLine($"            commandTypes[\"{commandType.Name}\"] = typeof({commandType.FullName});");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateEmptyCommandRegistration()
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Sanet.MakaMek.Core.Services.Transport");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extension for CommandTransportAdapter with auto-generated command type registration");
        sb.AppendLine("    /// Generated automatically by CommandTypeRegistrationGenerator");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public partial class CommandTransportAdapter");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all known IGameCommand types");
        sb.AppendLine("        /// Generated automatically by CommandTypeRegistrationGenerator");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        partial void RegisterGeneratedCommandTypes(Dictionary<string, Type> commandTypes)");
        sb.AppendLine("        {");
        sb.AppendLine("            // No command types found by the source generator");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

