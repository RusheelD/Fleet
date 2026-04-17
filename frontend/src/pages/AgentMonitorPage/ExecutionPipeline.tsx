import { makeStyles, mergeClasses, ProgressBar, Spinner, Text, Caption1 } from '@fluentui/react-components'
import { CheckmarkCircleFilled, CircleRegular, DismissCircleFilled, PauseRegular, StopRegular } from '@fluentui/react-icons'
import { FleetRocketLogo } from '../../components/shared'
import { appTokens } from '../../styles/appTokens'
import type { ExecutionStepStatus, PipelineDisplayStep } from './pipelineDisplay'

const useStyles = makeStyles({
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
})

function formatRunningProgressPercent(progress: number): string {
    const clampedPercent = Math.min(99.95, Math.max(0, progress * 100))
    const precision = clampedPercent < 1 ? 2 : 1
    const multiplier = precision === 2 ? 100 : 10
    const flooredPercent = Math.floor(clampedPercent * multiplier) / multiplier

    if (Math.abs(flooredPercent - Math.round(flooredPercent)) < 0.001) {
        return `${Math.round(flooredPercent)}%`
    }

    return `${flooredPercent.toFixed(precision)}%`
}

function AgentStepIcon({ status, isCompact }: { status: ExecutionStepStatus; isCompact: boolean }) {
    const styles = useStyles()
    const shellClassName = mergeClasses(styles.stepIconShell, isCompact && styles.stepIconShellCompact)

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

function getConnectorToneClass(status: ExecutionStepStatus, styles: ReturnType<typeof useStyles>) {
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

interface ExecutionPipelineProps {
    steps: PipelineDisplayStep[]
    isCompact: boolean
    isMobile: boolean
}

export function ExecutionPipeline({ steps, isCompact, isMobile }: ExecutionPipelineProps) {
    const styles = useStyles()

    return (
        <div className={styles.pipeline}>
            {steps.map((step, index) => {
                const isFirst = index === 0
                const isLast = index === steps.length - 1
                const isRunning = step.status === 'running'
                const previousStatus = isFirst ? null : steps[index - 1].status

                return (
                    <div key={step.key} className={mergeClasses(styles.agentStep, isCompact && styles.agentStepCompact)}>
                        <div className={mergeClasses(styles.stepGutter, isCompact && styles.stepGutterCompact)}>
                            {!isFirst && (
                                <div
                                    className={mergeClasses(
                                        styles.connectorSegment,
                                        styles.connectorTop,
                                        previousStatus ? getConnectorToneClass(previousStatus, styles) : undefined,
                                    )}
                                />
                            )}
                            {!isLast && (
                                <div
                                    className={mergeClasses(
                                        styles.connectorSegment,
                                        styles.connectorBottom,
                                        getConnectorToneClass(step.status, styles),
                                    )}
                                />
                            )}
                            <AgentStepIcon status={step.status} isCompact={isCompact} />
                        </div>

                        <div className={styles.stepBody}>
                            <Text
                                size={200}
                                weight="semibold"
                                className={mergeClasses(styles.roleName, isCompact && styles.roleNameCompact)}
                            >
                                {step.title}
                            </Text>
                            <Caption1
                                className={mergeClasses(
                                    isRunning ? styles.taskCaptionRunning : styles.taskCaption,
                                    isCompact && styles.taskCaptionCompact,
                                    isMobile && styles.taskCaptionMobile,
                                )}
                            >
                                {step.currentTask}
                            </Caption1>
                            {isRunning && step.progress > 0 && (
                                <ProgressBar
                                    className={mergeClasses(styles.stepProgress, isCompact && styles.stepProgressCompact)}
                                    value={step.progress}
                                    thickness="medium"
                                    color="brand"
                                />
                            )}
                        </div>

                        <div className={styles.stepTrailing}>
                            {isRunning && step.progress > 0 && step.progress < 1 && (
                                <Text className={mergeClasses(styles.progressPercent, isCompact && styles.progressPercentCompact)}>
                                    {formatRunningProgressPercent(step.progress)}
                                </Text>
                            )}
                        </div>
                    </div>
                )
            })}
        </div>
    )
}
