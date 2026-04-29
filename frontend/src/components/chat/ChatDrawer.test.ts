import { describe, expect, it } from 'vitest'
import { applySessionOptimisticState, canSubmitChatMessage, resolveContentToSend } from './chatDrawerHelpers'
import type { ChatSessionData } from '../../models'

function createSession(overrides: Partial<ChatSessionData> = {}): ChatSessionData {
  return {
    id: 'sess-1',
    title: 'Session',
    lastMessage: 'hi',
    timestamp: '2026-01-01T00:00:00Z',
    isActive: true,
    isGenerating: false,
    generationState: 'idle',
    generationStatus: null,
    generationUpdatedAtUtc: null,
    recentActivity: [],
    branchStrategy: 'AutoFromProjectPattern',
    sessionPinnedBranch: null,
    inheritParentBranchForSubFlows: true,
    isDynamicIterationEnabled: false,
    dynamicIterationBranch: null,
    dynamicIterationPolicyJson: null,
    ...overrides,
  }
}

describe('ChatDrawer mode helpers', () => {
  it('uses the default generation prompt when generate mode is selected with empty input', () => {
    const content = resolveContentToSend('', true)

    expect(content).toContain('Generate work-items based on provided context')
  })

  it('does not substitute a backlog prompt for an empty dynamic iteration request', () => {
    expect(resolveContentToSend('', true, true)).toBe('')
  })

  it('preserves typed input in normal mode', () => {
    expect(resolveContentToSend('hello', false)).toBe('hello')
  })

  it('requires explicit user intent before submitting dynamic iteration', () => {
    expect(canSubmitChatMessage('', true, true)).toBe(false)
    expect(canSubmitChatMessage('Fix the auth retry bug', true, true)).toBe(true)
  })

  it('still allows empty backlog generation outside dynamic iteration', () => {
    expect(canSubmitChatMessage('', true, false)).toBe(true)
  })
})

describe('ChatDrawer optimistic generation status', () => {
  it('marks idle sessions as running when optimistic generation starts', () => {
    const session = createSession()

    const updated = applySessionOptimisticState(session, {
      optimisticGeneratingSessionIds: ['sess-1'],
      isCancelingSession: false,
    })

    expect(updated.isGenerating).toBe(true)
    expect(updated.generationState).toBe('running')
    expect(updated.generationStatus).toBe('Preparing work-item generation...')
  })

  it('uses dynamic iteration language for optimistic dynamic runs', () => {
    const session = createSession({ isDynamicIterationEnabled: true })

    const updated = applySessionOptimisticState(session, {
      optimisticGeneratingSessionIds: ['sess-1'],
      isCancelingSession: false,
      isDynamicIterationSession: true,
    })

    expect(updated.isGenerating).toBe(true)
    expect(updated.generationState).toBe('running')
    expect(updated.generationStatus).toBe('Preparing dynamic iteration...')
  })

  it('shows canceling status when cancel mutation is pending', () => {
    const session = createSession({ isGenerating: true, generationState: 'running', generationStatus: 'Working...' })

    const updated = applySessionOptimisticState(session, {
      optimisticGeneratingSessionIds: [],
      isCancelingSession: true,
    })

    expect(updated.isGenerating).toBe(true)
    expect(updated.generationState).toBe('canceling')
    expect(updated.generationStatus).toBe('Canceling generation...')
  })
})
