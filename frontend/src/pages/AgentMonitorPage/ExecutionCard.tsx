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
    Spinner,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
} from '@fluentui/react-components'
import {
    PauseRegular,
    StopRegular,
    ArrowClockwiseRegular,
    ClockRegular,
    CodeRegular,
    DocumentRegular,
    BranchRegular,
    CheckmarkCircleFilled,
    DismissCircleFilled,
    CircleRegular,
} from '@fluentui/react-icons'
import type { AgentExecution, AgentInfo } from '../../models'

const STATUS_COLORS: Record<string, 'success' | 'warning' | 'danger' | 'informative' | 'subtle'> = {
    running: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'danger',
    paused: 'informative',
    queued: 'informative',
    idle: 'subtle',
}

/** Format an ISO timestamp into a friendly relative or short string. */
function formatTimestamp(iso: string): string {
    try {
        const date = new Date(iso)
        const now = new Date()
        const diffMs = now.getTime() - date.getTime()
        const diffMin = Math.floor(diffMs / 60_000)
        if (diffMin < 1) return 'just now'
        if (diffMin < 60) return `${diffMin}m ago`
        const diffHr = Math.floor(diffMin / 60)
        if (diffHr < 24) return `${diffHr}h ago`
        return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
    } catch {
        return iso
    }
}

const useStyles = makeStyles({
    executionCard: {
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    executionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
    },
    executionTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    flexRowGap: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    metaRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorNeutralForeground3,
    },
    metaIcon: {
        fontSize: '12px',
    },
    executionActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalXXS,
    },

    /* --- Pipeline / agent list --- */
    pipeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0px', /* controlled by individual rows */
    },
    agentStep: {
        display: 'grid',
        gridTemplateColumns: '20px 1fr auto',
        gap: tokens.spacingHorizontalS,
        alignItems: 'start',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
    },

    /* Left gutter: icon + connector line */
    stepGutter: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '0px',
        position: 'relative',
    },
    stepIcon: {
        fontSize: '16px',
        zIndex: 1,
    },
    stepIconCompleted: {
        color: tokens.colorPaletteGreenForeground1,
    },
    stepIconRunning: {
        /* Spinner replaces the icon — no extra colour needed */
    },
    stepIconFailed: {
        color: tokens.colorPaletteRedForeground1,
    },
    stepIconIdle: {
        color: tokens.colorNeutralForeground4,
    },
    connector: {
        width: '2px',
        flex: 1,
        minHeight: '8px',
        backgroundColor: tokens.colorNeutralStroke2,
        marginTop: '2px',
    },
    connectorCompleted: {
        backgroundColor: tokens.colorPaletteGreenBorder1,
    },

    /* Center: role + task info */
    stepBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    roleName: {
        lineHeight: '20px',
    },
    taskCaption: {
        color: tokens.colorNeutralForeground3,
    },
    taskCaptionRunning: {
        color: tokens.colorNeutralForeground2,
    },
    stepProgress: {
        maxWidth: '120px',
        marginTop: tokens.spacingVerticalXXS,
    },

    /* Right: percentage or badge */
    stepTrailing: {
        display: 'flex',
        alignItems: 'center',
        minHeight: '20px',
    },
    progressPercent: {
        fontSize: '11px',
        fontVariantNumeric: 'tabular-nums',
        color: tokens.colorNeutralForeground3,
    },

    completedActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
})

interface ExecutionCardProps {
    execution: AgentExecution
    onPause?: (executionId: string) => void
    onCancel?: (executionId: string) => void
}

function AgentStepIcon({ status }: { status: AgentInfo['status'] }) {
    const styles = useStyles()
    switch (status) {
        case 'completed':
            return <CheckmarkCircleFilled className={`${styles.stepIcon} ${styles.stepIconCompleted}`} />
        case 'running':
            return <Spinner size="extra-tiny" />
        case 'failed':
            return <DismissCircleFilled className={`${styles.stepIcon} ${styles.stepIconFailed}`} />
        default:
            return <CircleRegular className={`${styles.stepIcon} ${styles.stepIconIdle}`} />
    }
}

export function ExecutionCard({ execution, onPause, onCancel }: ExecutionCardProps) {
    const styles = useStyles()
    const toasterId = useId('exec-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const notify = (msg: string, intent: 'info' | 'success' | 'error' = 'info') => {
        dispatchToast(
            <Toast><ToastTitle>{msg}</ToastTitle></Toast>,
            { intent },
        )
    }

    const handlePause = () => {
        if (onPause) {
            onPause(execution.id)
            notify('Pausing agent execution...')
        }
    }

    const handleCancel = () => {
        if (onCancel) {
            onCancel(execution.id)
            notify('Stopping agent execution...')
        }
    }

    const agents = execution.agents

    return (
        <Card className={styles.executionCard}>
            <Toaster toasterId={toasterId} />

            {/* ── Header ── */}
            <div className={styles.executionHeader}>
                <div className={styles.executionTitle}>
                    <div className={styles.flexRowGap}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        <Badge appearance="filled" color={STATUS_COLORS[execution.status]} size="small">
                            {execution.status}
                        </Badge>
                    </div>
                    <Body1 weight="semibold">{execution.workItemTitle}</Body1>
                    <div className={styles.metaRow}>
                        <ClockRegular className={styles.metaIcon} />
                        <Caption1>Started {formatTimestamp(execution.startedAt)} · {execution.duration}</Caption1>
                    </div>
                </div>
                <div className={styles.executionActions}>
                    {execution.status === 'running' && (
                        <>
                            <Button appearance="subtle" size="small" icon={<PauseRegular />} aria-label="Pause" onClick={handlePause} />
                            <Button appearance="subtle" size="small" icon={<StopRegular />} aria-label="Stop" onClick={handleCancel} />
                        </>
                    )}
                    {execution.status === 'failed' && (
                        <Button appearance="subtle" size="small" icon={<ArrowClockwiseRegular />} aria-label="Retry" onClick={() => notify('Retrying agent execution...')} />
                    )}
                </div>
            </div>

            <ProgressBar
                value={execution.progress}
                thickness="large"
                color={execution.status === 'failed' ? 'error' : execution.status === 'completed' ? 'success' : 'brand'}
            />

            <Divider />

            {/* ── Pipeline steps ── */}
            <div className={styles.pipeline}>
                {agents.map((agent, i) => {
                    const isLast = i === agents.length - 1
                    const isRunning = agent.status === 'running'
                    const isCompleted = agent.status === 'completed'

                    return (
                        <div key={agent.role} className={styles.agentStep}>
                            {/* Gutter: icon + vertical connector */}
                            <div className={styles.stepGutter}>
                                <AgentStepIcon status={agent.status} />
                                {!isLast && (
                                    <div className={`${styles.connector} ${isCompleted ? styles.connectorCompleted : ''}`} />
                                )}
                            </div>

                            {/* Body: role name + current task */}
                            <div className={styles.stepBody}>
                                <Text size={200} weight="semibold" className={styles.roleName}>
                                    {agent.role}
                                </Text>
                                <Caption1 className={isRunning ? styles.taskCaptionRunning : styles.taskCaption}>
                                    {agent.currentTask}
                                </Caption1>
                                {isRunning && agent.progress > 0 && (
                                    <ProgressBar
                                        className={styles.stepProgress}
                                        value={agent.progress}
                                        thickness="medium"
                                        color="brand"
                                    />
                                )}
                            </div>

                            {/* Trailing: percentage */}
                            <div className={styles.stepTrailing}>
                                {isRunning && agent.progress > 0 && agent.progress < 1 && (
                                    <Text className={styles.progressPercent}>
                                        {Math.round(agent.progress * 100)}%
                                    </Text>
                                )}
                            </div>
                        </div>
                    )
                })}
            </div>

            {execution.status === 'completed' && (
                <div className={styles.completedActions}>
                    <Button appearance="outline" size="small" icon={<BranchRegular />} onClick={() => notify('PR view is not available in this version')}>View PR</Button>
                    <Button appearance="outline" size="small" icon={<CodeRegular />} onClick={() => notify('Code diff view coming soon')}>View Changes</Button>
                    <Button appearance="outline" size="small" icon={<DocumentRegular />} onClick={() => notify('Documentation view coming soon')}>Docs</Button>
                </div>
            )}
        </Card>
    )
}
