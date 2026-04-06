import {
    makeStyles,
    Title3,
    Text,
    Caption1,
    Button,
    mergeClasses,
    ToggleButton,
    Tab,
    TabList,
} from '@fluentui/react-components'
import { ArrowClockwiseRegular, CodeRegular, DeleteRegular } from '@fluentui/react-icons'
import { useEffect, useMemo, useRef, useState } from 'react'
import type { AgentExecution, LogEntry } from '../../models'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../../components/shared/InfoBadge'

const LOG_LEVEL_CLASSES: Record<string, 'logLevelInfo' | 'logLevelWarn' | 'logLevelError' | 'logLevelSuccess'> = {
    info: 'logLevelInfo',
    warn: 'logLevelWarn',
    error: 'logLevelError',
    success: 'logLevelSuccess',
}

/** Format an ISO string to HH:MM:SS (24-hour) for the log gutter. */
function formatLogTime(iso: string): string {
    try {
        const d = new Date(iso)
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
    } catch {
        return iso
    }
}

const useStyles = makeStyles({
    logPanel: {
        display: 'flex',
        flexDirection: 'column',
        maxHeight: appTokens.size.logPanelMaxHeight,
        overflow: 'hidden',
        backgroundColor: appTokens.color.surface,
        border: appTokens.border.subtle,
        borderRadius: appTokens.radius.md,
    },
    logPanelMobile: {
        maxHeight: 'none',
        minHeight: 0,
    },
    logHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        borderBottom: appTokens.border.subtle,
    },
    logHeaderMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: appTokens.space.xs,
    },
    logHeaderTitleRow: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: '8px',
        minWidth: 0,
    },
    logHeaderTitleStack: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    logHeaderSubtitle: {
        color: appTokens.color.textTertiary,
    },
    logHeaderActions: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        flexWrap: 'wrap',
    },
    logHeaderActionsMobile: {
        width: '100%',
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        gap: '0.375rem',
    },
    logTitle: {
        fontSize: '14px',
    },
    logList: {
        flex: 1,
        overflow: 'auto',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        fontFamily: '"Cascadia Code", Consolas, "Courier New", monospace',
        fontSize: '12px',
        lineHeight: '20px',
    },
    logListMobile: {
        paddingTop: appTokens.space.xxxs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.xxs,
        paddingRight: appTokens.space.xxs,
    },
    logEntry: {
        display: 'grid',
        gridTemplateColumns: '62px auto 14px 1fr',
        gap: appTokens.space.md,
        alignItems: 'baseline',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.sm,
        ':hover': {
            backgroundColor: appTokens.color.pageBackground,
        },
    },
    logEntryMobile: {
        gridTemplateColumns: '56px 1fr',
        gap: appTokens.space.sm,
        alignItems: 'start',
        paddingLeft: appTokens.space.xxs,
        paddingRight: appTokens.space.xxs,
    },
    logTime: {
        color: appTokens.color.textMuted,
        fontVariantNumeric: 'tabular-nums',
        whiteSpace: 'nowrap',
    },
    logAgent: {
        fontWeight: 600,
        color: appTokens.color.brand,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    logAgentMobile: {
        gridColumnStart: 2,
        gridColumnEnd: 3,
    },
    levelDot: {
        display: 'inline-block',
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        flexShrink: 0,
        alignSelf: 'center',
    },
    levelDotInfo: {
        backgroundColor: appTokens.color.textMuted,
    },
    levelDotWarn: {
        backgroundColor: appTokens.color.warning,
    },
    levelDotError: {
        backgroundColor: appTokens.color.danger,
    },
    levelDotSuccess: {
        backgroundColor: appTokens.color.success,
    },
    logMessage: {
        wordBreak: 'break-word',
    },
    logMessageMobile: {
        gridColumnStart: 2,
        gridColumnEnd: 3,
    },
    logLevelInfo: {
        color: appTokens.color.textPrimary,
    },
    logLevelWarn: {
        color: appTokens.color.warning,
    },
    logLevelError: {
        color: appTokens.color.danger,
    },
    logLevelSuccess: {
        color: appTokens.color.success,
    },
    emptyState: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        paddingTop: appTokens.space.xxl,
        paddingBottom: appTokens.space.xxl,
    },
    emptyStateBody: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.xs,
        color: appTokens.color.textMuted,
        textAlign: 'center',
    },
    runTabsContainer: {
        padding: '6px 8px',
        borderBottom: appTokens.border.subtle,
    },
    runTabsContainerMobile: {
        paddingTop: '4px',
        paddingBottom: '4px',
        paddingLeft: '6px',
        paddingRight: '6px',
    },
    runTabsList: {
        overflowX: 'auto',
        whiteSpace: 'nowrap',
    },
})

const LEVEL_DOT_CLASSES: Record<string, 'levelDotInfo' | 'levelDotWarn' | 'levelDotError' | 'levelDotSuccess'> = {
    info: 'levelDotInfo',
    warn: 'levelDotWarn',
    error: 'levelDotError',
    success: 'levelDotSuccess',
}

interface LogEntryWithExecutionId extends LogEntry {
    resolvedExecutionId: string | null
}

function flattenExecutions(executions: AgentExecution[]): AgentExecution[] {
    const flattened: AgentExecution[] = []

    const visit = (execution: AgentExecution) => {
        flattened.push(execution)
        for (const subFlow of execution.subFlows ?? []) {
            visit(subFlow)
        }
    }

    for (const execution of executions) {
        visit(execution)
    }

    return flattened
}

function extractExecutionIdFromMessage(message: string): string | null {
    const match = /\bExecution\s+([a-z0-9]{8,32})\b/i.exec(message)
    return match?.[1]?.toLowerCase() ?? null
}

function inferTitleFromLogs(logs: Array<{ message: string }>): string | null {
    for (const log of logs) {
        const match = /work item #\d+:\s*(.+)$/i.exec(log.message)
        if (match?.[1]?.trim()) {
            return match[1].trim()
        }
    }

    return null
}

function normalizeRunTitle(execution?: AgentExecution, inferredTitle?: string | null): string {
    if (execution?.workItemTitle?.trim()) {
        return execution.workItemTitle.trim()
    }

    if (execution?.workItemId) {
        return `Work Item #${execution.workItemId}`
    }

    if (inferredTitle?.trim()) {
        return inferredTitle.trim()
    }

    return 'Run'
}

interface RunTabDefinition {
    value: string
    label: string
    count: number
}

interface LogPanelProps {
    logs: LogEntry[]
    executions?: AgentExecution[]
    onRefresh?: () => void
    onClearAll?: () => void
    onClearRun?: (executionId: string) => void
    isClearingAll?: boolean
    isClearingRun?: boolean
}

export function LogPanel({
    logs,
    executions = [],
    onRefresh,
    onClearAll,
    onClearRun,
    isClearingAll = false,
    isClearingRun = false,
}: LogPanelProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const [showDetailed, setShowDetailed] = useState(false)
    const [selectedRun, setSelectedRun] = useState<string>('all')
    const logListRef = useRef<HTMLDivElement | null>(null)

    const baseLogs = useMemo(
        () => showDetailed ? logs : logs.filter((log) => !log.isDetailed),
        [logs, showDetailed],
    )

    const logsWithResolvedExecutionId = useMemo<LogEntryWithExecutionId[]>(() => {
        const sortedLogs = [...baseLogs].sort((left, right) => {
            const leftTime = Date.parse(left.time)
            const rightTime = Date.parse(right.time)

            if (Number.isNaN(leftTime) || Number.isNaN(rightTime)) {
                return left.time.localeCompare(right.time)
            }

            return leftTime - rightTime
        })

        let activeExecutionId: string | null = null

        return sortedLogs.map((log) => {
            const explicitExecutionId = log.executionId?.trim() || extractExecutionIdFromMessage(log.message)
            if (explicitExecutionId) {
                activeExecutionId = explicitExecutionId
                return { ...log, resolvedExecutionId: explicitExecutionId }
            }

            return { ...log, resolvedExecutionId: activeExecutionId }
        })
    }, [baseLogs])

    const { generalCount, countByExecutionId } = useMemo(() => {
        const counts = new Map<string, number>()
        let unscopedCount = 0

        for (const log of logsWithResolvedExecutionId) {
            if (!log.resolvedExecutionId) {
                unscopedCount += 1
                continue
            }

            counts.set(log.resolvedExecutionId, (counts.get(log.resolvedExecutionId) ?? 0) + 1)
        }

        return { generalCount: unscopedCount, countByExecutionId: counts }
    }, [logsWithResolvedExecutionId])

    const flattenedExecutionList = useMemo(
        () => flattenExecutions(executions),
        [executions],
    )

    const runTabs = useMemo<RunTabDefinition[]>(() => {
        const result: RunTabDefinition[] = []
        const seen = new Set<string>()
        const duplicateCounter = new Map<string, number>()
        const sortedExecutions = [...flattenedExecutionList].sort(
            (left, right) => Date.parse(right.startedAt) - Date.parse(left.startedAt),
        )
        const logsByExecutionId = new Map<string, LogEntryWithExecutionId[]>()

        for (const log of logsWithResolvedExecutionId) {
            if (!log.resolvedExecutionId) {
                continue
            }

            const list = logsByExecutionId.get(log.resolvedExecutionId)
            if (list) {
                list.push(log)
            } else {
                logsByExecutionId.set(log.resolvedExecutionId, [log])
            }
        }

        const uniqueLabel = (baseLabel: string): string => {
            const next = (duplicateCounter.get(baseLabel) ?? 0) + 1
            duplicateCounter.set(baseLabel, next)
            return next === 1 ? baseLabel : `${baseLabel} (${next})`
        }

        for (const execution of sortedExecutions) {
            const count = countByExecutionId.get(execution.id) ?? 0
            if (count === 0) {
                continue
            }

            seen.add(execution.id)
            const inferredTitle = inferTitleFromLogs(logsByExecutionId.get(execution.id) ?? [])
            const baseLabel = normalizeRunTitle(execution, inferredTitle)
            result.push({
                value: `execution:${execution.id}`,
                label: uniqueLabel(execution.parentExecutionId ? `Sub-flow: ${baseLabel}` : baseLabel),
                count,
            })
        }

        let unknownRunNumber = 0
        for (const [executionId, count] of countByExecutionId.entries()) {
            if (seen.has(executionId)) {
                continue
            }

            unknownRunNumber += 1
            const inferredTitle = inferTitleFromLogs(logsByExecutionId.get(executionId) ?? [])
            const fallbackTitle = inferredTitle ?? `Run ${unknownRunNumber}`
            result.push({
                value: `execution:${executionId}`,
                label: uniqueLabel(fallbackTitle),
                count,
            })
        }

        return result
    }, [flattenedExecutionList, countByExecutionId, logsWithResolvedExecutionId])

    useEffect(() => {
        const hasSelectedExecutionTab = selectedRun.startsWith('execution:') && runTabs.some((tab) => tab.value === selectedRun)
        const hasSelectedGeneralTab = selectedRun === 'general' && generalCount > 0
        const isSelectionValid = selectedRun === 'all' || hasSelectedExecutionTab || hasSelectedGeneralTab
        if (!isSelectionValid) {
            setSelectedRun('all')
        }
    }, [generalCount, runTabs, selectedRun])

    const visibleLogs = useMemo(() => {
        if (selectedRun === 'all') {
            return logsWithResolvedExecutionId
        }

        if (selectedRun === 'general') {
            return logsWithResolvedExecutionId.filter((log) => !log.resolvedExecutionId)
        }

        if (selectedRun.startsWith('execution:')) {
            const executionId = selectedRun.slice('execution:'.length)
            return logsWithResolvedExecutionId.filter((log) => log.resolvedExecutionId === executionId)
        }

        return logsWithResolvedExecutionId
    }, [logsWithResolvedExecutionId, selectedRun])

    const selectedExecutionId = useMemo(
        () => selectedRun.startsWith('execution:') ? selectedRun.slice('execution:'.length) : null,
        [selectedRun],
    )

    const sortedVisibleLogs = useMemo(
        () => [...visibleLogs].sort((left, right) => {
            const leftTime = Date.parse(left.time)
            const rightTime = Date.parse(right.time)

            if (Number.isNaN(leftTime) || Number.isNaN(rightTime)) {
                return left.time.localeCompare(right.time)
            }

            return leftTime - rightTime
        }),
        [visibleLogs],
    )

    const latestLogCursor = useMemo(() => {
        if (sortedVisibleLogs.length === 0) {
            return ''
        }

        const last = sortedVisibleLogs[sortedVisibleLogs.length - 1]
        return `${last.time}|${last.agent}|${last.level}|${last.message}`
    }, [sortedVisibleLogs])

    useEffect(() => {
        const container = logListRef.current
        if (!container) {
            return
        }

        container.scrollTop = container.scrollHeight
    }, [latestLogCursor, selectedRun])

    const selectedRunLabel = useMemo(() => {
        if (selectedRun === 'all') {
            return 'Showing every execution and system log'
        }

        if (selectedRun === 'general') {
            return 'Showing logs not tied to a specific run'
        }

        const activeRun = runTabs.find((tab) => tab.value === selectedRun)
        return activeRun ? `Showing logs for ${activeRun.label}` : 'Showing the selected run'
    }, [runTabs, selectedRun])

    const emptyStateMessage = useMemo(() => {
        if (selectedRun === 'all') {
            return {
                title: 'No logs yet',
                detail: 'Run activity will appear here once Fleet starts writing execution updates.',
            }
        }

        if (selectedRun === 'general') {
            return {
                title: 'No general logs',
                detail: 'This project does not have any unscoped system logs right now.',
            }
        }

        return {
            title: 'No logs for this run',
            detail: 'This run has not emitted any visible log entries for the current filter yet.',
        }
    }, [selectedRun])

    return (
        <div className={mergeClasses(styles.logPanel, isMobile && styles.logPanelMobile)}>
            <div className={mergeClasses(styles.logHeader, isMobile && styles.logHeaderMobile)}>
                <div className={styles.logHeaderTitleRow}>
                    <div className={styles.logHeaderTitleStack}>
                        <Title3 className={styles.logTitle}>Live Logs</Title3>
                        <Caption1 className={styles.logHeaderSubtitle}>{selectedRunLabel}</Caption1>
                    </div>
                    <InfoBadge appearance="filled" size="small">{sortedVisibleLogs.length}</InfoBadge>
                </div>
                <div className={mergeClasses(styles.logHeaderActions, isMobile && styles.logHeaderActionsMobile)}>
                    <ToggleButton
                        appearance="subtle"
                        size="small"
                        icon={<CodeRegular />}
                        checked={showDetailed}
                        onClick={() => setShowDetailed(prev => !prev)}
                    >
                        Detailed
                    </ToggleButton>
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<DeleteRegular />}
                        aria-label="Clear all logs"
                        onClick={onClearAll}
                        disabled={isClearingAll || logs.length === 0}
                    >
                        Clear All
                    </Button>
                    {selectedExecutionId && onClearRun && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<DeleteRegular />}
                            aria-label="Clear logs for this run"
                            onClick={() => onClearRun(selectedExecutionId)}
                            disabled={isClearingRun || sortedVisibleLogs.length === 0}
                        >
                            Clear Run
                        </Button>
                    )}
                    <Button appearance="subtle" size="small" icon={<ArrowClockwiseRegular />} aria-label="Refresh logs" onClick={onRefresh} />
                </div>
            </div>
            <div className={mergeClasses(styles.runTabsContainer, isMobile && styles.runTabsContainerMobile)}>
                <TabList
                    selectedValue={selectedRun}
                    onTabSelect={(_event, data) => setSelectedRun(data.value as string)}
                    size="small"
                    className={styles.runTabsList}
                >
                    <Tab value="all">All ({logsWithResolvedExecutionId.length})</Tab>
                    {generalCount > 0 && <Tab value="general">General ({generalCount})</Tab>}
                    {runTabs.map((runTab) => (
                        <Tab key={runTab.value} value={runTab.value}>
                            {runTab.label} ({runTab.count})
                        </Tab>
                    ))}
                </TabList>
            </div>
            <div ref={logListRef} className={mergeClasses(styles.logList, isMobile && styles.logListMobile)}>
                {sortedVisibleLogs.length === 0 && (
                    <div className={styles.emptyState}>
                        <div className={styles.emptyStateBody}>
                            <Text weight="semibold">{emptyStateMessage.title}</Text>
                            <Caption1>{emptyStateMessage.detail}</Caption1>
                        </div>
                    </div>
                )}
                {sortedVisibleLogs.map((log, i) => (
                    <div key={i} className={mergeClasses(styles.logEntry, isMobile && styles.logEntryMobile)}>
                        <span className={styles.logTime}>{formatLogTime(log.time)}</span>
                        <span className={mergeClasses(styles.logAgent, isMobile && styles.logAgentMobile)}>{log.agent}</span>
                        <span className={mergeClasses(styles.levelDot, styles[LEVEL_DOT_CLASSES[log.level]])} />
                        <span className={mergeClasses(styles.logMessage, styles[LOG_LEVEL_CLASSES[log.level]], isMobile && styles.logMessageMobile)}>{log.message}</span>
                    </div>
                ))}
            </div>
        </div>
    )
}
