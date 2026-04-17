import { Button, Caption1, Divider, makeStyles, mergeClasses, ProgressBar, Text } from '@fluentui/react-components'
import { ChevronDownRegular, ChevronUpRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import type { AgentExecution } from '../../models'
import { appTokens } from '../../styles/appTokens'
import { isSubFlowExpandedByDefault } from './subFlowExpansion'
import { ExecutionStatusBadge } from './ExecutionStatusBadge'
import { formatTimestamp } from './executionFormatting'

const useStyles = makeStyles({
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        minHeight: 0,
    },
    headerLabel: {
        color: appTokens.color.textSecondary,
    },
    sectionHeaderRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    list: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        maxHeight: 'none',
        overflowY: 'visible',
        overscrollBehavior: 'auto',
        paddingRight: 0,
    },
    mobileList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    summaryCard: {
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
    summaryCardActive: {
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
        backgroundColor: appTokens.color.surfaceSelected,
    },
    summaryHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: appTokens.space.sm,
    },
    summaryTitle: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
        flex: 1,
    },
    summaryDetails: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    titleRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    titleText: {
        whiteSpace: 'normal',
        overflow: 'visible',
        textOverflow: 'clip',
    },
    metaText: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        color: appTokens.color.textTertiary,
    },
    summaryProgress: {
        marginTop: appTokens.space.xxxs,
    },
    expandedContainer: {
        paddingLeft: appTokens.space.sm,
        borderLeft: `2px solid ${appTokens.color.border}`,
    },
})

interface SubFlowExecutionListProps {
    subFlows: AgentExecution[]
    expandedSubFlowIds: Record<string, boolean>
    isCompact: boolean
    isMobile: boolean
    onToggleSubFlow: (subFlow: AgentExecution) => void
    renderNestedExecution: (subFlow: AgentExecution) => ReactNode
}

export function SubFlowExecutionList({
    subFlows,
    expandedSubFlowIds,
    isCompact,
    isMobile,
    onToggleSubFlow,
    renderNestedExecution,
}: SubFlowExecutionListProps) {
    const styles = useStyles()

    if (subFlows.length === 0) {
        return null
    }

    return (
        <div className={styles.section}>
            <Divider />
            <div className={styles.sectionHeaderRow}>
                <Text weight="semibold" className={styles.headerLabel}>
                    Sub-flows
                </Text>
            </div>
            <div className={styles.list}>
                <div className={styles.mobileList}>
                    {subFlows.map((subFlow) => {
                        const isExpanded = expandedSubFlowIds[subFlow.id] ?? isSubFlowExpandedByDefault(subFlow)
                        const isActiveSubFlow =
                            subFlow.status === 'running' ||
                            subFlow.status === 'paused' ||
                            subFlow.status === 'failed'

                        return (
                            <div
                                key={subFlow.id}
                                className={mergeClasses(
                                    styles.summaryCard,
                                    isActiveSubFlow && styles.summaryCardActive,
                                )}
                            >
                                <div className={styles.summaryHeader}>
                                    <div className={styles.summaryTitle}>
                                        <div className={styles.titleRow}>
                                            <Text weight="semibold">#{subFlow.workItemId}</Text>
                                            <ExecutionStatusBadge status={subFlow.status} />
                                        </div>
                                        <Text weight="semibold" className={styles.titleText}>
                                            {subFlow.workItemTitle}
                                        </Text>
                                    </div>
                                    <Button
                                        appearance="subtle"
                                        size="small"
                                        icon={isExpanded ? <ChevronUpRegular /> : <ChevronDownRegular />}
                                        onClick={() => onToggleSubFlow(subFlow)}
                                    >
                                        {isExpanded ? 'Hide' : 'Show'}
                                    </Button>
                                </div>
                                <div className={styles.summaryDetails}>
                                    <Caption1>
                                        {subFlow.currentPhase || 'Waiting on sub-flow execution'}
                                    </Caption1>
                                    <Caption1 className={styles.metaText}>
                                        {[
                                            `Started ${formatTimestamp(subFlow.startedAt)}`,
                                            subFlow.duration,
                                            subFlow.branchName || null,
                                        ]
                                            .filter((value): value is string => Boolean(value))
                                            .join(' | ')}
                                    </Caption1>
                                    {subFlow.status === 'running' && subFlow.progress > 0 && (
                                        <ProgressBar
                                            className={styles.summaryProgress}
                                            value={Math.max(0, Math.min(subFlow.progress, 1))}
                                            thickness={isCompact || isMobile ? 'medium' : 'large'}
                                            color="brand"
                                        />
                                    )}
                                </div>
                                {isExpanded && (
                                    <div className={styles.expandedContainer}>
                                        {renderNestedExecution(subFlow)}
                                    </div>
                                )}
                            </div>
                        )
                    })}
                </div>
            </div>
        </div>
    )
}
