export type FlowDisplayStatus =
  | 'completed'
  | 'running'
  | 'paused'
  | 'failed'
  | 'cancelled'
  | 'queued'
  | 'idle'

export interface FlowDisplayOrderInput {
  status: FlowDisplayStatus
  flowNumber: number
  startedAt?: string | null
  title?: string | null
  uniqueId?: string | null
}

export function getFlowDisplayPriority(status: FlowDisplayStatus): number {
  switch (status) {
    case 'completed':
      return 0
    case 'queued':
    case 'idle':
      return 2
    default:
      return 1
  }
}

export function compareFlowDisplayOrder(
  left: FlowDisplayOrderInput,
  right: FlowDisplayOrderInput,
): number {
  const statusPriorityDifference = getFlowDisplayPriority(left.status) - getFlowDisplayPriority(right.status)
  if (statusPriorityDifference !== 0) {
    return statusPriorityDifference
  }

  if (left.flowNumber !== right.flowNumber) {
    return left.flowNumber - right.flowNumber
  }

  const leftStartedAt = left.startedAt ? Date.parse(left.startedAt) : Number.NaN
  const rightStartedAt = right.startedAt ? Date.parse(right.startedAt) : Number.NaN
  if (!Number.isNaN(leftStartedAt) && !Number.isNaN(rightStartedAt) && leftStartedAt !== rightStartedAt) {
    return leftStartedAt - rightStartedAt
  }

  const leftTitle = left.title ?? ''
  const rightTitle = right.title ?? ''
  const titleComparison = leftTitle.localeCompare(rightTitle)
  if (titleComparison !== 0) {
    return titleComparison
  }

  return (left.uniqueId ?? '').localeCompare(right.uniqueId ?? '')
}
