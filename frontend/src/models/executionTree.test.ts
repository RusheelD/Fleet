import { describe, expect, it } from 'vitest'
import type { AgentExecution } from './agent'
import {
  collectExecutionRootsByStatus,
  executionTreeHasAnyStatus,
  findExecutionInCollection,
  normalizeExecutionTree,
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

  it('sorts sub-flows by completion phase and then work item number when normalizing the tree', () => {
    const normalized = normalizeExecutionTree(createExecution({
      id: 'parent-flow',
      executionMode: 'orchestration',
      subFlows: [
        createExecution({ id: 'sub-5', workItemId: 5, workItemTitle: 'Five', status: 'queued' }),
        createExecution({ id: 'sub-3', workItemId: 3, workItemTitle: 'Three', status: 'running' }),
        createExecution({ id: 'sub-4', workItemId: 4, workItemTitle: 'Four', status: 'failed' }),
        createExecution({ id: 'sub-2', workItemId: 2, workItemTitle: 'Two', status: 'completed' }),
        createExecution({ id: 'sub-6', workItemId: 6, workItemTitle: 'Six', status: 'queued' }),
        createExecution({ id: 'sub-1', workItemId: 1, workItemTitle: 'One', status: 'completed' }),
      ],
    }))

    expect(normalized.subFlows?.map((execution) => execution.workItemId)).toEqual([1, 2, 3, 4, 5, 6])
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

  it('collects terminal render roots without hiding matching sub-flows under non-matching parents', () => {
    const completedGrandchild = createExecution({
      id: 'completed-grandchild',
      workItemId: 4,
      workItemTitle: 'Completed grandchild',
      status: 'completed',
      parentExecutionId: 'completed-child',
    })
    const completedChild = createExecution({
      id: 'completed-child',
      workItemId: 3,
      workItemTitle: 'Completed child',
      status: 'completed',
      parentExecutionId: 'failed-parent',
      subFlows: [completedGrandchild],
    })
    const completedParent = createExecution({
      id: 'completed-parent',
      workItemId: 1,
      workItemTitle: 'Completed parent',
      status: 'completed',
      subFlows: [
        createExecution({
          id: 'completed-parent-child',
          workItemId: 2,
          workItemTitle: 'Completed parent child',
          status: 'completed',
          parentExecutionId: 'completed-parent',
        }),
      ],
    })
    const failedParent = createExecution({
      id: 'failed-parent',
      workItemId: 5,
      workItemTitle: 'Failed parent',
      status: 'failed',
      subFlows: [completedChild],
    })

    const renderRoots = collectExecutionRootsByStatus([completedParent, failedParent], ['completed'])

    expect(renderRoots.map((execution) => execution.id)).toEqual(['completed-parent', 'completed-child'])
  })
})
