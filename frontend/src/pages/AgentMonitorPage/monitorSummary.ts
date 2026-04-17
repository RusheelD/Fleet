import type { AgentExecution } from '../../models/agent'
import { flattenExecutionCollection } from '../../models/executionTree'

const ACTIVE_EXECUTION_STATUSES = new Set<AgentExecution['status']>(['running', 'queued'])

export function countActiveFlows(executions: AgentExecution[]): number {
    return flattenExecutionCollection(executions)
        .filter((execution) => ACTIVE_EXECUTION_STATUSES.has(execution.status))
        .length
}

export function countActiveAgents(executions: AgentExecution[]): number {
    return flattenExecutionCollection(executions).reduce((count, execution) => {
        const runningAgents = execution.agents.filter((agent) => agent.status === 'running').length
        if (runningAgents > 0) {
            return count + runningAgents
        }

        return ACTIVE_EXECUTION_STATUSES.has(execution.status)
            ? count + 1
            : count
    }, 0)
}
