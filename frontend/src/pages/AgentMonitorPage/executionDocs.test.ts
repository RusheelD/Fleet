import { describe, expect, it } from 'vitest'
import { buildExecutionDocumentationFileName, hasExecutionDocumentation } from './executionDocs'

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
})
