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

        scopes.ShouldContainKey(("CA1506", "member:Run"));
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

        scopes.ShouldContainKey(("CA1501", "member:Run"));
        scopes.ShouldContainKey(("CA1502", "member:Run"));
        scopes.ShouldContainKey(("CA1505", "member:Run"));
        scopes.ShouldContainKey(("CA1506", "member:Run"));
        scopes.ShouldContainKey(("S3776", "member:Run"));
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

        failures.ShouldHaveSingleItem().ShouldContain("member:Second");
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

        failures.ShouldHaveSingleItem().ShouldContain("member:Second");
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

        failures.ShouldHaveSingleItem().ShouldContain("member:Second");
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

        scopes.ShouldContainKey(("S3776", "member:Run"));
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
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
            public class Example
            {
                private const string RuleId = "CA1822:Avoid unnecessary code";
            }
            """;
        const string current = """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
            public class Example
            {
                private const string RuleId = "CA1506:Avoid excessive class coupling";
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
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", RuleId)]
            public class Example
            {
                private const string RuleId = "CA1506:Avoid excessive class coupling";
            }
            """;

        var failures = Program.FindNewSuppressionsIn("Example.cs", null, current);

        failures.ShouldHaveSingleItem().ShouldContain("CA1506");
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
