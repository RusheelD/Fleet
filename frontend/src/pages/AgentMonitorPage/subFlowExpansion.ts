import type { AgentExecution } from '../../models'

type ExpandableSubFlow = Pick<AgentExecution, 'status'>

export function isSubFlowExpandedByDefault(subFlow: ExpandableSubFlow): boolean {
    return subFlow.status === 'running' ||
        subFlow.status === 'paused' ||
        subFlow.status === 'failed'
}

export function getNextSubFlowExpansionState(
    currentExpandedState: boolean | undefined,
    subFlow: ExpandableSubFlow,
): boolean {
    const isCurrentlyExpanded = currentExpandedState ?? isSubFlowExpandedByDefault(subFlow)
    return !isCurrentlyExpanded
}
