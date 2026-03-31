using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ExecutionDocumentationFormatterTests
{
    [TestMethod]
    public void FormatPhaseOutput_UnwrapsMarkdownLikeTextBlocks()
    {
        var output = """
            ```text
            ## Files Changed

            - Updated docs rendering

            ```ts
            const ok = true;
            ```
            ```
            """;

        var formatted = ExecutionDocumentationFormatter.FormatPhaseOutput(output);

        StringAssert.Contains(formatted, "## Files Changed");
        StringAssert.Contains(formatted, "```ts");
        Assert.IsFalse(formatted.Contains("```text"), "Markdown-like phase output should not stay wrapped in a text fence.");
    }

    [TestMethod]
    public void FormatPhaseOutput_PreservesTreeLikeOutputAsText()
    {
        var output = """
            src/
              components/
                Button.tsx
            """;

        var formatted = ExecutionDocumentationFormatter.FormatPhaseOutput(output);

        Assert.IsTrue(formatted.StartsWith("```text", StringComparison.Ordinal));
        StringAssert.Contains(formatted, "src/");
    }

    [TestMethod]
    public void NormalizeMarkdown_UnwrapsNestedMarkdownBlocks()
    {
        var output = """
            ```output
            ```markdown
            # Summary

            - Added execution docs cleanup
            ```
            ```
            """;

        var normalized = ExecutionDocumentationFormatter.NormalizeMarkdown(output);

        StringAssert.Contains(normalized, "# Summary");
        Assert.IsFalse(normalized.Contains("```output"), "Wrapper fences should be removed when the content is markdown.");
        Assert.IsFalse(normalized.Contains("```markdown"), "Embedded markdown fences should be removed.");
    }
}
