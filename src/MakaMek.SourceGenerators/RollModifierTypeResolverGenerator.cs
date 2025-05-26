using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Sanet.MakaMek.SourceGenerators;

[Generator]
public class RollModifierTypeResolverGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Get our syntax receiver
        if (context.SyntaxContextReceiver is not SyntaxReceiver)
            return;

        // Get the compilation
        var compilation = context.Compilation;

        // Find the RollModifier type
        var rollModifierSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.RollModifier");
        if (rollModifierSymbol == null)
        {
            // Try with a different namespace
            rollModifierSymbol = compilation.GetTypeByMetadataName("Sanet.MakaMek.Core.Models.Game.Mechanics.RollModifier");
            if (rollModifierSymbol == null)
            {
                // If we still can't find it, try to find it by looking at all types
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var classDeclarations = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                        
                    foreach (var classDeclaration in classDeclarations)
                    {
                        if (classDeclaration.Identifier.Text != "RollModifier") continue;
                        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                        if (symbol == null) continue;
                        rollModifierSymbol = symbol;
                        break;
                    }
                        
                    if (rollModifierSymbol != null)
                        break;
                }
                    
                if (rollModifierSymbol == null)
                {
                    // Add diagnostic to help debugging
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RMGEN001", 
                            "RollModifier type not found", 
                            "Could not find RollModifier type in compilation", 
                            "RollModifierGenerator", 
                            DiagnosticSeverity.Warning, 
                            true), 
                        Location.None));
                    return; // Still couldn't find RollModifier
                }
            }
        }

        // Log the found RollModifier type for debugging
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "RMGEN003", 
                "RollModifier type found", 
                "Found RollModifier type: {0}", 
                "RollModifierGenerator", 
                DiagnosticSeverity.Info, 
                true), 
            Location.None,
            rollModifierSymbol.ToDisplayString()));

        // Now that we have the RollModifier type, find all derived classes
        var derivedClasses = new List<INamedTypeSymbol>();
        
        // Scan all syntax trees for class and record declarations
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            
            // Look for both class and record declarations
            var typeDeclarations = syntaxTree.GetRoot().DescendantNodes()
                .Where(n => n is ClassDeclarationSyntax || n is RecordDeclarationSyntax);
            
            foreach (var typeDeclaration in typeDeclarations)
            {
                BaseTypeSyntax? baseType = null;
                string typeName = "";
                
                if (typeDeclaration is ClassDeclarationSyntax classDecl)
                {
                    baseType = classDecl.BaseList?.Types.FirstOrDefault();
                    typeName = classDecl.Identifier.Text;
                }
                else if (typeDeclaration is RecordDeclarationSyntax recordDecl)
                {
                    baseType = recordDecl.BaseList?.Types.FirstOrDefault();
                    typeName = recordDecl.Identifier.Text;
                }
                
                // Skip if no base type
                if (baseType == null) continue;
                
                // Log the type we're checking
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "RMGEN006", 
                        "Checking type", 
                        "Checking type {0} with base type syntax {1}", 
                        "RollModifierGenerator", 
                        DiagnosticSeverity.Info, 
                        true), 
                    Location.None,
                    typeName,
                    baseType.ToString()));
                
                var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (symbol == null) continue;
                
                // Convert to INamedTypeSymbol
                var typeSymbol = symbol as INamedTypeSymbol;
                if (typeSymbol == null || typeSymbol.IsAbstract) continue;
                
                // Check if this type inherits from RollModifier
                if (InheritsFrom(typeSymbol, rollModifierSymbol))
                {
                    derivedClasses.Add(typeSymbol);
                    
                    // Log found derived class
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RMGEN005", 
                            "Found derived class", 
                            "Found derived class: {0}", 
                            "RollModifierGenerator", 
                            DiagnosticSeverity.Info, 
                            true), 
                        Location.None,
                        typeSymbol.ToDisplayString()));
                }
            }
        }
        
        // Add diagnostic to help debugging
        if (derivedClasses.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "RMGEN002", 
                    "No RollModifier derived types found", 
                    "Found RollModifier type but no derived types were found in the compilation.", 
                    "RollModifierGenerator", 
                    DiagnosticSeverity.Warning, 
                    true),
                Location.None));
            
            // Generate empty implementation to avoid compilation errors
            var emptySource = @"
// <auto-generated/>
using System;
using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Core.Services.Transport
{
    public partial class RollModifierTypeResolver
    {
        static partial void RegisterGeneratedTypes(System.Text.Json.Serialization.Metadata.JsonTypeInfo jsonTypeInfo)
        {
            // No derived types found by the source generator
            // This is a placeholder implementation to avoid compilation errors
        }
    }
}";
            context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(emptySource, Encoding.UTF8));
            return;
        }
            
        // Generate the source code
        var source = GenerateTypeResolverExtension(rollModifierSymbol.ContainingNamespace.ToDisplayString(), derivedClasses);
            
        // Add the source code to the compilation
        context.AddSource("RollModifierTypeResolverExtension.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private bool InheritsFrom(INamedTypeSymbol classSymbol, INamedTypeSymbol baseTypeSymbol)
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

    private string GenerateTypeResolverExtension(string rollModifierNamespace, List<INamedTypeSymbol> derivedClasses)
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
            .Select(c => c.ContainingNamespace.ToDisplayString())
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
            string fullClassName = derivedClass.ToDisplayString();
            string className = derivedClass.Name;
            sb.AppendLine($"            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof({fullClassName}), \"{className}\"));");
        }
            
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
            
        return sb.ToString();
    }

    /// <summary>
    /// Syntax receiver that looks for classes that might derive from RollModifier
    /// </summary>
    private class SyntaxReceiver : ISyntaxContextReceiver
    {
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // This method is no longer used as we're directly scanning for derived classes in Execute
        }
    }
}