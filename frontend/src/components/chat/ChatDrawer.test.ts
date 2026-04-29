import { describe, expect, it } from 'vitest'
import {
  applySessionOptimisticState,
  canSubmitChatMessage,
  resolveActiveChatSessionId,
  resolveContentToSend,
  resolveServerMessagesForActiveSession,
  shouldShowChatLoading,
} from './chatDrawerHelpers'
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

describe('ChatDrawer session selection helpers', () => {
  it('keeps the selected session when it still exists', () => {
    const sessions = [
      createSession({ id: 'old-session', isActive: true }),
      createSession({ id: 'selected-session', isActive: false }),
    ]

    expect(resolveActiveChatSessionId(sessions, 'selected-session')).toBe('selected-session')
  })

  it('prefers the server active session when the current selection is missing', () => {
    const sessions = [
      createSession({ id: 'first-session', isActive: false }),
      createSession({ id: 'active-session', isActive: true }),
    ]

    expect(resolveActiveChatSessionId(sessions, 'missing-session')).toBe('active-session')
  })

  it('does not reuse chat-data messages for a different active session', () => {
    const staleChatDataMessages = [{
      id: 'old-message',
      role: 'user' as const,
      content: 'old session',
      timestamp: '2026-01-01T00:00:00Z',
    }]

    expect(resolveServerMessagesForActiveSession(
      undefined,
      staleChatDataMessages,
      'new-session',
      'old-session',
    )).toEqual([])
  })

  it('uses chat-data messages while viewing the chat-data active session', () => {
    const chatDataMessages = [{
      id: 'active-message',
      role: 'assistant' as const,
      content: 'active session',
      timestamp: '2026-01-01T00:00:00Z',
    }]

    expect(resolveServerMessagesForActiveSession(
      undefined,
      chatDataMessages,
      'active-session',
      'active-session',
    )).toEqual(chatDataMessages)
  })
})

describe('ChatDrawer loading guardrails', () => {
  it('does not show the chat loader over an optimistic first message', () => {
    const optimisticMessage = {
      id: 'optimistic-1',
      role: 'user' as const,
      content: 'hello',
      timestamp: '2026-01-01T00:00:00Z',
    }

    expect(shouldShowChatLoading(true, [optimisticMessage])).toBe(false)
  })

  it('shows the chat loader only while loading with no visible messages', () => {
    expect(shouldShowChatLoading(true, [])).toBe(true)
    expect(shouldShowChatLoading(false, [])).toBe(false)
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
