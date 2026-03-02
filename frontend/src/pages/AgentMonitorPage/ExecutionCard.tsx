import {
    makeStyles,
    tokens,
    Body1,
    Caption1,
    Text,
    Card,
    Button,
    Badge,
    Divider,
    ProgressBar,
} from '@fluentui/react-components'
import {
    BotRegular,
    PauseRegular,
    StopRegular,
    ArrowClockwiseRegular,
    ClockRegular,
    CodeRegular,
    DocumentRegular,
    BranchRegular,
} from '@fluentui/react-icons'
import type { AgentExecution } from '../../models'

const STATUS_COLORS: Record<string, 'success' | 'warning' | 'danger' | 'informative' | 'subtle'> = {
    running: 'warning',
    completed: 'success',
    failed: 'danger',
    queued: 'informative',
    idle: 'subtle',
}

const useStyles = makeStyles({
    executionCard: {
        padding: '1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    executionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
    },
    executionTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
    },
    flexRowGap: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    clockSmallIcon: {
        fontSize: '10px',
        marginRight: '0.25rem',
    },
    executionActions: {
        display: 'flex',
        gap: '0.25rem',
    },
    agentsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    agentRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    agentBotSmall: {
        fontSize: '16px',
        color: tokens.colorBrandForeground1,
    },
    agentInfo: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
    },
    progressPercent: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground3,
    },
    completedActions: {
        display: 'flex',
        gap: '0.5rem',
    },
})

interface ExecutionCardProps {
    execution: AgentExecution
}

export function ExecutionCard({ execution }: ExecutionCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.executionCard}>
            <div className={styles.executionHeader}>
                <div className={styles.executionTitle}>
                    <div className={styles.flexRowGap}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        <Badge appearance="filled" color={STATUS_COLORS[execution.status]} size="small">
                            {execution.status}
                        </Badge>
                    </div>
                    <Body1>{execution.workItemTitle}</Body1>
                    <Caption1>
                        <ClockRegular className={styles.clockSmallIcon} />
                        Started {execution.startedAt} · {execution.duration}
                    </Caption1>
                </div>
                <div className={styles.executionActions}>
                    {execution.status === 'running' && (
                        <>
                            <Button appearance="subtle" size="small" icon={<PauseRegular />} aria-label="Pause" />
                            <Button appearance="subtle" size="small" icon={<StopRegular />} aria-label="Stop" />
                        </>
                    )}
                    {execution.status === 'failed' && (
                        <Button appearance="subtle" size="small" icon={<ArrowClockwiseRegular />} aria-label="Retry" />
                    )}
                </div>
            </div>

            <ProgressBar
                value={execution.progress}
                thickness="large"
                color={execution.status === 'failed' ? 'error' : execution.status === 'completed' ? 'success' : 'brand'}
            />

            <Divider />

            <div className={styles.agentsContainer}>
                {execution.agents.map((agent, i) => (
                    <div key={i} className={styles.agentRow}>
                        <BotRegular className={styles.agentBotSmall} />
                        <div className={styles.agentInfo}>
                            <div className={styles.flexRowGap}>
                                <Text size={200} weight="semibold">{agent.role}</Text>
                                <Badge appearance="outline" color={STATUS_COLORS[agent.status]} size="tiny">
                                    {agent.status}
                                </Badge>
                            </div>
                            <Caption1>{agent.currentTask}</Caption1>
                        </div>
                        {agent.progress > 0 && agent.progress < 1 && (
                            <Text className={styles.progressPercent}>
                                {Math.round(agent.progress * 100)}%
                            </Text>
                        )}
                    </div>
                ))}
            </div>

            {execution.status === 'completed' && (
                <div className={styles.completedActions}>
                    <Button appearance="outline" size="small" icon={<BranchRegular />}>View PR</Button>
                    <Button appearance="outline" size="small" icon={<CodeRegular />}>View Changes</Button>
                    <Button appearance="outline" size="small" icon={<DocumentRegular />}>Docs</Button>
                </div>
            )}
        </Card>
    )
}
