import {
    makeStyles,
    mergeClasses,
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
    ChevronDownRegular,
    ChevronUpRegular,
    ClockRegular,
    CodeRegular,
    DocumentRegular,
    DeleteRegular,
    BranchRegular,
    CheckmarkCircleFilled,
    DismissCircleFilled,
    CircleRegular,
} from '@fluentui/react-icons'
import type { AgentExecution, AgentInfo, WorkItem } from '../../models'
import { usePreferences, useIsMobile } from '../../hooks'
import { openPullRequest, openPullRequestDiff } from './pullRequest'
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../../components/shared/InfoBadge'
import { FleetRocketLogo } from '../../components/shared'
import { useState } from 'react'

type ExecutionStepStatus = AgentInfo['status'] | 'paused' | 'queued'
type DisplayAgentInfo = Omit<AgentInfo, 'status'> & { status: ExecutionStepStatus }
type PlannedSubFlowStep = {
    workItemNumber: number
    title: string
    status: ExecutionStepStatus
    currentTask: string
    progress: number
}

const NON_ACTIONABLE_SUBFLOW_STATES = new Set<WorkItem['state']>([
    'In-PR',
    'In-PR (AI)',
    'Resolved',
    'Resolved (AI)',
    'Closed',
])

const STATUS_COLORS: Record<string, 'success' | 'warning' | 'danger' | 'subtle'> = {
    running: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'danger',
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
    detailPillRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.sm,
    },
    detailPillRowCompact: {
        gap: appTokens.space.xs,
    },
    detailPill: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        minWidth: '120px',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceRaised,
    },
    detailPillCompact: {
        minWidth: '104px',
        paddingTop: appTokens.space.xxxs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.xs,
        paddingRight: appTokens.space.xs,
    },
    detailPillMobile: {
        flex: '1 1 calc(50% - 0.5rem)',
        minWidth: 'unset',
    },
    detailPillLabel: {
        color: appTokens.color.textTertiary,
    },
    detailPillValue: {
        color: appTokens.color.textPrimary,
    },
    phaseBanner: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceRaised,
        color: appTokens.color.textSecondary,
    },
    phaseBannerRunning: {
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
        backgroundColor: appTokens.color.surfaceSelected,
        color: appTokens.color.textPrimary,
    },
    phaseBannerFailed: {
        borderTopColor: appTokens.color.danger,
        borderRightColor: appTokens.color.danger,
        borderBottomColor: appTokens.color.danger,
        borderLeftColor: appTokens.color.danger,
        color: appTokens.color.danger,
    },
    phaseBannerPaused: {
        borderTopColor: appTokens.color.info,
        borderRightColor: appTokens.color.info,
        borderBottomColor: appTokens.color.info,
        borderLeftColor: appTokens.color.info,
        color: appTokens.color.info,
    },
    phaseBannerIcon: {
        flexShrink: 0,
        color: appTokens.color.brand,
    },
    sectionHeaderRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
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
    subFlowSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        minHeight: 0,
    },
    subFlowHeader: {
        color: appTokens.color.textSecondary,
    },
    subFlowPipeline: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0px',
    },
    subFlowList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        maxHeight: 'none',
        overflowY: 'visible',
        overscrollBehavior: 'auto',
        paddingRight: 0,
    },
    subFlowListCompact: {
        maxHeight: 'none',
    },
    subFlowListMobile: {
        maxHeight: 'none',
        overflowY: 'visible',
        paddingRight: 0,
    },
    subFlowMobileList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    subFlowSummaryCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceRaised,
        boxShadow: 'none',
    },
    subFlowSummaryCardActive: {
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
        backgroundColor: appTokens.color.surfaceSelected,
    },
    subFlowSummaryHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
    },
    subFlowSummaryTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
        flex: 1,
    },
    subFlowSummaryDetails: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    subFlowSummaryProgress: {
        marginTop: appTokens.space.xxxs,
    },
    subFlowSummaryExpandButton: {
        alignSelf: 'flex-start',
    },
    subFlowExpandedContainer: {
        paddingLeft: appTokens.space.sm,
        borderLeft: `2px solid ${appTokens.color.border}`,
    },
    subFlowSummaryMeta: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        color: appTokens.color.textTertiary,
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
        case 'queued':
            return (
                <div className={shellClassName}>
                    <FleetRocketLogo size={isCompact ? 14 : 16} title="Queued sub-flow" variant="outline" className={mergeClasses(styles.stepIcon, styles.stepIconQueued)} />
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

function renderExecutionStatusBadge(status: AgentExecution['status']) {
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

    const getConnectorToneClass = (status: ExecutionStepStatus) => {
        switch (status) {
            case 'completed':
                return styles.connectorCompleted
            case 'running':
                return styles.connectorRunning
            case 'paused':
                return styles.connectorPaused
            case 'queued':
                return styles.connectorQueued
            case 'failed':
                return styles.connectorFailed
            default:
                return undefined
        }
    }

    const workItemsByNumber = new Map((workItems ?? []).map((workItem) => [workItem.workItemNumber, workItem]))
    const parentWorkItem = workItemsByNumber.get(execution.workItemId)
    const subFlowByWorkItemNumber = new Map(subFlows.map((subFlow) => [subFlow.workItemId, subFlow]))

    const mapExecutionStatusToStepStatus = (status: AgentExecution['status']): ExecutionStepStatus => {
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

    const plannedSubFlowSteps: PlannedSubFlowStep[] = (() => {
        const plannedChildren = (parentWorkItem?.childWorkItemNumbers ?? [])
            .map((childNumber) => workItemsByNumber.get(childNumber))
            .filter((child): child is WorkItem => Boolean(child))
            .filter((child) => !NON_ACTIONABLE_SUBFLOW_STATES.has(child.state))

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

        return [...steps, ...orphanLiveSteps]
    })()
    const displayedSubFlowCount = execution.executionMode === 'orchestration'
        ? (plannedSubFlowSteps.length > 0 ? plannedSubFlowSteps.length : subFlows.length)
        : 0

    const toggleSubFlowExpansion = (subFlowId: string) => {
        setExpandedSubFlowIds((current) => ({
            ...current,
            [subFlowId]: !current[subFlowId],
        }))
    }

    const isSubFlowExpandedByDefault = (subFlow: AgentExecution) =>
        subFlow.status === 'running' ||
        subFlow.status === 'paused' ||
        subFlow.status === 'failed'

    const renderSubFlowSummaryCard = (subFlow: AgentExecution) => {
        const isExpanded = expandedSubFlowIds[subFlow.id] ?? isSubFlowExpandedByDefault(subFlow)
        const isActiveSubFlow =
            subFlow.status === 'running' ||
            subFlow.status === 'paused' ||
            subFlow.status === 'failed'

        return (
            <div
                key={subFlow.id}
                className={mergeClasses(
                    styles.subFlowSummaryCard,
                    isActiveSubFlow && styles.subFlowSummaryCardActive,
                )}
            >
                <div className={styles.subFlowSummaryHeader}>
                    <div className={styles.subFlowSummaryTitle}>
                        <div className={styles.flexRowGap}>
                            <Text weight="semibold">#{subFlow.workItemId}</Text>
                            {renderExecutionStatusBadge(subFlow.status)}
                        </div>
                        <Text
                            weight="semibold"
                            className={mergeClasses(styles.titleText, styles.titleTextMobile)}
                        >
                            {subFlow.workItemTitle}
                        </Text>
                    </div>
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={isExpanded ? <ChevronUpRegular /> : <ChevronDownRegular />}
                        onClick={() => toggleSubFlowExpansion(subFlow.id)}
                    >
                        {isExpanded ? 'Hide' : 'Details'}
                    </Button>
                </div>
                <div className={styles.subFlowSummaryDetails}>
                    <Caption1 className={styles.taskCaption}>
                        {subFlow.currentPhase || 'Waiting on sub-flow execution'}
                    </Caption1>
                    <div className={styles.subFlowSummaryMeta}>
                        <Caption1>Started {formatTimestamp(subFlow.startedAt)}</Caption1>
                        <Caption1>•</Caption1>
                        <Caption1>{subFlow.duration}</Caption1>
                        {subFlow.branchName && (
                            <>
                                <Caption1>•</Caption1>
                                <Caption1>{subFlow.branchName}</Caption1>
                            </>
                        )}
                    </div>
                    {subFlow.status === 'running' && subFlow.progress > 0 && (
                        <ProgressBar
                            className={styles.subFlowSummaryProgress}
                            value={Math.max(0, Math.min(subFlow.progress, 1))}
                            thickness="medium"
                            color="brand"
                        />
                    )}
                </div>
                {isExpanded && (
                    <div className={styles.subFlowExpandedContainer}>
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
                    </div>
                )}
            </div>
        )
    }

    return (
        <Card className={mergeClasses(styles.executionCard, isCompact && styles.executionCardCompact, isMobile && styles.executionCardMobile, isNested && styles.executionCardNested)}>
            <Toaster toasterId={toasterId} />

            <div className={mergeClasses(styles.executionHeader, isCompact && styles.executionHeaderCompact, isMobile && styles.executionHeaderMobile)}>
                <div className={mergeClasses(styles.executionTitle, isCompact && styles.executionTitleCompact)}>
                    <div className={mergeClasses(styles.flexRowGap, isCompact && styles.flexRowGapCompact)}>
                        <Text weight="semibold">#{execution.workItemId}</Text>
                        {renderExecutionStatusBadge(execution.status)}
                        {execution.executionMode === 'orchestration' && (
                            <InfoBadge appearance="tint" size="small">
                                Flow
                            </InfoBadge>
                        )}
                        {reviewLoopLabel && (
                            isAutoRemediating ? (
                                <Badge appearance="tint" color="warning" size="small">
                                    {reviewLoopLabel}
                                </Badge>
                            ) : (
                                <InfoBadge appearance="tint" size="small">
                                    {reviewLoopLabel}
                                </InfoBadge>
                            )
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
                    {execution.executionMode === 'orchestration' && displayedSubFlowCount > 0 && (
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            Sub-flows: {displayedSubFlowCount}
                        </Caption1>
                    )}
                    {reviewLoopCount > 0 && lastReviewRecommendation && !isAutoRemediating && (
                        <Caption1 className={isCompact ? styles.metaCaptionCompact : undefined}>
                            Final review outcome: {lastReviewRecommendation}
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

            <div className={mergeClasses(styles.detailPillRow, isCompact && styles.detailPillRowCompact)}>
                <div className={mergeClasses(styles.detailPill, isCompact && styles.detailPillCompact, isMobile && styles.detailPillMobile)}>
                    <Caption1 className={styles.detailPillLabel}>Mode</Caption1>
                    <Text size={200} weight="semibold" className={styles.detailPillValue}>{runModeLabel}</Text>
                </div>
                <div className={mergeClasses(styles.detailPill, isCompact && styles.detailPillCompact, isMobile && styles.detailPillMobile)}>
                    <Caption1 className={styles.detailPillLabel}>Phase</Caption1>
                    <Text size={200} weight="semibold" className={styles.detailPillValue}>{activePhaseLabel}</Text>
                </div>
                <div className={mergeClasses(styles.detailPill, isCompact && styles.detailPillCompact, isMobile && styles.detailPillMobile)}>
                    <Caption1 className={styles.detailPillLabel}>Output</Caption1>
                    <Text size={200} weight="semibold" className={styles.detailPillValue}>{outputLabel}</Text>
                </div>
                {displayedSubFlowCount > 0 && (
                    <div className={mergeClasses(styles.detailPill, isCompact && styles.detailPillCompact, isMobile && styles.detailPillMobile)}>
                        <Caption1 className={styles.detailPillLabel}>Sub-flows</Caption1>
                        <Text size={200} weight="semibold" className={styles.detailPillValue}>{displayedSubFlowCount}</Text>
                    </div>
                )}
            </div>

            <div
                className={mergeClasses(
                    styles.phaseBanner,
                    execution.status === 'running' && styles.phaseBannerRunning,
                    execution.status === 'failed' && styles.phaseBannerFailed,
                    execution.status === 'paused' && styles.phaseBannerPaused,
                )}
            >
                <FleetRocketLogo size={16} title="Run phase" variant="outline" className={styles.phaseBannerIcon} />
                <Caption1>{activePhaseLabel}</Caption1>
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
                <InfoBadge appearance="tint" size="small">
                    {agents.length + (execution.executionMode === 'orchestration' ? plannedSubFlowSteps.length : 0)} steps
                </InfoBadge>
            </div>

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
                {execution.executionMode === 'orchestration' && plannedSubFlowSteps.map((subFlowStep, index) => {
                    const isRunning = subFlowStep.status === 'running'
                    const previousStatus = index === 0
                        ? (agents.length > 0 ? agents[agents.length - 1].status : null)
                        : plannedSubFlowSteps[index - 1].status
                    const isLast = index === plannedSubFlowSteps.length - 1

                    return (
                        <div key={`subflow-${subFlowStep.workItemNumber}`} className={mergeClasses(styles.agentStep, isCompact && styles.agentStepCompact)}>
                            <div className={mergeClasses(styles.stepGutter, isCompact && styles.stepGutterCompact)}>
                                <div
                                    className={mergeClasses(
                                        styles.connectorSegment,
                                        styles.connectorTop,
                                        previousStatus ? getConnectorToneClass(previousStatus) : undefined,
                                    )}
                                />
                                {!isLast && (
                                    <div
                                        className={mergeClasses(
                                            styles.connectorSegment,
                                            styles.connectorBottom,
                                            getConnectorToneClass(subFlowStep.status),
                                        )}
                                    />
                                )}
                                <AgentStepIcon status={subFlowStep.status} isCompact={isCompact} />
                            </div>

                            <div className={styles.stepBody}>
                                <Text
                                    size={200}
                                    weight="semibold"
                                    className={mergeClasses(styles.roleName, isCompact && styles.roleNameCompact)}
                                >
                                    Sub-flow #{subFlowStep.workItemNumber}
                                </Text>
                                <Caption1
                                    className={mergeClasses(
                                        isRunning ? styles.taskCaptionRunning : styles.taskCaption,
                                        isCompact && styles.taskCaptionCompact,
                                        isMobile && styles.taskCaptionMobile,
                                    )}
                                >
                                    {subFlowStep.currentTask}
                                </Caption1>
                                {isRunning && subFlowStep.progress > 0 && (
                                    <ProgressBar
                                        className={mergeClasses(styles.stepProgress, isCompact && styles.stepProgressCompact)}
                                        value={subFlowStep.progress}
                                        thickness="medium"
                                        color="brand"
                                    />
                                )}
                            </div>

                            <div className={styles.stepTrailing}>
                                {isRunning && subFlowStep.progress > 0 && subFlowStep.progress < 1 && (
                                    <Text className={mergeClasses(styles.progressPercent, isCompact && styles.progressPercentCompact)}>
                                        {Math.round(subFlowStep.progress * 100)}%
                                    </Text>
                                )}
                            </div>
                        </div>
                    )
                })}
            </div>

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

            {subFlows.length > 0 && (
                <div className={styles.subFlowSection}>
                    <Divider className={isCompact ? styles.compactDivider : undefined} />
                    <div className={styles.sectionHeaderRow}>
                        <Text weight="semibold" className={styles.subFlowHeader}>
                            Sub-flows
                        </Text>
                        <InfoBadge appearance="tint" size="small">
                            {subFlows.length} run{subFlows.length === 1 ? '' : 's'}
                        </InfoBadge>
                    </div>
                    <div className={mergeClasses(styles.subFlowList, isCompact && styles.subFlowListCompact, isMobile && styles.subFlowListMobile)}>
                        <div className={styles.subFlowMobileList}>
                            {subFlows.map(renderSubFlowSummaryCard)}
                        </div>
                    </div>
                </div>
            )}
        </Card>
    )
}
