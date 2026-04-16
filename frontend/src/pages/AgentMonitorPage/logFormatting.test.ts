import { describe, expect, it } from 'vitest'
import { formatLogAgentLabel, parseStructuredLogMessage } from './logFormatting'

describe('parseStructuredLogMessage', () => {
  it('parses orchestration failure summaries into a richer structure', () => {
    const parsed = parseStructuredLogMessage(
      'Phase failed on attempt 3/3: Azure OpenAI blocked this phase prompt because it was flagged as a potential jailbreak in prompt content.',
      'error',
    )

    expect(parsed).not.toBeNull()
    expect(parsed?.kind).toBe('summary')
    expect(parsed?.headline).toBe('Phase failed on attempt 3/3')
    expect(parsed?.body).toContain('Azure OpenAI blocked this phase prompt')
    expect(parsed?.badges.map((badge) => badge.label)).toEqual(['Azure OpenAI', 'Jailbreak'])
  })

  it('parses provider diagnostics into metadata and a readable provider message', () => {
    const parsed = parseStructuredLogMessage(
      'Provider diagnostics: status=400; code=content_filter; type=invalid_request_error; param=prompt; source=prompt; blocked_filters=jailbreak; offsets=0-5608; provider_message=The response was filtered due to the prompt triggering Azure OpenAI content policy.',
      'warn',
    )

    expect(parsed).not.toBeNull()
    expect(parsed?.kind).toBe('diagnostics')
    expect(parsed?.headline).toBe('Provider diagnostics')
    expect(parsed?.body).toContain('filtered due to the prompt')
    expect(parsed?.metadata).toEqual([
      { key: 'Status', value: '400' },
      { key: 'Code', value: 'content_filter' },
      { key: 'Type', value: 'invalid_request_error' },
      { key: 'Param', value: 'prompt' },
      { key: 'Source', value: 'prompt' },
      { key: 'Blocked Filters', value: 'jailbreak' },
      { key: 'Offsets', value: '0-5608' },
    ])
  })

  it('leaves simple status logs alone', () => {
    expect(parseStructuredLogMessage('Status update: Working via read_file (step 3)', 'info')).toBeNull()
  })

  it('adds flow context to agent labels when requested', () => {
    expect(formatLogAgentLabel('Planner', { workItemId: 4, parentExecutionId: null }, true)).toBe('Flow #4 · Planner')
    expect(formatLogAgentLabel('Backend', { workItemId: 7, parentExecutionId: 'parent-1' }, true)).toBe('Sub-flow #7 · Backend')
    expect(formatLogAgentLabel('System', null, true)).toBe('System')
  })
})
