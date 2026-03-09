import {
    makeStyles,
    tokens,
    Title3,
    Button,
    Badge,
    mergeClasses,
    ToggleButton,
    Tab,
    TabList,
} from '@fluentui/react-components'
import { ArrowClockwiseRegular, CodeRegular, DeleteRegular } from '@fluentui/react-icons'
import { useEffect, useMemo, useRef, useState } from 'react'
import type { AgentExecution, LogEntry } from '../../models'
import { useIsMobile } from '../../hooks'

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
        maxHeight: 'min(62vh, 560px)',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground1,
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
        borderRadius: tokens.borderRadiusMedium,
    },
    logPanelMobile: {
        maxHeight: 'none',
        minHeight: 0,
    },
    logHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalL,
        paddingRight: tokens.spacingHorizontalL,
        borderBottomWidth: '1px',
        borderBottomStyle: 'solid',
        borderBottomColor: tokens.colorNeutralStroke2,
    },
    logHeaderMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: tokens.spacingVerticalXS,
    },
    logHeaderTitleRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        minWidth: 0,
    },
    logHeaderActions: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        flexWrap: 'wrap',
    },
    logTitle: {
        fontSize: '14px',
    },
    logList: {
        flex: 1,
        overflow: 'auto',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        fontFamily: '"Cascadia Code", Consolas, "Courier New", monospace',
        fontSize: '12px',
        lineHeight: '20px',
    },
    logListMobile: {
        paddingTop: tokens.spacingVerticalXXS,
        paddingBottom: tokens.spacingVerticalXXS,
        paddingLeft: tokens.spacingHorizontalXS,
        paddingRight: tokens.spacingHorizontalXS,
    },
    logEntry: {
        display: 'grid',
        gridTemplateColumns: '62px auto 14px 1fr',
        gap: tokens.spacingHorizontalM,
        alignItems: 'baseline',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        borderRadius: tokens.borderRadiusSmall,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    logEntryMobile: {
        gridTemplateColumns: '56px 1fr',
        gap: tokens.spacingHorizontalS,
        alignItems: 'start',
    },
    logTime: {
        color: tokens.colorNeutralForeground4,
        fontVariantNumeric: 'tabular-nums',
        whiteSpace: 'nowrap',
    },
    logAgent: {
        fontWeight: 600,
        color: tokens.colorBrandForeground1,
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
        backgroundColor: tokens.colorNeutralForeground4,
    },
    levelDotWarn: {
        backgroundColor: tokens.colorPaletteMarigoldForeground1,
    },
    levelDotError: {
        backgroundColor: tokens.colorPaletteRedForeground1,
    },
    levelDotSuccess: {
        backgroundColor: tokens.colorPaletteGreenForeground1,
    },
    logMessage: {
        wordBreak: 'break-word',
    },
    logMessageMobile: {
        gridColumnStart: 2,
        gridColumnEnd: 3,
    },
    logLevelInfo: {
        color: tokens.colorNeutralForeground1,
    },
    logLevelWarn: {
        color: tokens.colorPaletteMarigoldForeground1,
    },
    logLevelError: {
        color: tokens.colorPaletteRedForeground1,
    },
    logLevelSuccess: {
        color: tokens.colorPaletteGreenForeground1,
    },
    emptyState: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        color: tokens.colorNeutralForeground4,
        fontStyle: 'italic',
        paddingTop: tokens.spacingVerticalXXL,
        paddingBottom: tokens.spacingVerticalXXL,
    },
    runTabsContainer: {
        padding: '6px 8px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
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
    onClear?: () => void
    isClearing?: boolean
}

export function LogPanel({ logs, executions = [], onRefresh, onClear, isClearing = false }: LogPanelProps) {
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

    const runTabs = useMemo<RunTabDefinition[]>(() => {
        const result: RunTabDefinition[] = []
        const seen = new Set<string>()
        const duplicateCounter = new Map<string, number>()
        const sortedExecutions = [...executions].sort(
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
            seen.add(execution.id)
            const inferredTitle = inferTitleFromLogs(logsByExecutionId.get(execution.id) ?? [])
            result.push({
                value: `execution:${execution.id}`,
                label: uniqueLabel(normalizeRunTitle(execution, inferredTitle)),
                count: countByExecutionId.get(execution.id) ?? 0,
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
    }, [executions, countByExecutionId, logsWithResolvedExecutionId])

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

    return (
        <div className={mergeClasses(styles.logPanel, isMobile && styles.logPanelMobile)}>
            <div className={mergeClasses(styles.logHeader, isMobile && styles.logHeaderMobile)}>
                <div className={styles.logHeaderTitleRow}>
                    <Title3 className={styles.logTitle}>Live Logs</Title3>
                    <Badge appearance="filled" color="informative" size="small">{sortedVisibleLogs.length}</Badge>
                </div>
                <div className={styles.logHeaderActions}>
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
                        aria-label="Clear logs"
                        onClick={onClear}
                        disabled={isClearing || logs.length === 0}
                    >
                        Clear
                    </Button>
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
                    <div className={styles.emptyState}>No log entries yet</div>
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
