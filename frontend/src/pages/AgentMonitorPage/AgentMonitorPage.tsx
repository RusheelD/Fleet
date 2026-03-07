import { useState, useMemo } from 'react'
import {
    makeStyles,
    mergeClasses,
    tokens,
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
    CheckmarkCircleRegular,
    ErrorCircleRegular,
    DismissCircleRegular,
    SearchRegular,
    ArrowClockwiseRegular,
    RocketRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { SummaryCard, ExecutionCard, LogPanel, StartExecutionDialog } from './'
import { useExecutions, useLogs, useWorkItems, useStartExecution, useCancelExecution, usePauseExecution, useRetryExecution, useExecutionDocumentation, useClearLogs } from '../../proxies'
import { useCurrentProject, usePreferences } from '../../hooks'
import { openExecutionDocumentation } from './executionDocs'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    pageCompact: {
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '1rem',
        paddingRight: '1rem',
    },
    headerActions: {
        display: 'flex',
        gap: '0.5rem',
        width: '100%',
    },
    summaryRow: {
        display: 'flex',
        gap: '1rem',
        marginBottom: '1.5rem',
        flexWrap: 'wrap',
    },
    summaryRowCompact: {
        gap: '0.5rem',
        marginBottom: '0.75rem',
    },
    summaryIconWarning: {
        color: tokens.colorPaletteMarigoldForeground1,
    },
    summaryIconSuccess: {
        color: tokens.colorPaletteGreenForeground1,
    },
    summaryIconDanger: {
        color: tokens.colorPaletteRedForeground1,
    },
    summaryIconBrand: {
        color: tokens.colorBrandForeground1,
    },
    tabListSpacing: {
        marginBottom: '1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingBottom: '0.5rem',
    },
    tabListSpacingCompact: {
        marginBottom: '0.5rem',
        paddingBottom: '0.25rem',
    },
    mainContent: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '1.5rem',
        flex: 1,
        overflow: 'hidden',
        '@media (max-width: 1000px)': {
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
})

export function AgentMonitorPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const { data: executions, isLoading: loadingExec, refetch: refetchExec } = useExecutions(projectId)
    const { data: logs, isLoading: loadingLogs, refetch: refetchLogs } = useLogs(projectId)
    const { data: workItems, isLoading: loadingWorkItems } = useWorkItems(projectId)
    const startExecution = useStartExecution(projectId)
    const cancelExecution = useCancelExecution(projectId)
    const pauseExecution = usePauseExecution(projectId)
    const retryExecution = useRetryExecution(projectId)
    const fetchExecutionDocumentation = useExecutionDocumentation(projectId)
    const clearLogs = useClearLogs(projectId)
    const [tab, setTab] = useState<string>('active')
    const [dialogOpen, setDialogOpen] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const toasterId = useId('agent-monitor-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const allExecutions = executions ?? []
    const allLogs = logs ?? []

    const running = allExecutions.filter((e) => e.status === 'running')
    const completed = allExecutions.filter((e) => e.status === 'completed')
    const failed = allExecutions.filter((e) => e.status === 'failed')
    const cancelled = allExecutions.filter((e) => e.status === 'cancelled')
    const activeAgentCount = running.reduce(
        (acc, execution) => acc + execution.agents.filter((agent) => agent.status === 'running').length,
        0,
    )

    const filteredByTab =
        tab === 'active' ? running :
            tab === 'completed' ? completed :
                tab === 'failed' ? failed :
                    tab === 'cancelled' ? cancelled :
                    allExecutions

    const filteredExecutions = useMemo(() => {
        if (!searchQuery) return filteredByTab
        const q = searchQuery.toLowerCase()
        return filteredByTab.filter((e) =>
            e.workItemTitle.toLowerCase().includes(q) ||
            e.id.toLowerCase().includes(q) ||
            e.agents.some((a) => a.role.toLowerCase().includes(q)),
        )
    }, [filteredByTab, searchQuery])

    const handleStartExecution = (workItemNumber: number) => {
        startExecution.mutate(workItemNumber, {
            onSuccess: (result) => {
                setDialogOpen(false)
                dispatchToast(
                    <Toast><ToastTitle>Agent execution started (ID: {result.executionId})</ToastTitle></Toast>,
                    { intent: 'success' },
                )
                void refetchExec()
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to start agent execution</ToastTitle></Toast>,
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
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to retry execution</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    const handleViewDocs = (executionId: string) => {
        fetchExecutionDocumentation.mutate(executionId, {
            onSuccess: (docs) => {
                const didOpen = openExecutionDocumentation(docs)
                if (!didOpen) {
                    dispatchToast(
                        <Toast><ToastTitle>No documentation output is available for this execution</ToastTitle></Toast>,
                        { intent: 'info' },
                    )
                }
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

    if (loadingExec || loadingLogs) {
        return (
            <div className={mergeClasses(styles.page, isCompact && styles.pageCompact)}>
                <Spinner label="Loading agent data..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isCompact && styles.pageCompact)}>
            <Toaster toasterId={toasterId} />
            <PageHeader
                title="Agent Monitor"
                subtitle="Track agent executions and view real-time logs"
                actions={
                    <div className={styles.headerActions}>
                        <Toolbar>
                            <ToolbarButton
                                icon={<RocketRegular />}
                                onClick={() => setDialogOpen(true)}
                            >
                                Start Execution
                            </ToolbarButton>
                            <Input
                                className={mergeClasses(styles.searchInput, isCompact && styles.searchInputCompact)}
                                placeholder="Search executions..."
                                size="small"
                                appearance="underline"
                                value={searchQuery}
                                onChange={(_e, data) => setSearchQuery(data.value)}
                                contentBefore={<SearchRegular />}
                            />
                            <ToolbarButton icon={<ArrowClockwiseRegular />} onClick={() => { void refetchExec(); void refetchLogs() }}>Refresh</ToolbarButton>
                        </Toolbar>
                    </div>
                }
            />

            <div className={mergeClasses(styles.summaryRow, isCompact && styles.summaryRowCompact)}>
                <SummaryCard
                    icon={<PlayRegular />}
                    iconClassName={styles.summaryIconWarning}
                    value={running.length}
                    label="Running"
                    onClick={() => setTab('active')}
                    isActive={tab === 'active'}
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
                className={mergeClasses(styles.tabListSpacing, isCompact && styles.tabListSpacingCompact)}
                size={isCompact ? 'small' : 'medium'}
            >
                <Tab value="active" icon={<PlayRegular />}>Active ({running.length})</Tab>
                <Tab value="completed" icon={<CheckmarkCircleRegular />}>Completed ({completed.length})</Tab>
                <Tab value="failed" icon={<ErrorCircleRegular />}>Failed ({failed.length})</Tab>
                <Tab value="cancelled" icon={<DismissCircleRegular />}>Cancelled ({cancelled.length})</Tab>
                <Tab value="all">All ({allExecutions.length})</Tab>
            </TabList>

            <div className={mergeClasses(styles.mainContent, isCompact && styles.mainContentCompact)}>
                <div className={styles.executionPanel}>
                    <div className={mergeClasses(styles.executionList, isCompact && styles.executionListCompact)}>
                        {filteredExecutions.map((execution) => (
                            <ExecutionCard
                                key={execution.id}
                                execution={execution}
                                onPause={handlePause}
                                onCancel={handleCancel}
                                onRetry={handleRetry}
                                onViewDocs={handleViewDocs}
                            />
                        ))}
                    </div>
                </div>

                <LogPanel
                    logs={allLogs}
                    executions={allExecutions}
                    onRefresh={() => void refetchLogs()}
                    onClear={handleClearLogs}
                    isClearing={clearLogs.isPending}
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
