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
    DeleteRegular,
    BranchRegular,
    CheckmarkCircleFilled,
    DismissCircleFilled,
    CircleRegular,
} from '@fluentui/react-icons'
import type { AgentExecution, AgentInfo } from '../../models'
import { usePreferences, useIsMobile } from '../../hooks'
import { openPullRequest, openPullRequestDiff } from './pullRequest'

type ExecutionStepStatus = AgentInfo['status'] | 'paused'
type DisplayAgentInfo = Omit<AgentInfo, 'status'> & { status: ExecutionStepStatus }

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
    executionCardMobile: {
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
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
    executionHeaderMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
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
    titleTextMobile: {
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
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
        flexWrap: 'wrap',
    },
    executionActionsMobile: {
        width: '100%',
        justifyContent: 'flex-end',
    },
    pipeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0px',
    },
    agentStep: {
        display: 'grid',
        gridTemplateColumns: '28px 1fr auto',
        gap: tokens.spacingHorizontalS,
        alignItems: 'start',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
    },
    agentStepCompact: {
        gridTemplateColumns: '24px 1fr auto',
        gap: tokens.spacingHorizontalXS,
        paddingTop: '2px',
        paddingBottom: '2px',
    },
    stepGutter: {
        position: 'relative',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        alignSelf: 'stretch',
        width: '28px',
        minHeight: '100%',
    },
    stepGutterCompact: {
        width: '24px',
    },
    stepIconShell: {
        width: '24px',
        height: '24px',
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
        zIndex: 1,
        boxShadow: `0 0 0 4px ${tokens.colorNeutralBackground1}`,
    },
    stepIconShellCompact: {
        width: '20px',
        height: '20px',
        boxShadow: `0 0 0 3px ${tokens.colorNeutralBackground1}`,
    },
    stepIcon: {
        fontSize: '16px',
    },
    stepIconCompleted: {
        color: tokens.colorPaletteGreenForeground1,
    },
    stepIconPaused: {
        color: tokens.colorBrandForeground1,
    },
    stepIconFailed: {
        color: tokens.colorPaletteRedForeground1,
    },
    stepIconCancelled: {
        color: tokens.colorNeutralForeground3,
    },
    stepIconIdle: {
        color: tokens.colorNeutralForeground4,
    },
    connectorSegment: {
        position: 'absolute',
        left: '50%',
        transform: 'translateX(-50%)',
        width: '3px',
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: tokens.colorNeutralStroke2,
    },
    connectorTop: {
        top: 0,
        bottom: '50%',
    },
    connectorBottom: {
        top: '50%',
        bottom: 0,
    },
    connectorCompleted: {
        backgroundColor: tokens.colorPaletteGreenForeground1,
    },
    connectorRunning: {
        backgroundColor: tokens.colorPaletteMarigoldForeground1,
    },
    connectorPaused: {
        backgroundColor: tokens.colorBrandForeground1,
    },
    connectorFailed: {
        backgroundColor: tokens.colorPaletteRedForeground1,
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
    taskCaptionMobile: {
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
        maxWidth: 'unset',
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
    runActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    completedActionsCompact: {
        gap: tokens.spacingHorizontalXS,
    },
    completedActionsMobile: {
        width: '100%',
    },
    completedActionButtonMobile: {
        flex: '1 1 120px',
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
    onResume?: (executionId: string) => void
    onRetry?: (executionId: string) => void
    onDelete?: (executionId: string) => void
    onViewDocs?: (executionId: string) => void
}

function AgentStepIcon({ status, isCompact }: { status: ExecutionStepStatus; isCompact: boolean }) {
    const styles = useStyles()
    const shellClassName = mergeClasses(
        styles.stepIconShell,
        isCompact && styles.stepIconShellCompact,
    )

    switch (status) {
        case 'completed':
            return (
                <div className={shellClassName}>
                    <CheckmarkCircleFilled className={mergeClasses(styles.stepIcon, styles.stepIconCompleted)} />
                </div>
            )
        case 'running':
            return (
                <div className={shellClassName}>
                    <Spinner size="extra-tiny" />
                </div>
            )
        case 'paused':
            return (
                <div className={shellClassName}>
                    <PauseRegular className={mergeClasses(styles.stepIcon, styles.stepIconPaused)} />
                </div>
            )
        case 'failed':
            return (
                <div className={shellClassName}>
                    <DismissCircleFilled className={mergeClasses(styles.stepIcon, styles.stepIconFailed)} />
                </div>
            )
        case 'cancelled':
            return (
                <div className={shellClassName}>
                    <StopRegular className={mergeClasses(styles.stepIcon, styles.stepIconCancelled)} />
                </div>
            )
        default:
            return (
                <div className={shellClassName}>
                    <CircleRegular className={mergeClasses(styles.stepIcon, styles.stepIconIdle)} />
                </div>
            )
    }
}

export function ExecutionCard({ execution, onPause, onCancel, onResume, onRetry, onDelete, onViewDocs }: ExecutionCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
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

    const handleDelete = () => {
        if (onDelete) {
            onDelete(execution.id)
        }
    }

    const handleResume = () => {
        if (onResume) {
            onResume(execution.id)
            return
        }

        notify('Resume is unavailable for this execution', 'error')
    }

    const handleRetry = () => {
        if (onRetry) {
            onRetry(execution.id)
            return
        }

        notify('Retry is unavailable for this execution', 'error')
    }

    const reviewLoopCount = execution.reviewLoopCount ?? 0
    const lastReviewRecommendation = execution.lastReviewRecommendation ?? null
    const isAutoRemediating = execution.status === 'running' && (execution.currentPhase?.startsWith('Auto-remediation:') ?? false)
    const canDeleteExecution =
        execution.status === 'running' ||
        execution.status === 'queued' ||
        execution.status === 'paused' ||
        execution.status === 'failed' ||
        execution.status === 'cancelled'
    const reviewLoopLabel = isAutoRemediating
        ? `Auto-fixing${lastReviewRecommendation ? ` ${lastReviewRecommendation}` : ''}`
        : reviewLoopCount > 0
            ? `Self-corrected x${reviewLoopCount}`
            : null

    const terminalExecutionStatus = execution.status === 'failed' || execution.status === 'cancelled' || execution.status === 'paused'
        ? execution.status
        : null

    const agents: DisplayAgentInfo[] = execution.agents.map((agent) => {
        if (terminalExecutionStatus === null || agent.status === 'completed')
            return { ...agent }

        if (terminalExecutionStatus === 'paused') {
            return {
                ...agent,
                status: 'paused',
                currentTask: 'Paused',
            }
        }

        return {
            ...agent,
            status: terminalExecutionStatus,
            currentTask: terminalExecutionStatus === 'failed' ? 'Failed' : 'Cancelled',
            progress: 0,
        }
    })

    const getConnectorToneClass = (status: ExecutionStepStatus) => {
        switch (status) {
            case 'completed':
                return styles.connectorCompleted
            case 'running':
                return styles.connectorRunning
            case 'paused':
                return styles.connectorPaused
            case 'failed':
                return styles.connectorFailed
            default:
                return undefined
        }
    }

    return (
        <Card className={mergeClasses(styles.executionCard, isCompact && styles.executionCardCompact, isMobile && styles.executionCardMobile)}>
            <Toaster toasterId={toasterId} />

            <div className={mergeClasses(styles.executionHeader, isCompact && styles.executionHeaderCompact, isMobile && styles.executionHeaderMobile)}>
                <div className={mergeClasses(styles.executionTitle, isCompact && styles.executionTitleCompact)}>
                    <div className={mergeClasses(styles.flexRowGap, isCompact && styles.flexRowGapCompact)}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        <Badge appearance="filled" color={STATUS_COLORS[execution.status]} size="small">
                            {execution.status}
                        </Badge>
                        {reviewLoopLabel && (
                            <Badge
                                appearance="tint"
                                color={isAutoRemediating ? 'warning' : 'informative'}
                                size="small"
                            >
                                {reviewLoopLabel}
                            </Badge>
                        )}
                    </div>
                    <Text
                        weight="semibold"
                        className={mergeClasses(styles.titleText, isCompact && styles.titleTextCompact, isMobile && styles.titleTextMobile)}
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
                    {reviewLoopCount > 0 && lastReviewRecommendation && !isAutoRemediating && (
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            Final review outcome: {lastReviewRecommendation}
                        </Caption1>
                    )}
                </div>
                <div className={mergeClasses(styles.executionActions, isMobile && styles.executionActionsMobile)}>
                    {execution.status === 'running' && (
                        <>
                            <Button appearance="subtle" size="small" icon={<PauseRegular />} aria-label="Pause" onClick={handlePause} />
                            <Button appearance="subtle" size="small" icon={<StopRegular />} aria-label="Stop" onClick={handleCancel} />
                            {onDelete && (
                                <Button appearance="subtle" size="small" icon={<DeleteRegular />} aria-label="Delete run" onClick={handleDelete} />
                            )}
                        </>
                    )}
                    {execution.status !== 'running' && execution.status !== 'failed' && execution.status !== 'paused' && canDeleteExecution && onDelete && (
                        <Button appearance="subtle" size="small" icon={<DeleteRegular />} aria-label="Delete run" onClick={handleDelete} />
                    )}
                </div>
            </div>

            <ProgressBar
                value={execution.progress}
                thickness={isCompact ? 'medium' : 'large'}
                color={
                    execution.status === 'failed'
                        ? 'error'
                        : execution.status === 'cancelled'
                            ? 'warning'
                            : execution.status === 'completed'
                                ? 'success'
                                : 'brand'
                }
            />

            <Divider className={isCompact ? styles.compactDivider : undefined} />

            <div className={styles.pipeline}>
                {agents.map((agent, i) => {
                    const isFirst = i === 0
                    const isLast = i === agents.length - 1
                    const isRunning = agent.status === 'running'
                    const previousStatus = isFirst ? null : agents[i - 1].status

                    return (
                        <div key={agent.role} className={mergeClasses(styles.agentStep, isCompact && styles.agentStepCompact)}>
                            <div className={mergeClasses(styles.stepGutter, isCompact && styles.stepGutterCompact)}>
                                {!isFirst && (
                                    <div
                                        className={mergeClasses(
                                            styles.connectorSegment,
                                            styles.connectorTop,
                                            previousStatus ? getConnectorToneClass(previousStatus) : undefined,
                                        )}
                                    />
                                )}
                                {!isLast && (
                                    <div
                                        className={mergeClasses(
                                            styles.connectorSegment,
                                            styles.connectorBottom,
                                            getConnectorToneClass(agent.status),
                                        )}
                                    />
                                )}
                                <AgentStepIcon status={agent.status} isCompact={isCompact} />
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
                                        isMobile && styles.taskCaptionMobile,
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

            {(execution.status === 'paused' || execution.status === 'failed') && (
                <div className={mergeClasses(styles.runActions, isCompact && styles.completedActionsCompact, isMobile && styles.completedActionsMobile)}>
                    {execution.status === 'paused' && (
                        <Button
                            appearance="primary"
                            size="small"
                            icon={<ArrowClockwiseRegular />}
                            onClick={handleResume}
                            className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
                        >
                            Resume
                        </Button>
                    )}
                    {execution.status === 'failed' && (
                        <Button
                            appearance="primary"
                            size="small"
                            icon={<ArrowClockwiseRegular />}
                            onClick={handleRetry}
                            className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
                        >
                            Retry
                        </Button>
                    )}
                    {onDelete && (
                        <Button
                            appearance="outline"
                            size="small"
                            icon={<DeleteRegular />}
                            onClick={handleDelete}
                            className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
                        >
                            Delete
                        </Button>
                    )}
                </div>
            )}

            {execution.status === 'completed' && (
                <div className={mergeClasses(styles.completedActions, isCompact && styles.completedActionsCompact, isMobile && styles.completedActionsMobile)}>
                    <Button
                        appearance="outline"
                        size="small"
                        icon={<BranchRegular />}
                        disabled={!execution.pullRequestUrl}
                        onClick={() => openPullRequest(execution.pullRequestUrl)}
                        className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
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
                        className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
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
                        className={mergeClasses(isMobile && styles.completedActionButtonMobile)}
                    >
                        Docs
                    </Button>
                </div>
            )}
        </Card>
    )
}
