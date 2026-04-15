import { useState, useMemo } from 'react'
import {
    Card,
    makeStyles,
    mergeClasses,
    Tab,
    TabList,
    Toolbar,
    ToolbarButton,
    Spinner,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
    Input,
    Text,
    Caption1,
} from '@fluentui/react-components'
import {
    BotRegular,
    PlayRegular,
    PauseRegular,
    CheckmarkCircleRegular,
    ErrorCircleRegular,
    DismissCircleRegular,
    SearchRegular,
    ArrowClockwiseRegular,
} from '@fluentui/react-icons'
import { FleetRocketLogo, PageHeader } from '../../components/shared'
import { InfoBadge } from '../../components/shared/InfoBadge'
import { SummaryCard, ExecutionCard, ExecutionDocsDialog, LogPanel, StartExecutionDialog } from './'
import { getApiErrorMessage, type ExecutionDocumentation, useExecutions, useLogs, useWorkItems, useStartExecution, useCancelExecution, usePauseExecution, useResumeExecution, useRetryExecution, useExecutionDocumentation, useClearLogs, useClearExecutionLogs, useDeleteExecution } from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile, useServerEventConnection } from '../../hooks'
import { hasExecutionDocumentation } from './executionDocs'
import { appTokens, APP_NARROW_LAYOUT_MEDIA_QUERY } from '../../styles/appTokens'
import { resolveConnectionAwarePollingInterval } from '../../hooks/serverEventConnectionState'
import { executionTreeHasAnyStatus, findExecutionInCollection, flattenExecutionCollection } from '../../models/executionTree'

const LIVE_FALLBACK_POLL_MS = 5000
const IDLE_FALLBACK_POLL_MS = 15000
const ACTIVE_EXECUTION_STATUSES = new Set(['running', 'queued'] as const)
const PAUSED_EXECUTION_STATUSES = new Set(['paused'] as const)

const useStyles = makeStyles({
    page: {
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.pageX,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.pageX,
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
        backgroundColor: appTokens.color.pageBackground,
        minWidth: 0,
    },
    pageCompact: {
        paddingTop: appTokens.space.pageYMobile,
        paddingBottom: appTokens.space.pageYMobile,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
    },
    headerActions: {
        display: 'flex',
        gap: '0.5rem',
        width: '100%',
        flexWrap: 'wrap',
    },
    headerActionsMobile: {
        width: '100%',
    },
    actionsToolbar: {
        display: 'flex',
        flexWrap: 'wrap',
        width: '100%',
        rowGap: '0.375rem',
        columnGap: '0.375rem',
    },
    actionsToolbarMobile: {
        alignItems: 'stretch',
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        gap: '0.5rem',
    },
    toolbarButtonMobile: {
        flex: '1 1 120px',
    },
    summaryRow: {
        display: 'flex',
        gap: '1rem',
        marginBottom: appTokens.space.xl,
        flexWrap: 'wrap',
    },
    summaryRowCompact: {
        gap: '0.5rem',
        marginBottom: '0.75rem',
    },
    summaryRowMobile: {
        gap: '0.5rem',
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    },
    summaryIconWarning: {
        color: appTokens.color.warning,
    },
    summaryIconSuccess: {
        color: appTokens.color.success,
    },
    summaryIconDanger: {
        color: appTokens.color.danger,
    },
    summaryIconBrand: {
        color: appTokens.color.brand,
    },
    tabListSpacing: {
        marginBottom: appTokens.space.lg,
        borderBottom: appTokens.border.subtle,
        paddingBottom: appTokens.space.sm,
        overflowX: 'auto',
        whiteSpace: 'nowrap',
    },
    tabListSpacingCompact: {
        marginBottom: '0.5rem',
        paddingBottom: '0.25rem',
    },
    monitorTab: {
        borderRadius: appTokens.radius.md,
        transitionProperty: 'background-color, color, box-shadow',
        transitionDuration: appTokens.motion.fast,
        [`& .fui-Tab__icon`]: {
            color: appTokens.color.textTertiary,
        },
        [`&[aria-selected="true"]`]: {
            backgroundColor: appTokens.color.surfaceAlt,
            boxShadow: appTokens.border.activeInset,
        },
        [`&[aria-selected="true"] .fui-Tab__icon`]: {
            color: appTokens.color.brand,
        },
        [`&[aria-selected="true"] .fui-Tab__content`]: {
            color: appTokens.color.textPrimary,
        },
        [`&[aria-selected="true"]:hover .fui-Tab__icon`]: {
            color: appTokens.color.brandHover,
        },
    },
    mainContent: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.xl,
        flex: 1,
        overflow: 'hidden',
        minHeight: 0,
        [`@media ${APP_NARROW_LAYOUT_MEDIA_QUERY}`]: {
            gridTemplateColumns: '1fr',
        },
    },
    mainContentCompact: {
        gap: '0.75rem',
    },
    executionPanel: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        minHeight: 0,
    },
    executionPanelShell: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        minHeight: 0,
        flex: 1,
        paddingTop: appTokens.space.lg,
        paddingBottom: appTokens.space.lg,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        borderRadius: appTokens.radius.lg,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
    },
    executionPanelShellCompact: {
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
    },
    executionPanelHeader: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
        flexWrap: 'wrap',
    },
    executionPanelHeaderMeta: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    executionPanelSubtitle: {
        color: appTokens.color.textTertiary,
    },
    executionList: {
        flex: 1,
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
        paddingRight: '0.25rem',
    },
    executionListCompact: {
        gap: '0.5rem',
        paddingRight: 0,
    },
    searchInput: {
        maxWidth: '260px',
        minWidth: '220px',
        flex: 1,
    },
    searchInputCompact: {
        maxWidth: '200px',
        minWidth: '160px',
    },
    searchInputMobile: {
        maxWidth: 'unset',
        minWidth: '140px',
        width: '100%',
        flex: '1 1 100%',
        gridColumn: '1 / -1',
    },
    emptyExecutionState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: appTokens.space.sm,
        minHeight: '220px',
        paddingTop: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        borderRadius: appTokens.radius.lg,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceRaised,
        textAlign: 'center',
    },
    emptyExecutionStateIcon: {
        color: appTokens.color.brand,
    },
    emptyExecutionStateBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        maxWidth: '28rem',
    },
    emptyExecutionStateDetail: {
        color: appTokens.color.textTertiary,
    },
    statusStrip: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: appTokens.space.md,
        flexWrap: 'wrap',
        paddingTop: appTokens.space.md,
        paddingRight: appTokens.space.lg,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.lg,
        marginBottom: appTokens.space.md,
        borderRadius: appTokens.radius.lg,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        boxShadow: appTokens.shadow.card,
    },
    statusStripMobile: {
        paddingTop: appTokens.space.sm,
        paddingRight: appTokens.space.md,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
    },
    statusStripLive: {
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
    },
    statusStripReconnecting: {
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.infoSurface} 100%)`,
    },
    statusStripConnecting: {
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceBrand} 120%)`,
    },
    statusStripMeta: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    statusStripPills: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
    },
})

export function AgentMonitorPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const { state: serverEventState } = useServerEventConnection(projectId)
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const [tab, setTab] = useState<string>('active')
    const isLiveRunTab = tab === 'active' || tab === 'paused' || tab === 'all'
    const fallbackPollingInterval = isLiveRunTab ? LIVE_FALLBACK_POLL_MS : IDLE_FALLBACK_POLL_MS
    const executionsPollingInterval = resolveConnectionAwarePollingInterval(serverEventState, fallbackPollingInterval, fallbackPollingInterval)
    const logsPollingInterval = resolveConnectionAwarePollingInterval(serverEventState, fallbackPollingInterval, fallbackPollingInterval)
    const workItemsPollingInterval = resolveConnectionAwarePollingInterval(serverEventState, IDLE_FALLBACK_POLL_MS, IDLE_FALLBACK_POLL_MS)
    const { data: executions, isLoading: loadingExec, refetch: refetchExec } = useExecutions(projectId, {
        pollingInterval: executionsPollingInterval,
    })
    const { data: logs, isLoading: loadingLogs, refetch: refetchLogs } = useLogs(projectId, {
        pollingInterval: logsPollingInterval,
    })
    const { data: workItems, isLoading: loadingWorkItems } = useWorkItems(projectId, {
        pollingInterval: workItemsPollingInterval,
    })
    const startExecution = useStartExecution(projectId)
    const cancelExecution = useCancelExecution(projectId)
    const pauseExecution = usePauseExecution(projectId)
    const resumeExecution = useResumeExecution(projectId)
    const retryExecution = useRetryExecution(projectId)
    const fetchExecutionDocumentation = useExecutionDocumentation(projectId)
    const clearLogs = useClearLogs(projectId)
    const clearExecutionLogs = useClearExecutionLogs(projectId)
    const deleteExecution = useDeleteExecution(projectId)
    const [dialogOpen, setDialogOpen] = useState(false)
    const [selectedDocumentation, setSelectedDocumentation] = useState<ExecutionDocumentation | null>(null)
    const [searchQuery, setSearchQuery] = useState('')
    const toasterId = useId('agent-monitor-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const allExecutions = useMemo(() => executions ?? [], [executions])
    const allLogs = useMemo(() => logs ?? [], [logs])
    const flatExecutions = useMemo(() => flattenExecutionCollection(allExecutions), [allExecutions])
    const active = useMemo(
        () => allExecutions.filter((execution) => executionTreeHasAnyStatus(execution, ACTIVE_EXECUTION_STATUSES)),
        [allExecutions],
    )
    const paused = useMemo(
        () => allExecutions.filter((execution) =>
            !executionTreeHasAnyStatus(execution, ACTIVE_EXECUTION_STATUSES) &&
            executionTreeHasAnyStatus(execution, PAUSED_EXECUTION_STATUSES)),
        [allExecutions],
    )
    const completed = useMemo(() => allExecutions.filter((e) => e.status === 'completed'), [allExecutions])
    const failed = useMemo(() => allExecutions.filter((e) => e.status === 'failed'), [allExecutions])
    const cancelled = useMemo(() => allExecutions.filter((e) => e.status === 'cancelled'), [allExecutions])
    const running = useMemo(
        () => flatExecutions.filter((execution) => execution.status === 'running'),
        [flatExecutions],
    )
    const activeAgentCount = useMemo(
        () => flatExecutions.reduce(
            (acc, execution) => acc + execution.agents.filter((agent) => agent.status === 'running').length,
            0,
        ),
        [flatExecutions],
    )

    const filteredByTab =
        tab === 'active' ? active :
            tab === 'paused' ? paused :
            tab === 'completed' ? completed :
                tab === 'failed' ? failed :
                    tab === 'cancelled' ? cancelled :
                    allExecutions

    const filteredExecutions = useMemo(() => {
        if (!searchQuery) return filteredByTab
        const q = searchQuery.toLowerCase()

        const matchesExecution = (execution: typeof filteredByTab[number]): boolean =>
            execution.workItemTitle.toLowerCase().includes(q) ||
            execution.id.toLowerCase().includes(q) ||
            execution.agents.some((a) => a.role.toLowerCase().includes(q)) ||
            (execution.subFlows ?? []).some(matchesExecution)

        return filteredByTab.filter(matchesExecution)
    }, [filteredByTab, searchQuery])

    const currentTabLabel = useMemo(() => {
        switch (tab) {
            case 'active':
                return 'Active runs'
            case 'paused':
                return 'Paused runs'
            case 'completed':
                return 'Completed runs'
            case 'failed':
                return 'Failed runs'
            case 'cancelled':
                return 'Cancelled runs'
            default:
                return 'All runs'
        }
    }, [tab])

    const executionPanelSubtitle = useMemo(() => {
        if (searchQuery.trim()) {
            return `Showing ${filteredExecutions.length} of ${filteredByTab.length} runs matching "${searchQuery.trim()}"`
        }

        if (filteredExecutions.length === 0) {
            return `No runs are currently visible in ${currentTabLabel.toLowerCase()}.`
        }

        return `${filteredExecutions.length} run${filteredExecutions.length === 1 ? '' : 's'} in view`
    }, [currentTabLabel, filteredByTab.length, filteredExecutions.length, searchQuery])
    const connectionStatusLabel = serverEventState === 'live'
        ? 'Live event stream connected'
        : serverEventState === 'reconnecting'
            ? 'Live stream reconnecting'
            : 'Connecting to live stream'
    const connectionStatusDetail = serverEventState === 'live'
        ? 'Runs, logs, and work-item updates should appear with minimal delay.'
        : serverEventState === 'reconnecting'
            ? 'Fleet is temporarily leaning on fallback polling while the stream reconnects.'
            : 'Fleet is warming up the event stream before switching to live updates.'

    const emptyExecutionState = useMemo(() => {
        if (searchQuery.trim()) {
            return {
                title: 'No runs match this search',
                detail: 'Try a different work item number, agent role, or execution id to find the run you want.',
            }
        }

        if (tab === 'active') {
            return {
                title: 'No active runs right now',
                detail: 'Start a new run or resume a paused one to see live execution progress here.',
            }
        }

        if (tab === 'paused') {
            return {
                title: 'No paused runs',
                detail: 'Paused executions will show up here with resume controls when Fleet needs your attention.',
            }
        }

        if (tab === 'completed') {
            return {
                title: 'No completed runs yet',
                detail: 'Finished runs will appear here once Fleet ships work all the way through.',
            }
        }

        if (tab === 'failed') {
            return {
                title: 'No failed runs',
                detail: 'If a run hits a problem, this view will collect the failures so they are easy to retry.',
            }
        }

        if (tab === 'cancelled') {
            return {
                title: 'No cancelled runs',
                detail: 'User-cancelled runs will appear here along with their preserved logs and actions.',
            }
        }

        return {
            title: 'No runs yet',
            detail: 'Start an execution to begin tracking Fleet activity, sub-flows, and logs in one place.',
        }
    }, [searchQuery, tab])

    const handleStartExecution = (workItemNumber: number, targetBranch: string) => {
        startExecution.mutate({ workItemNumber, targetBranch }, {
            onSuccess: (result) => {
                setDialogOpen(false)
                setTab('active')
                setSearchQuery('')
                dispatchToast(
                    <Toast><ToastTitle>Agent execution started (ID: {result.executionId})</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: (error) => {
                dispatchToast(
                    <Toast><ToastTitle>{getApiErrorMessage(error, 'Failed to start agent execution.')}</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleCancel = (executionId: string) => {
        cancelExecution.mutate(executionId, {
            onSuccess: () => {
                dispatchToast(
                    <Toast><ToastTitle>Agent execution stopped</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to stop execution</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handlePause = (executionId: string) => {
        pauseExecution.mutate(executionId, {
            onSuccess: () => {
                dispatchToast(
                    <Toast><ToastTitle>Agent execution paused</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to pause execution</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleResume = (executionId: string) => {
        resumeExecution.mutate(executionId, {
            onSuccess: () => {
                dispatchToast(
                    <Toast><ToastTitle>Execution resumed</ToastTitle></Toast>,
                    { intent: 'success' },
                )
                setTab('active')
            },
            onError: (error) => {
                dispatchToast(
                    <Toast><ToastTitle>{getApiErrorMessage(error, 'Failed to resume execution.')}</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleRetry = (executionId: string) => {
        retryExecution.mutate(executionId, {
            onSuccess: (result) => {
                setTab('active')
                dispatchToast(
                    <Toast><ToastTitle>Execution retried (new ID: {result.executionId})</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: (error) => {
                dispatchToast(
                    <Toast><ToastTitle>{getApiErrorMessage(error, 'Failed to retry execution.')}</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleViewDocs = (executionId: string) => {
        fetchExecutionDocumentation.mutate(executionId, {
            onSuccess: (docs) => {
                if (!hasExecutionDocumentation(docs)) {
                    dispatchToast(
                        <Toast><ToastTitle>No documentation output is available for this execution</ToastTitle></Toast>,
                        { intent: 'info' },
                    )
                    return
                }

                setSelectedDocumentation(docs)
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to generate execution documentation</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleClearLogs = () => {
        clearLogs.mutate(undefined, {
            onSuccess: (result) => {
                dispatchToast(
                    <Toast><ToastTitle>Cleared {result.deletedCount} log entr{result.deletedCount === 1 ? 'y' : 'ies'}</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to clear logs</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleClearRunLogs = (executionId: string) => {
        const execution = findExecutionInCollection(allExecutions, executionId)
        const description = execution ? `run for #${execution.workItemId}` : 'this run'
        if (!window.confirm(`Clear the logs for ${description}?`)) {
            return
        }

        clearExecutionLogs.mutate(executionId, {
            onSuccess: (result) => {
                dispatchToast(
                    <Toast><ToastTitle>Cleared {result.deletedCount} log entr{result.deletedCount === 1 ? 'y' : 'ies'} for this run</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: (error) => {
                dispatchToast(
                    <Toast><ToastTitle>{getApiErrorMessage(error, 'Failed to clear logs for this run.')}</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleDelete = (executionId: string) => {
        const execution = findExecutionInCollection(allExecutions, executionId)
        const description = execution
            ? `Delete the ${execution.status} run for #${execution.workItemId}? This also removes its logs.`
            : 'Delete this run and its logs?'
        if (!window.confirm(description)) {
            return
        }

        deleteExecution.mutate(executionId, {
            onSuccess: (result) => {
                if (selectedDocumentation?.executionId === executionId) {
                    setSelectedDocumentation(null)
                }

                dispatchToast(
                    <Toast><ToastTitle>Deleted run and {result.deletedLogCount} log entr{result.deletedLogCount === 1 ? 'y' : 'ies'}</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: (error) => {
                dispatchToast(
                    <Toast><ToastTitle>{getApiErrorMessage(error, 'Failed to delete run.')}</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    if (loadingExec || loadingLogs) {
        return (
            <div className={mergeClasses(styles.page, isDense && styles.pageCompact)}>
                <Spinner label="Loading agent data..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isDense && styles.pageCompact)}>
            <Toaster toasterId={toasterId} />
            <ExecutionDocsDialog
                docs={selectedDocumentation}
                open={!!selectedDocumentation}
                onOpenChange={(open) => {
                    if (!open) {
                        setSelectedDocumentation(null)
                    }
                }}
            />
            <PageHeader
                title="Agent Monitor"
                subtitle="Track agent executions and view real-time logs"
                actions={
                    <div className={mergeClasses(styles.headerActions, isMobile && styles.headerActionsMobile)}>
                        <Toolbar className={mergeClasses(styles.actionsToolbar, isMobile && styles.actionsToolbarMobile)}>
                            <ToolbarButton
                                icon={<FleetRocketLogo size={18} title="Start execution" variant="outline" />}
                                onClick={() => setDialogOpen(true)}
                                className={mergeClasses(isMobile && styles.toolbarButtonMobile)}
                            >
                                Start Execution
                            </ToolbarButton>
                            <Input
                                className={mergeClasses(styles.searchInput, isDense && styles.searchInputCompact, isMobile && styles.searchInputMobile)}
                                placeholder="Search executions..."
                                size="small"
                                appearance="underline"
                                value={searchQuery}
                                onChange={(_e, data) => setSearchQuery(data.value)}
                                contentBefore={<SearchRegular />}
                            />
                            <ToolbarButton
                                icon={<ArrowClockwiseRegular />}
                                onClick={() => { void refetchExec(); void refetchLogs() }}
                                className={mergeClasses(isMobile && styles.toolbarButtonMobile)}
                            >
                                Refresh
                            </ToolbarButton>
                        </Toolbar>
                    </div>
                }
            />
            <Card
                className={mergeClasses(
                    styles.statusStrip,
                    isMobile && styles.statusStripMobile,
                    serverEventState === 'live'
                        ? styles.statusStripLive
                        : serverEventState === 'reconnecting'
                            ? styles.statusStripReconnecting
                            : styles.statusStripConnecting,
                )}
            >
                <div className={styles.statusStripMeta}>
                    <Text weight="semibold">{connectionStatusLabel}</Text>
                    <Caption1>{connectionStatusDetail}</Caption1>
                </div>
                <div className={styles.statusStripPills}>
                    <InfoBadge appearance={serverEventState === 'live' ? 'filled' : 'tint'}>
                        {serverEventState}
                    </InfoBadge>
                    <InfoBadge appearance="tint">{allExecutions.length} total runs</InfoBadge>
                    <InfoBadge appearance="tint">{allLogs.length} log entries</InfoBadge>
                    <InfoBadge appearance="tint">{workItems?.length ?? 0} executable items</InfoBadge>
                </div>
            </Card>

            <div className={mergeClasses(styles.summaryRow, isDense && styles.summaryRowCompact, isMobile && styles.summaryRowMobile)}>
                <SummaryCard
                    icon={<PlayRegular />}
                    iconClassName={styles.summaryIconWarning}
                    value={running.length}
                    label="Running"
                    onClick={() => setTab('active')}
                    isActive={tab === 'active'}
                />
                <SummaryCard
                    icon={<PauseRegular />}
                    iconClassName={styles.summaryIconBrand}
                    value={paused.length}
                    label="Paused"
                    onClick={() => setTab('paused')}
                    isActive={tab === 'paused'}
                />
                <SummaryCard
                    icon={<CheckmarkCircleRegular />}
                    iconClassName={styles.summaryIconSuccess}
                    value={completed.length}
                    label="Completed"
                    onClick={() => setTab('completed')}
                    isActive={tab === 'completed'}
                />
                <SummaryCard
                    icon={<ErrorCircleRegular />}
                    iconClassName={styles.summaryIconDanger}
                    value={failed.length}
                    label="Failed"
                    onClick={() => setTab('failed')}
                    isActive={tab === 'failed'}
                />
                <SummaryCard
                    icon={<DismissCircleRegular />}
                    iconClassName={styles.summaryIconDanger}
                    value={cancelled.length}
                    label="Cancelled"
                    onClick={() => setTab('cancelled')}
                    isActive={tab === 'cancelled'}
                />
                <SummaryCard
                    icon={<BotRegular />}
                    iconClassName={styles.summaryIconBrand}
                    value={activeAgentCount}
                    label="Active Agents"
                    onClick={() => setTab('active')}
                    isActive={tab === 'active'}
                />
            </div>

            <TabList
                selectedValue={tab}
                onTabSelect={(_e, data) => setTab(data.value as string)}
                className={mergeClasses(styles.tabListSpacing, isDense && styles.tabListSpacingCompact)}
                size={isDense ? 'small' : 'medium'}
            >
                <Tab className={styles.monitorTab} value="active" icon={<PlayRegular />}>Active ({active.length})</Tab>
                <Tab className={styles.monitorTab} value="paused" icon={<PauseRegular />}>Paused ({paused.length})</Tab>
                <Tab className={styles.monitorTab} value="completed" icon={<CheckmarkCircleRegular />}>Completed ({completed.length})</Tab>
                <Tab className={styles.monitorTab} value="failed" icon={<ErrorCircleRegular />}>Failed ({failed.length})</Tab>
                <Tab className={styles.monitorTab} value="cancelled" icon={<DismissCircleRegular />}>Cancelled ({cancelled.length})</Tab>
                <Tab className={styles.monitorTab} value="all">All ({allExecutions.length})</Tab>
            </TabList>

            <div className={mergeClasses(styles.mainContent, isDense && styles.mainContentCompact)}>
                <div className={styles.executionPanel}>
                    <div className={mergeClasses(styles.executionPanelShell, isDense && styles.executionPanelShellCompact)}>
                        <div className={styles.executionPanelHeader}>
                            <div className={styles.executionPanelHeaderMeta}>
                                <Text weight="semibold">{currentTabLabel}</Text>
                                <Caption1 className={styles.executionPanelSubtitle}>{executionPanelSubtitle}</Caption1>
                            </div>
                            <InfoBadge appearance="tint" size="small">
                                {filteredExecutions.length} result{filteredExecutions.length === 1 ? '' : 's'}
                            </InfoBadge>
                        </div>
                        <div className={mergeClasses(styles.executionList, isDense && styles.executionListCompact)}>
                            {filteredExecutions.length === 0 ? (
                                <div className={styles.emptyExecutionState}>
                                    <FleetRocketLogo
                                        size={30}
                                        title="No runs"
                                        variant="outline"
                                        className={styles.emptyExecutionStateIcon}
                                    />
                                    <div className={styles.emptyExecutionStateBody}>
                                        <Text weight="semibold">{emptyExecutionState.title}</Text>
                                        <Caption1 className={styles.emptyExecutionStateDetail}>{emptyExecutionState.detail}</Caption1>
                                    </div>
                                </div>
                            ) : (
                                filteredExecutions.map((execution) => (
                                    <ExecutionCard
                                        key={execution.id}
                                        execution={execution}
                                        workItems={workItems ?? []}
                                        onPause={handlePause}
                                        onCancel={handleCancel}
                                        onResume={handleResume}
                                        onRetry={handleRetry}
                                        onDelete={handleDelete}
                                        onViewDocs={handleViewDocs}
                                    />
                                ))
                            )}
                        </div>
                    </div>
                </div>

                <LogPanel
                    logs={allLogs}
                    executions={allExecutions}
                    onRefresh={() => void refetchLogs()}
                    onClearAll={handleClearLogs}
                    onClearRun={handleClearRunLogs}
                    isClearingAll={clearLogs.isPending}
                    isClearingRun={clearExecutionLogs.isPending}
                />
            </div>

            <StartExecutionDialog
                open={dialogOpen}
                onOpenChange={setDialogOpen}
                workItems={workItems ?? []}
                isLoading={loadingWorkItems}
                isPending={startExecution.isPending}
                onStart={handleStartExecution}
            />
        </div>
    )
}
