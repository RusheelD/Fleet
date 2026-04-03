import type { WorkItem } from '../../models'

export const AUTO_ASSIGNMENT_LABEL = 'Auto-detect'
export const MANUAL_ASSIGNMENT_LABEL = 'Manual assignment'
export const WORK_ITEM_ASSIGNMENT_OPTION_LABELS = [
    AUTO_ASSIGNMENT_LABEL,
    '1 agent',
    '3 agents',
    '5 agents',
    MANUAL_ASSIGNMENT_LABEL,
] as const

export type WorkItemAssignmentOptionLabel = typeof WORK_ITEM_ASSIGNMENT_OPTION_LABELS[number]

export interface WorkItemAssignmentSettings {
    isAI: boolean
    assignmentMode: 'auto' | 'manual'
    assignedAgentCount: number | null
    assignedTo: string
}

export function getWorkItemAssignmentSettings(
    label: string,
    manualAssignee?: string,
): WorkItemAssignmentSettings {
    if (label === MANUAL_ASSIGNMENT_LABEL) {
        return {
            isAI: false,
            assignmentMode: 'manual',
            assignedAgentCount: null,
            assignedTo: manualAssignee?.trim() || 'Unassigned',
        }
    }

    if (label === '1 agent') {
        return {
            isAI: true,
            assignmentMode: 'manual',
            assignedAgentCount: 1,
            assignedTo: 'Fleet AI',
        }
    }

    if (label === '3 agents') {
        return {
            isAI: true,
            assignmentMode: 'manual',
            assignedAgentCount: 3,
            assignedTo: 'Fleet AI',
        }
    }

    if (label === '5 agents') {
        return {
            isAI: true,
            assignmentMode: 'manual',
            assignedAgentCount: 5,
            assignedTo: 'Fleet AI',
        }
    }

    return {
        isAI: true,
        assignmentMode: 'auto',
        assignedAgentCount: null,
        assignedTo: 'Fleet AI',
    }
}

export function getWorkItemAssignmentLabel(
    workItem: Pick<WorkItem, 'isAI' | 'assignmentMode' | 'assignedAgentCount'>,
): WorkItemAssignmentOptionLabel {
    if (!workItem.isAI) {
        return MANUAL_ASSIGNMENT_LABEL
    }

    if (workItem.assignmentMode !== 'manual') {
        return AUTO_ASSIGNMENT_LABEL
    }

    switch (workItem.assignedAgentCount) {
        case 1:
            return '1 agent'
        case 3:
            return '3 agents'
        case 5:
            return '5 agents'
        default:
            return AUTO_ASSIGNMENT_LABEL
    }
}
