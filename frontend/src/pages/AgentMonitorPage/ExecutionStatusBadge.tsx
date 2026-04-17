import { Badge } from '@fluentui/react-components'
import type { AgentExecution } from '../../models'
import { InfoBadge } from '../../components/shared/InfoBadge'

const STATUS_COLORS: Record<string, 'success' | 'warning' | 'danger' | 'subtle'> = {
    running: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'danger',
    idle: 'subtle',
}

interface ExecutionStatusBadgeProps {
    status: AgentExecution['status']
}

export function ExecutionStatusBadge({ status }: ExecutionStatusBadgeProps) {
    if (status === 'paused' || status === 'queued') {
        return (
            <InfoBadge appearance="filled" size="small">
                {status}
            </InfoBadge>
        )
    }

    return (
        <Badge appearance="filled" color={STATUS_COLORS[status] ?? 'subtle'} size="small">
            {status}
        </Badge>
    )
}
