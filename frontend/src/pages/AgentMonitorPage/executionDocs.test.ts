import { describe, expect, it } from 'vitest'
import { buildExecutionDocumentationFileName, hasExecutionDocumentation, normalizeExecutionDocumentationMarkdown } from './executionDocs'

describe('execution documentation helpers', () => {
  it('detects when markdown content exists', () => {
    expect(hasExecutionDocumentation({
      executionId: 'abc123',
      title: 'Run Docs',
      markdown: '## Summary',
      pullRequestUrl: null,
      diffUrl: null,
    })).toBe(true)

    expect(hasExecutionDocumentation({
      executionId: 'abc123',
      title: 'Run Docs',
      markdown: '   ',
      pullRequestUrl: null,
      diffUrl: null,
    })).toBe(false)
  })

  it('builds a stable markdown download filename', () => {
    expect(buildExecutionDocumentationFileName({
      executionId: 'abc123',
      title: 'Execution 123: Fix OAuth + PR Flow',
      markdown: '# Docs',
      pullRequestUrl: null,
      diffUrl: null,
    })).toBe('fleet-execution-123-fix-oauth-pr-flow.md')
  })

  it('unwraps fenced markdown agent output while preserving code fences', () => {
    const markdown = `
## Generated Documentation

\`\`\`markdown
# Summary

- Added token handling

\`\`\`ts
const ok = true
\`\`\`
\`\`\`
`.trim()

    expect(normalizeExecutionDocumentationMarkdown(markdown)).toContain('# Summary')
    expect(normalizeExecutionDocumentationMarkdown(markdown)).toContain('```ts')
    expect(normalizeExecutionDocumentationMarkdown(markdown)).not.toContain('```markdown')
  })

  it('keeps fenced tree-style text output as a code block', () => {
    const markdown = `
## Phase Outputs

\`\`\`text
src/
  components/
    Button.tsx
\`\`\`
`.trim()

    expect(normalizeExecutionDocumentationMarkdown(markdown)).toContain('```text')
    expect(normalizeExecutionDocumentationMarkdown(markdown)).toContain('src/')
  })
})
