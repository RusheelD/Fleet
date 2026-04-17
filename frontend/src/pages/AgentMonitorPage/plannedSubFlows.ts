import type { AgentExecution, WorkItem } from '../../models'
import { compareFlowDisplayOrder } from '../../models/flowDisplayOrder'
import type { ExecutionStepStatus, PlannedSubFlowStep } from './pipelineDisplay'

const NON_ACTIONABLE_SUBFLOW_STATES = new Set<WorkItem['state']>([
    'In-PR',
    'In-PR (AI)',
    'Resolved',
    'Resolved (AI)',
    'Closed',
])

function comparePlannedSubFlowSteps(left: PlannedSubFlowStep, right: PlannedSubFlowStep): number {
    return compareFlowDisplayOrder(
        {
            status: left.status,
            flowNumber: left.workItemNumber,
            title: left.title,
            uniqueId: left.title,
        },
        {
            status: right.status,
            flowNumber: right.workItemNumber,
            title: right.title,
            uniqueId: right.title,
        },
    )
}

function mapExecutionStatusToStepStatus(status: AgentExecution['status']): ExecutionStepStatus {
    switch (status) {
        case 'running':
        case 'completed':
        case 'failed':
        case 'cancelled':
        case 'paused':
        case 'queued':
            return status
        default:
            return 'queued'
    }
}

export function buildPlannedSubFlowSteps(execution: AgentExecution, workItems?: WorkItem[]): PlannedSubFlowStep[] {
    const subFlows = execution.subFlows ?? []
    const workItemsByNumber = new Map((workItems ?? []).map((workItem) => [workItem.workItemNumber, workItem]))
    const parentWorkItem = workItemsByNumber.get(execution.workItemId)
    const subFlowByWorkItemNumber = new Map(subFlows.map((subFlow) => [subFlow.workItemId, subFlow]))

    const plannedChildren = (parentWorkItem?.childWorkItemNumbers ?? [])
        .map((childNumber) => workItemsByNumber.get(childNumber))
        .filter((child): child is WorkItem => Boolean(child))
        .filter((child) => !NON_ACTIONABLE_SUBFLOW_STATES.has(child.state))
        .sort((left, right) => left.workItemNumber - right.workItemNumber)

    const steps: PlannedSubFlowStep[] = plannedChildren.map((child): PlannedSubFlowStep => {
        const liveExecution = subFlowByWorkItemNumber.get(child.workItemNumber)
        if (liveExecution) {
            const liveStatus = mapExecutionStatusToStepStatus(liveExecution.status)
            return {
                workItemNumber: child.workItemNumber,
                title: child.title,
                status: liveStatus,
                currentTask: liveExecution.currentPhase
                    ? `${child.title} - ${liveExecution.currentPhase}`
                    : child.title,
                progress: liveExecution.status === 'completed'
                    ? 1
                    : Math.max(0, Math.min(liveExecution.progress ?? 0, 1)),
            }
        }

        if (execution.status === 'completed') {
            return {
                workItemNumber: child.workItemNumber,
                title: child.title,
                status: 'completed',
                currentTask: `${child.title} - Completed`,
                progress: 1,
            }
        }

        if (execution.status === 'paused') {
            return {
                workItemNumber: child.workItemNumber,
                title: child.title,
                status: 'paused',
                currentTask: `${child.title} - Paused`,
                progress: 0,
            }
        }

        if (execution.status === 'failed') {
            return {
                workItemNumber: child.workItemNumber,
                title: child.title,
                status: 'failed',
                currentTask: `${child.title} - Blocked by parent flow failure`,
                progress: 0,
            }
        }

        if (execution.status === 'cancelled') {
            return {
                workItemNumber: child.workItemNumber,
                title: child.title,
                status: 'cancelled',
                currentTask: `${child.title} - Cancelled`,
                progress: 0,
            }
        }

        return {
            workItemNumber: child.workItemNumber,
            title: child.title,
            status: 'queued',
            currentTask: `${child.title} - Queued sub-flow`,
            progress: 0,
        }
    })

    const orphanLiveSteps: PlannedSubFlowStep[] = subFlows
        .filter((subFlow) => !plannedChildren.some((child) => child.workItemNumber === subFlow.workItemId))
        .map((subFlow): PlannedSubFlowStep => ({
            workItemNumber: subFlow.workItemId,
            title: subFlow.workItemTitle,
            status: mapExecutionStatusToStepStatus(subFlow.status),
            currentTask: subFlow.currentPhase
                ? `${subFlow.workItemTitle} - ${subFlow.currentPhase}`
                : subFlow.workItemTitle,
            progress: subFlow.status === 'completed'
                ? 1
                : Math.max(0, Math.min(subFlow.progress ?? 0, 1)),
        }))

    return [...steps, ...orphanLiveSteps].sort(comparePlannedSubFlowSteps)
}
