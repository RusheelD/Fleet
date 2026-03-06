import {
    makeStyles,
    mergeClasses,
    tokens,
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
import { usePreferences } from '../../hooks'
import { openPullRequest, openPullRequestDiff } from './pullRequest'

const STATUS_COLORS: Record<string, 'success' | 'warning' | 'danger' | 'informative' | 'subtle'> = {
    running: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'danger',
    paused: 'informative',
    queued: 'informative',
    idle: 'subtle',
}

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
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke2,
        borderRightColor: tokens.colorNeutralStroke2,
        borderBottomColor: tokens.colorNeutralStroke2,
        borderLeftColor: tokens.colorNeutralStroke2,
        boxShadow: tokens.shadow4,
    },
    executionCardCompact: {
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        gap: tokens.spacingVerticalS,
    },
    executionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
    executionHeaderCompact: {
        gap: tokens.spacingHorizontalS,
    },
    executionTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        minWidth: 0,
    },
    executionTitleCompact: {
        gap: '1px',
    },
    titleText: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    titleTextCompact: {
        fontSize: '12px',
        lineHeight: '16px',
    },
    flexRowGap: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    flexRowGapCompact: {
        gap: tokens.spacingHorizontalXS,
    },
    metaRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        color: tokens.colorNeutralForeground3,
        flexWrap: 'wrap',
    },
    metaRowCompact: {
        gap: '2px',
    },
    metaCaptionCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    metaIcon: {
        fontSize: '12px',
    },
    metaIconCompact: {
        fontSize: '10px',
    },
    executionActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalXXS,
        flexShrink: 0,
    },
    pipeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0px',
    },
    agentStep: {
        display: 'grid',
        gridTemplateColumns: '20px 1fr auto',
        gap: tokens.spacingHorizontalS,
        alignItems: 'start',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
    },
    agentStepCompact: {
        gridTemplateColumns: '16px 1fr auto',
        gap: tokens.spacingHorizontalXS,
        paddingTop: '2px',
        paddingBottom: '2px',
    },
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
    stepBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    roleName: {
        lineHeight: '20px',
    },
    roleNameCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    taskCaption: {
        color: tokens.colorNeutralForeground3,
    },
    taskCaptionRunning: {
        color: tokens.colorNeutralForeground2,
    },
    taskCaptionCompact: {
        fontSize: '10px',
        lineHeight: '13px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        maxWidth: '26ch',
    },
    stepProgress: {
        maxWidth: '120px',
        marginTop: tokens.spacingVerticalXXS,
    },
    stepProgressCompact: {
        maxWidth: '88px',
        marginTop: '1px',
    },
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
    progressPercentCompact: {
        fontSize: '10px',
    },
    completedActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    completedActionsCompact: {
        gap: tokens.spacingHorizontalXS,
    },
    compactDivider: {
        marginTop: '2px',
        marginBottom: '2px',
    },
})

interface ExecutionCardProps {
    execution: AgentExecution
    onPause?: (executionId: string) => void
    onCancel?: (executionId: string) => void
    onRetry?: (executionId: string) => void
    onViewDocs?: (executionId: string) => void
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

export function ExecutionCard({ execution, onPause, onCancel, onRetry, onViewDocs }: ExecutionCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
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
        <Card className={mergeClasses(styles.executionCard, isCompact && styles.executionCardCompact)}>
            <Toaster toasterId={toasterId} />

            <div className={mergeClasses(styles.executionHeader, isCompact && styles.executionHeaderCompact)}>
                <div className={mergeClasses(styles.executionTitle, isCompact && styles.executionTitleCompact)}>
                    <div className={mergeClasses(styles.flexRowGap, isCompact && styles.flexRowGapCompact)}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        <Badge appearance="filled" color={STATUS_COLORS[execution.status]} size="small">
                            {execution.status}
                        </Badge>
                    </div>
                    <Text
                        weight="semibold"
                        className={mergeClasses(styles.titleText, isCompact && styles.titleTextCompact)}
                    >
                        {execution.workItemTitle}
                    </Text>
                    <div className={mergeClasses(styles.metaRow, isCompact && styles.metaRowCompact)}>
                        <ClockRegular className={mergeClasses(styles.metaIcon, isCompact && styles.metaIconCompact)} />
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            Started {formatTimestamp(execution.startedAt)} - {execution.duration}
                            {execution.currentPhase ? ` - ${execution.currentPhase}` : ''}
                        </Caption1>
                    </div>
                    {execution.branchName && (
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            Branch: {execution.branchName}
                        </Caption1>
                    )}
                </div>
                <div className={styles.executionActions}>
                    {execution.status === 'running' && (
                        <>
                            <Button appearance="subtle" size="small" icon={<PauseRegular />} aria-label="Pause" onClick={handlePause} />
                            <Button appearance="subtle" size="small" icon={<StopRegular />} aria-label="Stop" onClick={handleCancel} />
                        </>
                    )}
                    {execution.status === 'failed' && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<ArrowClockwiseRegular />}
                            aria-label="Retry"
                            onClick={() => {
                                if (onRetry) {
                                    onRetry(execution.id)
                                } else {
                                    notify('Retry is unavailable for this execution', 'error')
                                }
                            }}
                        />
                    )}
                </div>
            </div>

            <ProgressBar
                value={execution.progress}
                thickness={isCompact ? 'medium' : 'large'}
                color={execution.status === 'failed' ? 'error' : execution.status === 'completed' ? 'success' : 'brand'}
            />

            <Divider className={isCompact ? styles.compactDivider : undefined} />

            <div className={styles.pipeline}>
                {agents.map((agent, i) => {
                    const isLast = i === agents.length - 1
                    const isRunning = agent.status === 'running'
                    const isCompleted = agent.status === 'completed'

                    return (
                        <div key={agent.role} className={mergeClasses(styles.agentStep, isCompact && styles.agentStepCompact)}>
                            <div className={styles.stepGutter}>
                                <AgentStepIcon status={agent.status} />
                                {!isLast && (
                                    <div className={`${styles.connector} ${isCompleted ? styles.connectorCompleted : ''}`} />
                                )}
                            </div>

                            <div className={styles.stepBody}>
                                <Text
                                    size={200}
                                    weight="semibold"
                                    className={mergeClasses(styles.roleName, isCompact && styles.roleNameCompact)}
                                >
                                    {agent.role}
                                </Text>
                                <Caption1
                                    className={mergeClasses(
                                        isRunning ? styles.taskCaptionRunning : styles.taskCaption,
                                        isCompact && styles.taskCaptionCompact,
                                    )}
                                >
                                    {agent.currentTask}
                                </Caption1>
                                {isRunning && agent.progress > 0 && (
                                    <ProgressBar
                                        className={mergeClasses(styles.stepProgress, isCompact && styles.stepProgressCompact)}
                                        value={agent.progress}
                                        thickness="medium"
                                        color="brand"
                                    />
                                )}
                            </div>

                            <div className={styles.stepTrailing}>
                                {isRunning && agent.progress > 0 && agent.progress < 1 && (
                                    <Text className={mergeClasses(styles.progressPercent, isCompact && styles.progressPercentCompact)}>
                                        {Math.round(agent.progress * 100)}%
                                    </Text>
                                )}
                            </div>
                        </div>
                    )
                })}
            </div>

            {execution.status === 'completed' && (
                <div className={mergeClasses(styles.completedActions, isCompact && styles.completedActionsCompact)}>
                    <Button
                        appearance="outline"
                        size="small"
                        icon={<BranchRegular />}
                        disabled={!execution.pullRequestUrl}
                        onClick={() => openPullRequest(execution.pullRequestUrl)}
                    >
                        View PR
                    </Button>
                    <Button
                        appearance="outline"
                        size="small"
                        icon={<CodeRegular />}
                        onClick={() => {
                            if (!openPullRequestDiff(execution.pullRequestUrl)) {
                                notify('No pull request diff is available for this execution', 'error')
                            }
                        }}
                    >
                        View Changes
                    </Button>
                    <Button
                        appearance="outline"
                        size="small"
                        icon={<DocumentRegular />}
                        onClick={() => {
                            if (onViewDocs) {
                                onViewDocs(execution.id)
                            } else {
                                notify('Documentation is unavailable for this execution', 'error')
                            }
                        }}
                    >
                        Docs
                    </Button>
                </div>
            )}
        </Card>
    )
}
