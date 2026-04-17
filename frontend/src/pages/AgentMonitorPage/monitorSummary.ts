import type { AgentExecution } from '../../models/agent'
import { sortExecutionCollectionByDisplayOrder } from '../../models/executionTree'

const ACTIVE_EXECUTION_STATUSES = new Set<AgentExecution['status']>(['running', 'queued'])
const PAUSED_EXECUTION_STATUSES = new Set<AgentExecution['status']>(['paused'])

export interface MonitorExecutionSummary {
    activeRoots: AgentExecution[]
    pausedRoots: AgentExecution[]
    completedRoots: AgentExecution[]
    failedRoots: AgentExecution[]
    cancelledRoots: AgentExecution[]
    activeFlowCount: number
    activeAgentCount: number
}

interface TraversalSnapshot {
    hasActive: boolean
    hasPaused: boolean
}

function summarizeExecution(
    execution: AgentExecution,
    activeRoots: AgentExecution[],
    pausedRoots: AgentExecution[],
    completedRoots: AgentExecution[],
    failedRoots: AgentExecution[],
    cancelledRoots: AgentExecution[],
    counts: { activeFlows: number; activeAgents: number },
    ancestorMatchedStatuses: ReadonlySet<AgentExecution['status']>,
): TraversalSnapshot {
    const childAncestorMatchedStatuses = new Set(ancestorMatchedStatuses)

    if (execution.status === 'completed' && !ancestorMatchedStatuses.has('completed')) {
        completedRoots.push(execution)
        childAncestorMatchedStatuses.add('completed')
    }

    if (execution.status === 'failed' && !ancestorMatchedStatuses.has('failed')) {
        failedRoots.push(execution)
        childAncestorMatchedStatuses.add('failed')
    }

    if (execution.status === 'cancelled' && !ancestorMatchedStatuses.has('cancelled')) {
        cancelledRoots.push(execution)
        childAncestorMatchedStatuses.add('cancelled')
    }

    const isActive = ACTIVE_EXECUTION_STATUSES.has(execution.status)
    const isPaused = PAUSED_EXECUTION_STATUSES.has(execution.status)

    if (isActive) {
        counts.activeFlows += 1
    }

    const runningAgents = execution.agents.filter((agent) => agent.status === 'running').length
    if (runningAgents > 0) {
        counts.activeAgents += runningAgents
    } else if (isActive) {
        counts.activeAgents += 1
    }

    let hasActive = isActive
    let hasPaused = isPaused

    for (const child of execution.subFlows ?? []) {
        const childSummary = summarizeExecution(
            child,
            activeRoots,
            pausedRoots,
            completedRoots,
            failedRoots,
            cancelledRoots,
            counts,
            childAncestorMatchedStatuses,
        )
        hasActive = hasActive || childSummary.hasActive
        hasPaused = hasPaused || childSummary.hasPaused
    }

    if (hasActive && !execution.parentExecutionId) {
        activeRoots.push(execution)
    } else if (hasPaused && !hasActive && !execution.parentExecutionId) {
        pausedRoots.push(execution)
    }

    return { hasActive, hasPaused }
}

export function buildMonitorExecutionSummary(executions: AgentExecution[]): MonitorExecutionSummary {
    const activeRoots: AgentExecution[] = []
    const pausedRoots: AgentExecution[] = []
    const completedRoots: AgentExecution[] = []
    const failedRoots: AgentExecution[] = []
    const cancelledRoots: AgentExecution[] = []
    const counts = { activeFlows: 0, activeAgents: 0 }

    for (const execution of executions) {
        summarizeExecution(
            execution,
            activeRoots,
            pausedRoots,
            completedRoots,
            failedRoots,
            cancelledRoots,
            counts,
            new Set<AgentExecution['status']>(),
        )
    }

    return {
        activeRoots: sortExecutionCollectionByDisplayOrder(activeRoots),
        pausedRoots: sortExecutionCollectionByDisplayOrder(pausedRoots),
        completedRoots: sortExecutionCollectionByDisplayOrder(completedRoots),
        failedRoots: sortExecutionCollectionByDisplayOrder(failedRoots),
        cancelledRoots: sortExecutionCollectionByDisplayOrder(cancelledRoots),
        activeFlowCount: counts.activeFlows,
        activeAgentCount: counts.activeAgents,
    }
}

export function countActiveFlows(executions: AgentExecution[]): number {
    return buildMonitorExecutionSummary(executions).activeFlowCount
}

export function countActiveAgents(executions: AgentExecution[]): number {
    return buildMonitorExecutionSummary(executions).activeAgentCount
}

export function formatCountLabel(count: number, singular: string, plural = `${singular}s`): string {
    return count === 1 ? singular : plural
}
