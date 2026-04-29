import { describe, expect, it } from 'vitest'
import type { ChatSessionData } from '../../models'
import {
  applyDynamicOptionsToChatSession,
  mergeOptimisticChatSessions,
  removeOptimisticSessionsPresentOnServer,
  upsertOptimisticChatSession,
} from './chatSessionOptimism'

function createSession(overrides: Partial<ChatSessionData> = {}): ChatSessionData {
  return {
    id: 'sess-1',
    title: 'Session',
    lastMessage: '',
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

describe('chat session optimism', () => {
  it('keeps a newly created session visible until chat data catches up', () => {
    const optimistic = createSession({ id: 'new-session' })
    const server = createSession({ id: 'old-session' })

    expect(mergeOptimisticChatSessions([server], [optimistic]).map((session) => session.id))
      .toEqual(['new-session', 'old-session'])
  })

  it('removes optimistic sessions once the server returns them', () => {
    const optimistic = createSession({ id: 'new-session' })
    const server = createSession({ id: 'new-session', title: 'Server session' })

    expect(removeOptimisticSessionsPresentOnServer([optimistic], [server])).toEqual([])
    expect(mergeOptimisticChatSessions([server], [optimistic])).toEqual([server])
  })

  it('upserts optimistic sessions without duplicating ids', () => {
    const first = createSession({ id: 'new-session', title: 'First title' })
    const second = createSession({ id: 'new-session', title: 'Second title' })

    expect(upsertOptimisticChatSession([first], second)).toEqual([second])
  })

  it('applies dynamic iteration settings to a just-created optimistic session', () => {
    const session = applyDynamicOptionsToChatSession(createSession(), {
      enabled: true,
      branchName: ' feature/auth ',
      strategy: 'parallel',
    })

    expect(session.isDynamicIterationEnabled).toBe(true)
    expect(session.dynamicIterationBranch).toBe('feature/auth')
    expect(session.dynamicIterationPolicyJson).toBe('{"executionPolicy":"parallel"}')
    expect(session.dynamicOptions).toEqual({
      enabled: true,
      branchName: 'feature/auth',
      strategy: 'parallel',
    })
  })
})
