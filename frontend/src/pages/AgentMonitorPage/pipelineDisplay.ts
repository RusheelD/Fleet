import type { AgentExecution, AgentInfo } from '../../models'

export type ExecutionStepStatus = AgentInfo['status'] | 'paused' | 'queued'

export type DisplayAgentInfo = Omit<AgentInfo, 'status'> & { status: ExecutionStepStatus }

export type PlannedSubFlowStep = {
    workItemNumber: number
    title: string
    status: ExecutionStepStatus
    currentTask: string
    progress: number
}

export type PipelineDisplayStep =
    | {
        key: string
        kind: 'agent'
        title: string
        status: ExecutionStepStatus
        currentTask: string
        progress: number
      }
    | {
        key: string
        kind: 'subflow'
        title: string
        status: ExecutionStepStatus
        currentTask: string
        progress: number
      }

function getBaseRoleName(role: string): string {
    return role.replace(/\s+#\d+$/, '').trim()
}

function findLastRoleIndex(agents: DisplayAgentInfo[], roleName: string): number {
    for (let index = agents.length - 1; index >= 0; index -= 1) {
        if (getBaseRoleName(agents[index].role) === roleName) {
            return index
        }
    }

    return -1
}

function findFirstRoleIndexAfter(agents: DisplayAgentInfo[], roleName: string, startIndex: number): number {
    for (let index = Math.max(0, startIndex); index < agents.length; index += 1) {
        if (getBaseRoleName(agents[index].role) === roleName) {
            return index
        }
    }

    return -1
}

function buildAgentSteps(agents: DisplayAgentInfo[]): PipelineDisplayStep[] {
    return agents.map((agent) => ({
        key: `agent-${agent.role}`,
        kind: 'agent' as const,
        title: agent.role,
        status: agent.status,
        currentTask: agent.currentTask,
        progress: agent.progress,
    }))
}

function buildSubFlowSteps(subFlows: PlannedSubFlowStep[]): PipelineDisplayStep[] {
    return subFlows.map((subFlowStep) => ({
        key: `subflow-${subFlowStep.workItemNumber}`,
        kind: 'subflow' as const,
        title: `Sub-flow #${subFlowStep.workItemNumber}`,
        status: subFlowStep.status,
        currentTask: subFlowStep.currentTask,
        progress: subFlowStep.progress,
    }))
}

export function buildPipelineDisplaySteps(
    executionMode: AgentExecution['executionMode'],
    agents: DisplayAgentInfo[],
    plannedSubFlowSteps: PlannedSubFlowStep[],
): PipelineDisplayStep[] {
    if (executionMode !== 'orchestration' || plannedSubFlowSteps.length === 0) {
        return buildAgentSteps(agents)
    }

    const lastContractsIndex = findLastRoleIndex(agents, 'Contracts')
    const firstConsolidationIndex = findFirstRoleIndexAfter(
        agents,
        'Consolidation',
        lastContractsIndex >= 0 ? lastContractsIndex + 1 : 0,
    )

    let insertionIndex = 0
    if (lastContractsIndex >= 0) {
        insertionIndex = lastContractsIndex + 1
    } else {
        const lastPreludeIndex = Math.max(
            findLastRoleIndex(agents, 'Planner'),
            findLastRoleIndex(agents, 'Manager'),
        )
        insertionIndex = lastPreludeIndex >= 0 ? lastPreludeIndex + 1 : 0
    }

    const preSubFlowAgents = agents.slice(0, insertionIndex)
    const postSubFlowAgents = firstConsolidationIndex >= 0
        ? agents.slice(firstConsolidationIndex)
        : agents.slice(insertionIndex)

    return [
        ...buildAgentSteps(preSubFlowAgents),
        ...buildSubFlowSteps(plannedSubFlowSteps),
        ...buildAgentSteps(postSubFlowAgents),
    ]
}
