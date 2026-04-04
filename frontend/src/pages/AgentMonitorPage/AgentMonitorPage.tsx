import { useState, useMemo } from 'react'
import {
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
import { SummaryCard, ExecutionCard, ExecutionDocsDialog, LogPanel, StartExecutionDialog } from './'
import { getApiErrorMessage, type ExecutionDocumentation, useExecutions, useLogs, useWorkItems, useStartExecution, useCancelExecution, usePauseExecution, useResumeExecution, useRetryExecution, useExecutionDocumentation, useClearLogs, useClearExecutionLogs, useDeleteExecution } from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile } from '../../hooks'
import { hasExecutionDocumentation } from './executionDocs'
import { appTokens, APP_NARROW_LAYOUT_MEDIA_QUERY } from '../../styles/appTokens'

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
})

export function AgentMonitorPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const { data: executions, isLoading: loadingExec, refetch: refetchExec } = useExecutions(projectId)
    const { data: logs, isLoading: loadingLogs, refetch: refetchLogs } = useLogs(projectId)
    const { data: workItems, isLoading: loadingWorkItems } = useWorkItems(projectId)
    const startExecution = useStartExecution(projectId)
    const cancelExecution = useCancelExecution(projectId)
    const pauseExecution = usePauseExecution(projectId)
    const resumeExecution = useResumeExecution(projectId)
    const retryExecution = useRetryExecution(projectId)
    const fetchExecutionDocumentation = useExecutionDocumentation(projectId)
    const clearLogs = useClearLogs(projectId)
    const clearExecutionLogs = useClearExecutionLogs(projectId)
    const deleteExecution = useDeleteExecution(projectId)
    const [tab, setTab] = useState<string>('active')
    const [dialogOpen, setDialogOpen] = useState(false)
    const [selectedDocumentation, setSelectedDocumentation] = useState<ExecutionDocumentation | null>(null)
    const [searchQuery, setSearchQuery] = useState('')
    const toasterId = useId('agent-monitor-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const allExecutions = executions ?? []
    const allLogs = logs ?? []

    const running = allExecutions.filter((e) => e.status === 'running')
    const paused = allExecutions.filter((e) => e.status === 'paused')
    const completed = allExecutions.filter((e) => e.status === 'completed')
    const failed = allExecutions.filter((e) => e.status === 'failed')
    const cancelled = allExecutions.filter((e) => e.status === 'cancelled')
    const activeAgentCount = running.reduce(
        (acc, execution) => acc + execution.agents.filter((agent) => agent.status === 'running').length,
        0,
    )

    const filteredByTab =
        tab === 'active' ? running :
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

    const handleStartExecution = (workItemNumber: number, targetBranch: string) => {
        startExecution.mutate({ workItemNumber, targetBranch }, {
            onSuccess: (result) => {
                setDialogOpen(false)
                dispatchToast(
                    <Toast><ToastTitle>Agent execution started (ID: {result.executionId})</ToastTitle></Toast>,
                    { intent: 'success' },
                )
                void refetchExec()
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
                void refetchExec()
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
                void refetchExec()
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
                void refetchExec()
                void refetchLogs()
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
                dispatchToast(
                    <Toast><ToastTitle>Execution retried (new ID: {result.executionId})</ToastTitle></Toast>,
                    { intent: 'success' },
                )
                void refetchExec()
                void refetchLogs()
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
                void refetchLogs()
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
        const execution = allExecutions.find((item) => item.id === executionId)
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
                void refetchLogs()
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
        const execution = allExecutions.find((item) => item.id === executionId)
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
                void refetchExec()
                void refetchLogs()
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
                                icon={<FleetRocketLogo size={18} title="Start execution" />}
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
                <Tab className={styles.monitorTab} value="active" icon={<PlayRegular />}>Active ({running.length})</Tab>
                <Tab className={styles.monitorTab} value="paused" icon={<PauseRegular />}>Paused ({paused.length})</Tab>
                <Tab className={styles.monitorTab} value="completed" icon={<CheckmarkCircleRegular />}>Completed ({completed.length})</Tab>
                <Tab className={styles.monitorTab} value="failed" icon={<ErrorCircleRegular />}>Failed ({failed.length})</Tab>
                <Tab className={styles.monitorTab} value="cancelled" icon={<DismissCircleRegular />}>Cancelled ({cancelled.length})</Tab>
                <Tab className={styles.monitorTab} value="all">All ({allExecutions.length})</Tab>
            </TabList>

            <div className={mergeClasses(styles.mainContent, isDense && styles.mainContentCompact)}>
                <div className={styles.executionPanel}>
                    <div className={mergeClasses(styles.executionList, isDense && styles.executionListCompact)}>
                        {filteredExecutions.map((execution) => (
                            <ExecutionCard
                                key={execution.id}
                                execution={execution}
                                onPause={handlePause}
                                onCancel={handleCancel}
                                onResume={handleResume}
                                onRetry={handleRetry}
                                onDelete={handleDelete}
                                onViewDocs={handleViewDocs}
                            />
                        ))}
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
