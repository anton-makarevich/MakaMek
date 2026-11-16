using System.Diagnostics;
using AnalyzerGate;
using Shouldly;

namespace Sanet.MakaMek.AnalyzerGate.Tests;

public class SuppressionScanTests
{
    [Fact]
    public void SuppressionScopes_MultilineAttribute_AttributesTheRuleToTheFollowingType()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage(
                "Maintainability",
                "CA1506:Avoid excessive class coupling",
                Justification = "fixture")]
            public class Example
            {
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "type:Example"));
    }

    [Fact]
    public void SuppressionScopes_AttributeFollowedByAnotherAttributeInTheSameList_IsDetected()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1506:Avoid excessive class coupling"), System.Obsolete]
            public class Example
            {
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "type:Example"));
    }

    [Fact]
    public void SuppressionScopes_CommentMarkersInsideStringLiterals_DoNotHideTheAttribute()
    {
        const string source = """
            public class Example
            {
                private const string Open = "/*";

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1506:Avoid excessive class coupling")]
                public void Run() { }

                private const string Close = "*/";
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "member:Example.Run()"));
    }

    [Fact]
    public void SuppressionScopes_CommentedOutDirective_IsIgnored()
    {
        const string source = """
            public class Example
            {
                // #pragma warning disable CA1506
                /* #pragma warning disable S3776 */
                public void Run() { }
            }
            """;

        Program.SuppressionScopes(source).ShouldBeEmpty();
    }

    [Fact]
    public void SuppressionScopes_DirectiveWithoutRuleIds_SuppressesEveryGatedRule()
    {
        const string source = """
            public class Example
            {
            #pragma warning disable
                public void Run() { }
            #pragma warning restore
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1501", "member:Example.Run()"));
        scopes.ShouldContainKey(("CA1502", "member:Example.Run()"));
        scopes.ShouldContainKey(("CA1505", "member:Example.Run()"));
        scopes.ShouldContainKey(("CA1506", "member:Example.Run()"));
        scopes.ShouldContainKey(("S3776", "member:Example.Run()"));
    }

    [Fact]
    public void FindNewSuppressionsIn_MovedSuppression_FailsForTheNewMember()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #pragma warning restore S3776
                public void Second() { }
            }
            """;
        const string current = """
            public class Example
            {
                public void First() { }
            #pragma warning disable S3776
                public void Second() { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Second");
    }

    [Fact]
    public void FindNewSuppressionsIn_RemovedRestore_ThatWidensTheScope_FailsForTheNewMember()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #pragma warning restore S3776
                public void Second() { }
            }
            """;
        const string current = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
                public void Second() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Second");
    }

    [Fact]
    public void FindNewSuppressionsIn_ConditionalRestore_DoesNotNarrowTheSuppression_Fails()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #pragma warning restore S3776
                public void Second() { }
            }
            """;
        const string current = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #if FEATURE
            #pragma warning restore S3776
            #endif
                public void Second() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Second");
    }

    [Fact]
    public void FindNewSuppressionsIn_SecondSuppressionForAnAlreadySuppressedRule_Fails()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #pragma warning restore S3776
                public void Second() { }
            }
            """;
        const string current = """
            public class Example
            {
            #pragma warning disable S3776
                public void First() { }
            #pragma warning restore S3776
            #pragma warning disable S3776
                public void Second() { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Second");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnchangedSuppressions_WithCommentEdits_Passes()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable CA1506
                public void Run() { }
            #pragma warning restore CA1506
            }
            """;
        const string current = """
            // A new explanatory comment above the class.
            public class Example
            {
                // Comments inside the suppressed member are fine too.
            #pragma warning disable CA1506
                public void Run() { }
            #pragma warning restore CA1506
            }
            """;

        Program.FindNewSuppressionsIn("Example.cs", baseline, current).ShouldBeEmpty();
    }

    [Fact]
    public void FindNewSuppressionsIn_NewFileWithASuppression_Fails()
    {
        const string current = """
            public class Example
            {
            #pragma warning disable CA1502
                public void Run() { }
            #pragma warning restore CA1502
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1502");
    }

    [Fact]
    public void SuppressionScopes_PragmaWithWhitespaceAfterHash_IsRecognized()
    {
        const string source = """
            public class Example
            {
            # pragma warning disable S3776
                public void Run() { }
            #  pragma warning restore S3776
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("S3776", "member:Example.Run()"));
    }

    [Fact]
    public void FindNewSuppressionsIn_PragmaWithWhitespaceAfterHash_Fails()
    {
        const string baseline = """
            public class Example
            {
                public void Run() { }
            }
            """;
        const string current = """
            public class Example
            {
            # pragma warning disable S3776
                public void Run() { }
            # pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("S3776");
    }

    [Fact]
    public void SuppressionScopes_AttributeWithConcatenatedRuleArgument_IsResolvedToTheRuleId()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "type:Example"));
    }

    [Fact]
    public void SuppressionScopes_AttributeWithEscapedRuleId_IsDetected()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "\u0043A1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "type:Example"));
    }

    [Fact]
    public void FindNewSuppressionsIn_AttributeWithEscapedRuleId_Fails()
    {
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "\u0043A1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_EditedConcatenatedRuleArgument_Fails()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "1822:Avoid unnecessary code")]
            public class Example
            {
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_LiteralWhitespaceDifference_Fails()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "15 06:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_EditedReferencedConstant_Fails()
    {
        const string baseline = """
            public class Example
            {
                private const string RuleId = "CA1822:Avoid unnecessary code";

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
                public void Run() { }
            }
            """;
        const string current = """
            public class Example
            {
                private const string RuleId = "CA1506:Avoid excessive class coupling";

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
                public void Run() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_EditedConstantWithConcatenatedInitializer_Fails()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
            public class Example
            {
            }

            internal static class RuleIds
            {
                public const string Id = "CA" + "1822:Avoid unnecessary code";
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
            public class Example
            {
            }

            internal static class RuleIds
            {
                public const string Id = "CA" + "1506:Avoid excessive class coupling";
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnresolvableRuleArgument_Fails()
    {
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
        failures[0].ShouldContain("Example.cs(1)");
        failures[0].ShouldContain("RuleIds.Id");
    }

    [Fact]
    public void SuppressionScopes_AttributeWithRawStringRuleId_IsDetected()
    {
        const string source = """"
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", """CA1506:Avoid excessive class coupling""")]
            public class Example
            {
            }
            """";

        var scopes = Program.SuppressionScopes(source);

        scopes.ShouldContainKey(("CA1506", "type:Example"));
    }

    [Fact]
    public void FindNewSuppressionsIn_AttributeWithRawStringRuleId_Fails()
    {
        const string current = """"
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", """CA1506:Avoid excessive class coupling""")]
            public class Example
            {
            }
            """";

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_MultilineRawStringRuleArgument_Fails()
    {
        const string current = """"
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", """
                CA1506:Avoid excessive class coupling
                """)]
            public class Example
            {
            }
            """";

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_EditedQualifiedConstant_ResolvesTheQualifiedDeclaration()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
            public class Example
            {
            }

            internal static class OtherIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }

            internal static class RuleIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
            public class Example
            {
            }

            internal static class OtherIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }

            internal static class RuleIds
            {
                public const string Id = "CA1506:Avoid excessive class coupling";
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_AmbiguousUnqualifiedConstant_Fails()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", Id)]
            public class Example
            {
            }

            internal static class OtherIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }

            internal static class RuleIds
            {
                public const string Id = "CA1506:Avoid excessive class coupling";
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_ExternalQualifiedReference_DoesNotBindToALocalConstant()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", ExternalIds.Id)]
            public class Example
            {
            }

            internal static class RuleIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", ExternalIds.Id)]
            public class Example
            {
            }

            internal static class RuleIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_ExternalNamespaceTypeWithMatchingName_Fails()
    {
        const string source = """
            namespace Other
            {
                internal static class RuleIds
                {
                    public const string Id = "CA1822:Avoid unnecessary code";
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", External.RuleIds.Id)]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_LocalNamespaceConstant_Resolves()
    {
        const string baseline = """
            namespace Other
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
                public class Example
                {
                }

                internal static class RuleIds
                {
                    public const string Id = "CA1822:Avoid unnecessary code";
                }
            }
            """;
        const string current = """
            namespace Other
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", RuleIds.Id)]
                public class Example
                {
                }

                internal static class RuleIds
                {
                    public const string Id = "CA1506:Avoid excessive class coupling";
                }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_FullyQualifiedNamespaceConstant_Resolves()
    {
        const string baseline = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", Other.RuleIds.Id)]
            public class Example
            {
            }

            namespace Other
            {
                internal static class RuleIds
                {
                    public const string Id = "CA1822:Avoid unnecessary code";
                }
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", Other.RuleIds.Id)]
            public class Example
            {
            }

            namespace Other
            {
                internal static class RuleIds
                {
                    public const string Id = "CA1506:Avoid excessive class coupling";
                }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_LocalConstantInsideMethod_IsNotAnAttributeCandidate()
    {
        const string source = """
            using static External.RuleIds;

            public class Example
            {
                public void Other()
                {
                    const string Id = "CA1822:Avoid unnecessary code";
                }

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", Id)]
                public void Run() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_MovedSuppressionBetweenTypesWithTheSameMemberName_Fails()
    {
        const string baseline = """
            public class A
            {
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            }

            public class B
            {
                public void Run() { }
            }
            """;
        const string current = """
            public class A
            {
                public void Run() { }
            }

            public class B
            {
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:B.Run");
    }

    [Fact]
    public void FindNewSuppressionsIn_MovedSuppressionBetweenOverloads_Fails()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void Run(int value) { }
            #pragma warning restore S3776

                public void Run(string value) { }
            }
            """;
        const string current = """
            public class Example
            {
                public void Run(int value) { }

            #pragma warning disable S3776
                public void Run(string value) { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Run(stringvalue)");
    }

    [Fact]
    public void FindNewSuppressionsIn_MovedSuppressionBetweenMultilineOverloads_Fails()
    {
        const string baseline = """
            public class Example
            {
            #pragma warning disable S3776
                public void Run(
                    int value) { }
            #pragma warning restore S3776

                public void Run(
                    string value) { }
            }
            """;
        const string current = """
            public class Example
            {
                public void Run(
                    int value) { }

            #pragma warning disable S3776
                public void Run(
                    string value) { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("member:Example.Run(stringvalue)");
    }

    [Fact]
    public void CanonicalizePath_EquivalentSpellings_ReturnTheRepositoryRelativePath()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "makamek-gate-root"));

        Program.CanonicalizePath(root, "src/MakaMek.Core/A.cs").ShouldBe("src/MakaMek.Core/A.cs");
        Program.CanonicalizePath(root, "./src/MakaMek.Core/A.cs").ShouldBe("src/MakaMek.Core/A.cs");
        Program.CanonicalizePath(root, Path.Combine(root, "src", "MakaMek.Core", "A.cs"))
            .ShouldBe("src/MakaMek.Core/A.cs");
    }

    [Fact]
    public void FindNewSuppressionsIn_MovedAssemblySuppressionTarget_Fails()
    {
        const string baseline = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = "~M:Example.First")]
            """;
        const string current = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = "~M:Example.Second")]
            """;

        var failures = Program.FindNewSuppressionsIn("GlobalSuppressions.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("target:~M:Example.Second");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnresolvableSuppressionTarget_Fails()
    {
        const string source = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = nameof(Example.First))]
            """;

        var failures = Program.FindNewSuppressionsIn("GlobalSuppressions.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_ChangedAssemblySuppressionScope_Fails()
    {
        const string baseline = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = "~M:Example.First")]
            """;
        const string current = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "type", Target = "~M:Example.First")]
            """;

        var failures = Program.FindNewSuppressionsIn("GlobalSuppressions.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("scope:type");
    }

    [Fact]
    public void FindNewSuppressionsIn_ChangedAssemblySuppressionUsageTarget_Fails()
    {
        const string baseline = """
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = "~M:Example.First")]
            """;
        const string current = """
            [module: System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Scope = "member", Target = "~M:Example.First")]
            """;

        var failures = Program.FindNewSuppressionsIn("GlobalSuppressions.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("attribute:module");
    }

    [Fact]
    public void FindNewSuppressionsIn_ActivatedConditionalSuppression_Fails()
    {
        const string baseline = """
            public class Example
            {
            #if false
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;
        const string current = """
            public class Example
            {
            #if true
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("if:true:true");
    }

    [Fact]
    public void FindNewSuppressionsIn_ActivatedElseBranchSuppression_Fails()
    {
        const string baseline = """
            public class Example
            {
            #if true
            #else
            #pragma warning disable S3776
            #endif
                public void Run() { }
            #pragma warning restore S3776
            }
            """;
        const string current = """
            public class Example
            {
            #if false
            #else
            #pragma warning disable S3776
            #endif
                public void Run() { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("if:false:false/else:true");
    }

    [Fact]
    public void FindNewSuppressionsIn_ActivatedElifBranchSuppression_Fails()
    {
        const string baseline = """
            public class Example
            {
            #if true
            #elif true
            #pragma warning disable S3776
            #endif
                public void Run() { }
            #pragma warning restore S3776
            }
            """;
        const string current = """
            public class Example
            {
            #if false
            #elif true
            #pragma warning disable S3776
            #endif
                public void Run() { }
            #pragma warning restore S3776
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("if:false:false/elif:true:true");
    }

    [Fact]
    public void FindNewSuppressionsIn_SymbolDefinedInMaybeActiveBranch_KeepsSuppressionActive()
    {
        const string source = """
            #if SWITCH
            #define HIDE
            #endif

            public class Example
            {
            #if !HIDE
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("S3776");
    }

    [Fact]
    public void FindNewSuppressionsIn_SymbolDefinedUnconditionally_HidesTheSuppressedBranch()
    {
        const string source = """
            #define HIDE

            public class Example
            {
            #if !HIDE
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        Program.FindNewSuppressionsIn("Example.cs", null, source).ShouldBeEmpty();
    }

    [Fact]
    public void FindNewSuppressionsIn_SymbolDefinitionThatActivatesSuppression_Fails()
    {
        const string baseline = """
            public class Example
            {
            #if FEATURE
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;
        const string current = """
            #define FEATURE

            public class Example
            {
            #if FEATURE
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", baseline, current);

        failures.ShouldHaveSingleItem().ShouldContain("if:FEATURE:true");
    }

    [Fact]
    public void FindNewSuppressionsIn_SuppressionInInactiveSymbolBranch_IsNotRecorded()
    {
        const string source = """
            #undef FEATURE

            public class Example
            {
            #if FEATURE
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        Program.FindNewSuppressionsIn("Example.cs", null, source).ShouldBeEmpty();
    }

    [Fact]
    public void FindNewSuppressionsIn_UnchangedConditionalSuppression_Passes()
    {
        const string source = """
            public class Example
            {
            #if DEBUG
            #pragma warning disable S3776
                public void Run() { }
            #pragma warning restore S3776
            #endif
            }
            """;

        Program.FindNewSuppressionsIn("Example.cs", source, source).ShouldBeEmpty();
    }

    [Fact]
    public void FindNewSuppressionsIn_AliasedSuppressMessageAttribute_Fails()
    {
        const string source = """
            using Hidden = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

            public class Example
            {
                [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                public void Run() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_GlobalAliasFromAnotherFile_Fails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"makamek-gate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(
                Path.Combine(root, "Aliases.cs"),
                "global using Hidden = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;");
            const string source = """
                public class Example
                {
                    [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                    public void Run() { }
                }
                """;
            File.WriteAllText(Path.Combine(root, "Example.cs"), source);

            var aliases = Program.FindProjectGlobalAliases(root, "Example.csproj");

            aliases.ShouldContain("Hidden");
            Program.FindNewSuppressionsIn("Example.cs", null, source, aliases)
                .ShouldHaveSingleItem().ShouldContain("CA1506");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindNewSuppressionsIn_AliasBecomingSuppressMessage_Fails()
    {
        const string baseline = """
            public class Example
            {
                [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                public void Run() { }
            }
            """;
        const string current = """
            public class Example
            {
                // comment edit only
                [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                public void Run() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn(
            "Example.cs",
            baseline,
            current,
            projectAliases: ["Hidden"],
            baselineProjectAliases: []);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnchangedAliasWithCommentEdit_Passes()
    {
        const string baseline = """
            public class Example
            {
                [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                public void Run() { }
            }
            """;
        const string current = """
            public class Example
            {
                // comment edit only
                [Hidden("Maintainability", "CA1506:Avoid excessive class coupling")]
                public void Run() { }
            }
            """;

        Program.FindNewSuppressionsIn(
                "Example.cs",
                baseline,
                current,
                projectAliases: ["Hidden"],
                baselineProjectAliases: ["Hidden"])
            .ShouldBeEmpty();
    }

    [Fact]
    public void FindNewSuppressionsIn_NewSuppression_ReportsTheDeclarationLine()
    {
        const string current = """
            // leading comment
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("Example.cs(3)");
    }

    [Fact]
    public void FindNewSuppressionsIn_AttributeWithConstantRuleArgument_Fails()
    {
        const string current = """
            public class Example
            {
                private const string RuleId = "CA1506:Avoid excessive class coupling";

                [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
                public void Run() { }
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnqualifiedReferenceToAnotherType_Fails()
    {
        const string source = """
            using static External.RuleIds;

            namespace Local;

            internal static class OtherIds
            {
                public const string Id = "CA1822:Avoid unnecessary code";
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", Id)]
            internal class Example { }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, source);

        failures.ShouldHaveSingleItem().ShouldContain("cannot be resolved");
    }

    [Fact]
    public void FindNewSuppressionsIn_UnchangedNonLiteralAttribute_Passes()
    {
        const string source = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA" + "1506:Avoid excessive class coupling")]
            public class Example
            {
            }
            """;

        Program.FindNewSuppressionsIn("Example.cs", source, source).ShouldBeEmpty();
    }

    [Fact]
    public void FindBaselineProjectGlobalAliases_ReadsTheBaseRevision()
    {
        var root = Path.Combine(Path.GetTempPath(), $"makamek-gate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            RunGit(root, "init", "--quiet");
            RunGit(root, "config", "user.email", "gate@example.invalid");
            RunGit(root, "config", "user.name", "Gate Tests");
            File.WriteAllText(
                Path.Combine(root, "Aliases.cs"),
                "global using Hidden = Custom.NoOpAttribute;");
            File.WriteAllText(Path.Combine(root, "Example.csproj"), "<Project />");
            RunGit(root, "add", "--all");
            RunGit(root, "commit", "--quiet", "--message", "baseline");

            File.WriteAllText(
                Path.Combine(root, "Aliases.cs"),
                "global using Hidden = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;");

            Program.FindBaselineProjectGlobalAliases(root, "HEAD", "Example.csproj").ShouldBeEmpty();
            Program.FindProjectGlobalAliases(root, "Example.csproj").ShouldContain("Hidden");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is harmless.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover temp directory is harmless.
            }
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");
        }
    }

    [Fact]
    public void MaskLiteralsAndComments_PreservesLineBreaks()
    {
        const string source = """
            var open = "/*";
            var close = "*/";
            // trailing comment
            var value = 1;
            """;

        var masked = Program.MaskLiteralsAndComments(source);

        masked.Count(character => character == '\n').ShouldBe(source.Count(character => character == '\n'));
        masked.ShouldNotContain("/*");
        masked.ShouldNotContain("*/");
        masked.ShouldNotContain("trailing comment");
        masked.ShouldContain("var value = 1;");
    }
}
