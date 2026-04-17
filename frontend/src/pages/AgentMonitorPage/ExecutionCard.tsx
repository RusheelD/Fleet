import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
    Card,
    Button,
    Divider,
    ProgressBar,
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
} from '@fluentui/react-icons'
import type { AgentExecution, WorkItem } from '../../models'
import { usePreferences, useIsMobile } from '../../hooks'
import { openPullRequest, openPullRequestDiff } from './pullRequest'
import { appTokens } from '../../styles/appTokens'
import { useState } from 'react'
import { getNextSubFlowExpansionState } from './subFlowExpansion'
import { ExecutionPipeline } from './ExecutionPipeline'
import { ExecutionStatusBadge } from './ExecutionStatusBadge'
import { formatTimestamp } from './executionFormatting'
import { buildPlannedSubFlowSteps } from './plannedSubFlows'
import { SubFlowExecutionList } from './SubFlowExecutionList'
import {
    buildPipelineDisplaySteps,
    type DisplayAgentInfo,
} from './pipelineDisplay'

const useStyles = makeStyles({
    executionCard: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    executionCardCompact: {
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        gap: appTokens.space.sm,
    },
    executionCardMobile: {
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
    },
    executionCardNested: {
        backgroundColor: appTokens.color.surfaceAlt,
        boxShadow: 'none',
    },
    executionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: appTokens.space.md,
    },
    executionHeaderCompact: {
        gap: appTokens.space.sm,
    },
    executionHeaderMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    executionTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
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
        fontSize: appTokens.fontSize.sm,
        lineHeight: appTokens.lineHeight.snug,
    },
    flexRowGap: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    flexRowGapCompact: {
        gap: appTokens.space.xs,
    },
    metaRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        color: appTokens.color.textTertiary,
        flexWrap: 'wrap',
    },
    metaRowCompact: {
        gap: '2px',
    },
    metaCaptionCompact: {
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
    metaIcon: {
        fontSize: appTokens.fontSize.iconXs,
    },
    metaIconCompact: {
        fontSize: appTokens.fontSize.xxs,
    },
    executionActions: {
        display: 'flex',
        gap: appTokens.space.xxxs,
        flexShrink: 0,
        flexWrap: 'wrap',
    },
    executionActionsMobile: {
        width: '100%',
        justifyContent: 'flex-end',
    },
    sectionHeaderRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    sectionHeaderLabel: {
        color: appTokens.color.textSecondary,
    },
    pipeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0px',
    },
    agentStep: {
        display: 'grid',
        gridTemplateColumns: '28px 1fr auto',
        gap: appTokens.space.sm,
        alignItems: 'start',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
    },
    agentStepCompact: {
        gridTemplateColumns: '24px 1fr auto',
        gap: appTokens.space.xs,
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
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.surface,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
        zIndex: 1,
        boxShadow: `0 0 0 4px ${appTokens.color.surface}`,
    },
    stepIconShellCompact: {
        width: '20px',
        height: '20px',
        boxShadow: `0 0 0 3px ${appTokens.color.surface}`,
    },
    stepIcon: {
        fontSize: appTokens.fontSize.iconSm,
    },
    stepIconCompleted: {
        color: appTokens.color.success,
    },
    stepIconPaused: {
        color: appTokens.color.info,
    },
    stepIconQueued: {
        color: appTokens.color.brand,
    },
    stepIconFailed: {
        color: appTokens.color.danger,
    },
    stepIconCancelled: {
        color: appTokens.color.textTertiary,
    },
    stepIconIdle: {
        color: appTokens.color.textMuted,
    },
    connectorSegment: {
        position: 'absolute',
        left: '50%',
        transform: 'translateX(-50%)',
        width: '3px',
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.border,
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
        backgroundColor: appTokens.color.success,
    },
    connectorRunning: {
        backgroundColor: appTokens.color.warning,
    },
    connectorPaused: {
        backgroundColor: appTokens.color.info,
    },
    connectorQueued: {
        backgroundColor: appTokens.color.brand,
    },
    connectorFailed: {
        backgroundColor: appTokens.color.danger,
    },
    stepBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    roleName: {
        lineHeight: appTokens.lineHeight.base,
    },
    roleNameCompact: {
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
    taskCaption: {
        color: appTokens.color.textTertiary,
    },
    taskCaptionRunning: {
        color: appTokens.color.textSecondary,
    },
    taskCaptionCompact: {
        fontSize: appTokens.fontSize.xxs,
        lineHeight: appTokens.lineHeight.tight,
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
        marginTop: appTokens.space.xxxs,
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
        fontSize: appTokens.fontSize.xs,
        fontVariantNumeric: 'tabular-nums',
        color: appTokens.color.textTertiary,
    },
    progressPercentCompact: {
        fontSize: appTokens.fontSize.xxs,
    },
    completedActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    runActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    completedActionsCompact: {
        gap: appTokens.space.xs,
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
    workItems?: WorkItem[]
    onPause?: (executionId: string) => void
    onCancel?: (executionId: string) => void
    onResume?: (executionId: string) => void
    onRetry?: (executionId: string) => void
    onDelete?: (executionId: string) => void
    onViewDocs?: (executionId: string) => void
    nested?: boolean
}

export function ExecutionCard({ execution, workItems, onPause, onCancel, onResume, onRetry, onDelete, onViewDocs, nested = false }: ExecutionCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const [expandedSubFlowIds, setExpandedSubFlowIds] = useState<Record<string, boolean>>({})
    const isCompact = preferences?.compactMode ?? false
    const isNested = nested || Boolean(execution.parentExecutionId)
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
    const subFlows = execution.subFlows ?? []
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
    const runModeLabel = execution.executionMode === 'orchestration' ? 'Flow' : 'Direct'
    const outputLabel = execution.pullRequestUrl
        ? execution.status === 'completed'
            ? 'PR ready'
            : 'PR active'
        : execution.executionMode === 'orchestration'
            ? 'Merged batch'
            : execution.status === 'completed'
                ? 'Completed'
                : 'No PR yet'
    const activePhaseLabel = execution.currentPhase ?? (
        execution.status === 'completed'
            ? 'Run completed'
            : execution.status === 'paused'
                ? 'Run paused'
                : execution.status === 'failed'
                    ? 'Run failed'
                    : execution.status === 'cancelled'
                        ? 'Run cancelled'
                        : 'Waiting to start'
    )

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

    const plannedSubFlowSteps = buildPlannedSubFlowSteps(execution, workItems)
    const displayedSubFlowCount = execution.executionMode === 'orchestration'
        ? (plannedSubFlowSteps.length > 0 ? plannedSubFlowSteps.length : subFlows.length)
        : 0
    const pipelineDisplaySteps = buildPipelineDisplaySteps(
        execution.executionMode,
        agents,
        plannedSubFlowSteps,
    )
    const executionMetaParts = [
        activePhaseLabel,
        runModeLabel,
        outputLabel,
        displayedSubFlowCount > 0 ? `${displayedSubFlowCount} sub-flow${displayedSubFlowCount === 1 ? '' : 's'}` : null,
        execution.branchName || null,
        reviewLoopLabel && !isAutoRemediating ? reviewLoopLabel : null,
        reviewLoopCount > 0 && lastReviewRecommendation && !isAutoRemediating ? `Review: ${lastReviewRecommendation}` : null,
    ].filter((value): value is string => Boolean(value))

    const toggleSubFlowExpansion = (subFlow: AgentExecution) => {
        setExpandedSubFlowIds((current) => ({
            ...current,
            [subFlow.id]: getNextSubFlowExpansionState(current[subFlow.id], subFlow),
        }))
    }

    return (
        <Card className={mergeClasses(styles.executionCard, isCompact && styles.executionCardCompact, isMobile && styles.executionCardMobile, isNested && styles.executionCardNested)}>
            <Toaster toasterId={toasterId} />

            <div className={mergeClasses(styles.executionHeader, isCompact && styles.executionHeaderCompact, isMobile && styles.executionHeaderMobile)}>
                <div className={mergeClasses(styles.executionTitle, isCompact && styles.executionTitleCompact)}>
                    <div className={mergeClasses(styles.flexRowGap, isCompact && styles.flexRowGapCompact)}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        <ExecutionStatusBadge status={execution.status} />
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
                            Started {formatTimestamp(execution.startedAt)} | {execution.duration}
                        </Caption1>
                    </div>
                    {executionMetaParts.length > 0 && (
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            {executionMetaParts.join(' | ')}
                        </Caption1>
                    )}
                </div>
                <div className={mergeClasses(styles.executionActions, isMobile && styles.executionActionsMobile)}>
                    {execution.status === 'running' && (onPause || onCancel || onDelete) && (
                        <>
                            {onPause && (
                                <Button appearance="subtle" size="small" icon={<PauseRegular />} aria-label="Pause" onClick={handlePause} />
                            )}
                            {onCancel && (
                                <Button appearance="subtle" size="small" icon={<StopRegular />} aria-label="Stop" onClick={handleCancel} />
                            )}
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

            <div className={styles.sectionHeaderRow}>
                <Text weight="semibold" className={styles.sectionHeaderLabel}>Pipeline</Text>
            </div>

            <ExecutionPipeline
                steps={pipelineDisplaySteps}
                isCompact={isCompact}
                isMobile={isMobile}
            />

            {(execution.status === 'paused' || execution.status === 'failed') && (onResume || onRetry || onDelete) && (
                <div className={mergeClasses(styles.runActions, isCompact && styles.completedActionsCompact, isMobile && styles.completedActionsMobile)}>
                    {execution.status === 'paused' && onResume && (
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
                    {execution.status === 'failed' && onRetry && (
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

            <SubFlowExecutionList
                subFlows={subFlows}
                expandedSubFlowIds={expandedSubFlowIds}
                isCompact={isCompact}
                isMobile={isMobile}
                onToggleSubFlow={toggleSubFlowExpansion}
                renderNestedExecution={(subFlow) => (
                    <ExecutionCard
                        execution={subFlow}
                        workItems={workItems}
                        onPause={onPause}
                        onCancel={onCancel}
                        onResume={onResume}
                        onRetry={onRetry}
                        onDelete={onDelete}
                        onViewDocs={onViewDocs}
                        nested
                    />
                )}
            />
        </Card>
    )
}
