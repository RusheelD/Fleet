import type { AgentExecution } from './agent'
import { compareFlowDisplayOrder } from './flowDisplayOrder'

function compareSubFlowExecutions(left: AgentExecution, right: AgentExecution): number {
  return compareFlowDisplayOrder(
    {
      status: left.status,
      flowNumber: left.workItemId,
      startedAt: left.startedAt,
      title: left.workItemTitle,
      uniqueId: left.id,
    },
    {
      status: right.status,
      flowNumber: right.workItemId,
      startedAt: right.startedAt,
      title: right.workItemTitle,
      uniqueId: right.id,
    },
  )
}

function sortSubFlowExecutions(executions: AgentExecution[]): AgentExecution[] {
  return [...executions]
    .map((execution) => ({
      ...execution,
      subFlows: sortSubFlowExecutions(execution.subFlows ?? []),
    }))
    .sort(compareSubFlowExecutions)
}

export function sortExecutionCollectionByDisplayOrder(executions: AgentExecution[]): AgentExecution[] {
  return [...executions].sort(compareSubFlowExecutions)
}

function collectExecutionIds(execution: AgentExecution, ids: Set<string>) {
  ids.add(execution.id)

  for (const child of execution.subFlows ?? []) {
    collectExecutionIds(child, ids)
  }
}

function pruneNestedDuplicates(executions: AgentExecution[]): AgentExecution[] {
  const descendantIds = new Set<string>()

  for (const execution of executions) {
    for (const child of execution.subFlows ?? []) {
      collectExecutionIds(child, descendantIds)
    }
  }

  return executions
    .filter((execution) => !descendantIds.has(execution.id))
    .map((execution) => ({
      ...execution,
      subFlows: pruneNestedDuplicates(execution.subFlows ?? []),
    }))
    .sort(compareSubFlowExecutions)
}

function hasExecutionStatus(
  execution: AgentExecution,
  statuses: ReadonlySet<AgentExecution['status']>,
): boolean {
  if (statuses.has(execution.status)) {
    return true
  }

  return (execution.subFlows ?? []).some((child) => hasExecutionStatus(child, statuses))
}

export function normalizeExecutionTree(execution: AgentExecution): AgentExecution {
  return {
    ...execution,
    subFlows: sortSubFlowExecutions((execution.subFlows ?? []).map(normalizeExecutionTree)),
  }
}

export function flattenExecutionCollection(executions: AgentExecution[]): AgentExecution[] {
  const flattened: AgentExecution[] = []

  const visit = (execution: AgentExecution) => {
    flattened.push(execution)

    for (const child of execution.subFlows ?? []) {
      visit(child)
    }
  }

  for (const execution of executions) {
    visit(execution)
  }

  return flattened
}

export function collectExecutionRootsByStatus(
  executions: AgentExecution[],
  statuses: Iterable<AgentExecution['status']>,
): AgentExecution[] {
  const statusSet = statuses instanceof Set ? statuses : new Set(statuses)
  const collected: AgentExecution[] = []

  const visit = (execution: AgentExecution, ancestorMatches: boolean) => {
    const matches = statusSet.has(execution.status)
    if (matches && !ancestorMatches) {
      collected.push(execution)
    }

    for (const child of execution.subFlows ?? []) {
      visit(child, ancestorMatches || matches)
    }
  }

  for (const execution of executions) {
    visit(execution, false)
  }

  return sortExecutionCollectionByDisplayOrder(collected)
}

export function findExecutionInCollection(
  executions: AgentExecution[],
  executionId: string,
): AgentExecution | undefined {
  for (const execution of executions) {
    if (execution.id === executionId) {
      return execution
    }

    const nestedMatch = findExecutionInCollection(execution.subFlows ?? [], executionId)
    if (nestedMatch) {
      return nestedMatch
    }
  }

  return undefined
}

export function mergeExecutionSnapshot(existing: AgentExecution, incoming: AgentExecution): AgentExecution {
  const incomingChildren = incoming.subFlows ?? []
  const mergedChildren = incomingChildren.length > 0 ? incomingChildren : existing.subFlows ?? incomingChildren

  return {
    ...incoming,
    branchName: incoming.branchName ?? existing.branchName,
    pullRequestUrl: incoming.pullRequestUrl ?? existing.pullRequestUrl,
    currentPhase: incoming.currentPhase ?? existing.currentPhase,
    reviewLoopCount: incoming.reviewLoopCount && incoming.reviewLoopCount > 0
      ? incoming.reviewLoopCount
      : existing.reviewLoopCount,
    lastReviewRecommendation: incoming.lastReviewRecommendation ?? existing.lastReviewRecommendation,
    subFlows: sortSubFlowExecutions(mergedChildren),
  }
}

export function upsertExecutionCollection(
  current: AgentExecution[],
  incoming: AgentExecution,
): { executions: AgentExecution[]; found: boolean } {
  let found = false

  const executions = current.map((execution) => {
    if (execution.id === incoming.id) {
      found = true
      return mergeExecutionSnapshot(execution, incoming)
    }

    if (incoming.parentExecutionId && execution.id === incoming.parentExecutionId) {
      found = true
      const existingChildren = execution.subFlows ?? []
      const existingChildIndex = existingChildren.findIndex((child) => child.id === incoming.id)
      const nextChildren = existingChildIndex >= 0
        ? existingChildren.map((child, index) => (
          index === existingChildIndex ? mergeExecutionSnapshot(child, incoming) : child
        ))
        : [incoming, ...existingChildren]

      return {
        ...execution,
        subFlows: pruneNestedDuplicates(nextChildren),
      }
    }

    if ((execution.subFlows?.length ?? 0) > 0) {
      const nested = upsertExecutionCollection(execution.subFlows ?? [], incoming)
      if (nested.found) {
        found = true
        return {
          ...execution,
          subFlows: pruneNestedDuplicates(nested.executions),
        }
      }
    }

    return execution
  })

  return { executions, found }
}

export function upsertExecutionCollectionWithFallback(
  current: AgentExecution[],
  incoming: AgentExecution,
): { executions: AgentExecution[]; found: boolean; insertedAsFallback: boolean } {
  const updated = upsertExecutionCollection(current, incoming)
  if (updated.found) {
    return {
      executions: pruneNestedDuplicates(updated.executions),
      found: true,
      insertedAsFallback: false,
    }
  }

  return {
    executions: pruneNestedDuplicates([incoming, ...current]),
    found: false,
    insertedAsFallback: true,
  }
}

export function removeExecutionCollection(
  current: AgentExecution[],
  executionId: string,
): { executions: AgentExecution[]; removed: boolean } {
  let removed = false

  const executions = current
    .filter((execution) => {
      if (execution.id === executionId) {
        removed = true
        return false
      }

      return true
    })
    .map((execution) => {
      if ((execution.subFlows?.length ?? 0) === 0) {
        return execution
      }

      const nested = removeExecutionCollection(execution.subFlows ?? [], executionId)
      if (!nested.removed) {
        return execution
      }

      removed = true
      return {
        ...execution,
        subFlows: nested.executions,
      }
    })

  return { executions, removed }
}

export function patchExecutionCollection(
  current: AgentExecution[],
  executionId: string,
  patch: Partial<AgentExecution>,
): { executions: AgentExecution[]; found: boolean } {
  let found = false

  const executions = current.map((execution) => {
    if (execution.id === executionId) {
      found = true
      return {
        ...execution,
        ...patch,
        subFlows: execution.subFlows,
      }
    }

    if ((execution.subFlows?.length ?? 0) === 0) {
      return execution
    }

    const nested = patchExecutionCollection(execution.subFlows ?? [], executionId, patch)
    if (!nested.found) {
      return execution
    }

    found = true
    return {
      ...execution,
      subFlows: nested.executions,
    }
  })

  return { executions, found }
}

export function executionTreeHasAnyStatus(
  execution: AgentExecution,
  statuses: Iterable<AgentExecution['status']>,
): boolean {
  return hasExecutionStatus(execution, statuses instanceof Set ? statuses : new Set(statuses))
}
