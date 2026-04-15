import { describe, expect, it } from 'vitest'
import type { AgentExecution } from './agent'
import {
  executionTreeHasAnyStatus,
  findExecutionInCollection,
  upsertExecutionCollectionWithFallback,
} from './executionTree'

function createExecution(overrides: Partial<AgentExecution> = {}): AgentExecution {
  return {
    id: overrides.id ?? 'execution-1',
    workItemId: overrides.workItemId ?? 1,
    workItemTitle: overrides.workItemTitle ?? 'Execution',
    executionMode: overrides.executionMode ?? 'standard',
    status: overrides.status ?? 'running',
    agents: overrides.agents ?? [],
    startedAt: overrides.startedAt ?? '2026-04-15T00:00:00.000Z',
    duration: overrides.duration ?? '1m',
    progress: overrides.progress ?? 0.2,
    branchName: overrides.branchName ?? null,
    pullRequestUrl: overrides.pullRequestUrl ?? null,
    currentPhase: overrides.currentPhase ?? 'Working',
    reviewLoopCount: overrides.reviewLoopCount ?? 0,
    lastReviewRecommendation: overrides.lastReviewRecommendation ?? null,
    parentExecutionId: overrides.parentExecutionId ?? null,
    subFlows: overrides.subFlows ?? [],
  }
}

describe('executionTree helpers', () => {
  it('finds nested executions inside the current tree', () => {
    const child = createExecution({
      id: 'child-retry',
      workItemId: 2,
      workItemTitle: 'Child retry',
      parentExecutionId: 'parent-flow',
    })
    const executions = [
      createExecution({
        id: 'parent-flow',
        subFlows: [child],
      }),
    ]

    expect(findExecutionInCollection(executions, 'child-retry')).toEqual(child)
  })

  it('keeps a child retry visible when it arrives before its new parent snapshot', () => {
    const incomingChild = createExecution({
      id: 'child-retry',
      workItemId: 2,
      workItemTitle: 'Child retry',
      status: 'queued',
      parentExecutionId: 'new-parent',
    })

    const updated = upsertExecutionCollectionWithFallback([], incomingChild)

    expect(updated.insertedAsFallback).toBe(true)
    expect(updated.executions).toHaveLength(1)
    expect(updated.executions[0].id).toBe('child-retry')
  })

  it('moves an orphaned child retry under the real parent once that parent snapshot arrives', () => {
    const orphanedChild = createExecution({
      id: 'child-retry',
      workItemId: 2,
      workItemTitle: 'Child retry',
      status: 'running',
      parentExecutionId: 'new-parent',
    })
    const withFallback = upsertExecutionCollectionWithFallback([], orphanedChild)

    const incomingParent = createExecution({
      id: 'new-parent',
      workItemId: 1,
      workItemTitle: 'New parent retry',
      executionMode: 'orchestration',
      status: 'running',
      subFlows: [orphanedChild],
    })

    const nested = upsertExecutionCollectionWithFallback(withFallback.executions, incomingParent)

    expect(nested.executions).toHaveLength(1)
    expect(nested.executions[0].id).toBe('new-parent')
    expect(nested.executions[0].subFlows).toHaveLength(1)
    expect(nested.executions[0].subFlows?.[0].id).toBe('child-retry')
  })

  it('treats descendant live retries as active tree members', () => {
    const execution = createExecution({
      id: 'failed-parent',
      executionMode: 'orchestration',
      status: 'failed',
      subFlows: [
        createExecution({
          id: 'running-child-retry',
          workItemId: 2,
          workItemTitle: 'Running child retry',
          status: 'running',
          parentExecutionId: 'failed-parent',
        }),
      ],
    })

    expect(executionTreeHasAnyStatus(execution, ['running', 'queued'])).toBe(true)
  })
})
