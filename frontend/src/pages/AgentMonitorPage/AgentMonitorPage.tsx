import { useState } from 'react'
import {
    makeStyles,
    tokens,
    Tab,
    TabList,
    Toolbar,
    ToolbarButton,
    Spinner,
} from '@fluentui/react-components'
import {
    BotRegular,
    PlayRegular,
    CheckmarkCircleRegular,
    ErrorCircleRegular,
    FilterRegular,
    ArrowClockwiseRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { SummaryCard, ExecutionCard, LogPanel } from './'
import { useExecutions, useLogs } from '../../proxies'
import { useCurrentProject } from '../../hooks'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    headerActions: {
        display: 'flex',
        gap: '0.5rem',
    },
    summaryRow: {
        display: 'flex',
        gap: '1rem',
        marginBottom: '1.5rem',
        flexWrap: 'wrap',
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
    },
})

export function AgentMonitorPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { data: executions, isLoading: loadingExec, refetch: refetchExec } = useExecutions(projectId)
    const { data: logs, isLoading: loadingLogs, refetch: refetchLogs } = useLogs(projectId)
    const [tab, setTab] = useState<string>('active')

    const allExecutions = executions ?? []
    const allLogs = logs ?? []

    const running = allExecutions.filter((e) => e.status === 'running')
    const completed = allExecutions.filter((e) => e.status === 'completed')
    const failed = allExecutions.filter((e) => e.status === 'failed')

    const filteredExecutions =
        tab === 'active' ? running :
            tab === 'completed' ? completed :
                tab === 'failed' ? failed :
                    allExecutions

    if (loadingExec || loadingLogs) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading agent data..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
            <PageHeader
                title="Agent Monitor"
                subtitle="Track agent executions and view real-time logs"
                actions={
                    <div className={styles.headerActions}>
                        <Toolbar>
                            <ToolbarButton icon={<FilterRegular />}>Filter</ToolbarButton>
                            <ToolbarButton icon={<ArrowClockwiseRegular />} onClick={() => { void refetchExec(); void refetchLogs() }}>Refresh</ToolbarButton>
                        </Toolbar>
                    </div>
                }
            />

            <div className={styles.summaryRow}>
                <SummaryCard
                    icon={<PlayRegular />}
                    iconClassName={styles.summaryIconWarning}
                    value={running.length}
                    label="Running"
                />
                <SummaryCard
                    icon={<CheckmarkCircleRegular />}
                    iconClassName={styles.summaryIconSuccess}
                    value={completed.length}
                    label="Completed"
                />
                <SummaryCard
                    icon={<ErrorCircleRegular />}
                    iconClassName={styles.summaryIconDanger}
                    value={failed.length}
                    label="Failed"
                />
                <SummaryCard
                    icon={<BotRegular />}
                    iconClassName={styles.summaryIconBrand}
                    value={allExecutions.reduce((acc, e) => acc + e.agents.filter((a) => a.status === 'running').length, 0)}
                    label="Active Agents"
                />
            </div>

            <TabList selectedValue={tab} onTabSelect={(_e, data) => setTab(data.value as string)} className={styles.tabListSpacing}>
                <Tab value="active" icon={<PlayRegular />}>Active ({running.length})</Tab>
                <Tab value="completed" icon={<CheckmarkCircleRegular />}>Completed ({completed.length})</Tab>
                <Tab value="failed" icon={<ErrorCircleRegular />}>Failed ({failed.length})</Tab>
                <Tab value="all">All ({allExecutions.length})</Tab>
            </TabList>

            <div className={styles.mainContent}>
                <div className={styles.executionPanel}>
                    <div className={styles.executionList}>
                        {filteredExecutions.map((execution) => (
                            <ExecutionCard key={execution.id} execution={execution} />
                        ))}
                    </div>
                </div>

                <LogPanel logs={allLogs} onRefresh={() => void refetchLogs()} />
            </div>
        </div>
    )
}
