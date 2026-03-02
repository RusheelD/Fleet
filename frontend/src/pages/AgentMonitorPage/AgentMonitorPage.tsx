import { useState } from 'react'
import {
    makeStyles,
    tokens,
    Tab,
    TabList,
    Toolbar,
    ToolbarButton,
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
import { SummaryCard } from './SummaryCard'
import { ExecutionCard } from './ExecutionCard'
import { LogPanel } from './LogPanel'
import type { AgentExecution, LogEntry } from '../../models'

const MOCK_EXECUTIONS: AgentExecution[] = [
    {
        id: 'e1',
        workItemId: 101,
        workItemTitle: 'Set up authentication with OAuth',
        status: 'running',
        progress: 0.55,
        startedAt: '2:15 PM',
        duration: '23 min',
        agents: [
            { role: 'Manager', status: 'running', currentTask: 'Coordinating phase 3', progress: 0.6 },
            { role: 'Backend', status: 'running', currentTask: 'Implementing OAuth endpoints', progress: 0.45 },
            { role: 'Frontend', status: 'running', currentTask: 'Building login UI', progress: 0.35 },
            { role: 'Testing', status: 'idle', currentTask: 'Waiting for implementation', progress: 0 },
        ],
    },
    {
        id: 'e2',
        workItemId: 104,
        workItemTitle: 'Implement work item board view',
        status: 'running',
        progress: 0.7,
        startedAt: '1:40 PM',
        duration: '58 min',
        agents: [
            { role: 'Manager', status: 'running', currentTask: 'Monitoring progress', progress: 0.75 },
            { role: 'Frontend', status: 'running', currentTask: 'Adding drag-and-drop', progress: 0.65 },
            { role: 'Styling', status: 'completed', currentTask: 'Theme applied', progress: 1.0 },
        ],
    },
    {
        id: 'e3',
        workItemId: 105,
        workItemTitle: 'Set up CI/CD pipeline',
        status: 'completed',
        progress: 1.0,
        startedAt: '11:00 AM',
        duration: '15 min',
        agents: [
            { role: 'Manager', status: 'completed', currentTask: 'PR opened', progress: 1.0 },
            { role: 'Backend', status: 'completed', currentTask: 'Pipeline configured', progress: 1.0 },
            { role: 'Documentation', status: 'completed', currentTask: 'README updated', progress: 1.0 },
        ],
    },
    {
        id: 'e4',
        workItemId: 108,
        workItemTitle: 'Agent execution logs',
        status: 'failed',
        progress: 0.3,
        startedAt: '10:20 AM',
        duration: '12 min',
        agents: [
            { role: 'Manager', status: 'failed', currentTask: 'Error: build failure', progress: 0.3 },
            { role: 'Backend', status: 'failed', currentTask: 'Compilation error in LogService', progress: 0.4 },
        ],
    },
]

const MOCK_LOGS: LogEntry[] = [
    { time: '2:38:12 PM', agent: 'Manager', level: 'info', message: 'Phase 3 parallel execution started - Backend, Frontend, Testing agents deployed' },
    { time: '2:38:10 PM', agent: 'Backend', level: 'info', message: 'Creating OAuth callback endpoint at /api/auth/callback' },
    { time: '2:37:55 PM', agent: 'Frontend', level: 'info', message: 'Generating LoginPage.tsx component with GitHub OAuth button' },
    { time: '2:37:40 PM', agent: 'Backend', level: 'success', message: 'OAuth middleware configured successfully' },
    { time: '2:37:22 PM', agent: 'Manager', level: 'info', message: 'Contracts phase completed - data models shared with all agents' },
    { time: '2:36:15 PM', agent: 'Contracts', level: 'success', message: 'Generated AuthUser, OAuthToken, and Session interfaces' },
    { time: '2:35:48 PM', agent: 'Planner', level: 'info', message: 'Work item decomposed into 8 sub-tasks' },
    { time: '2:35:30 PM', agent: 'Manager', level: 'info', message: 'Analyzing work item #101: Set up authentication with OAuth' },
    { time: '2:35:05 PM', agent: 'Frontend', level: 'warn', message: 'No existing auth components found - creating from scratch' },
    { time: '2:34:50 PM', agent: 'Backend', level: 'info', message: 'Reading existing project structure and dependencies' },
]

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
    const [tab, setTab] = useState<string>('active')

    const running = MOCK_EXECUTIONS.filter((e) => e.status === 'running')
    const completed = MOCK_EXECUTIONS.filter((e) => e.status === 'completed')
    const failed = MOCK_EXECUTIONS.filter((e) => e.status === 'failed')

    const filteredExecutions =
        tab === 'active' ? running :
            tab === 'completed' ? completed :
                tab === 'failed' ? failed :
                    MOCK_EXECUTIONS

    return (
        <div className={styles.page}>
            <PageHeader
                title="Agent Monitor"
                subtitle="Track agent executions and view real-time logs"
                actions={
                    <div className={styles.headerActions}>
                        <Toolbar>
                            <ToolbarButton icon={<FilterRegular />}>Filter</ToolbarButton>
                            <ToolbarButton icon={<ArrowClockwiseRegular />}>Refresh</ToolbarButton>
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
                    value={MOCK_EXECUTIONS.reduce((acc, e) => acc + e.agents.filter((a) => a.status === 'running').length, 0)}
                    label="Active Agents"
                />
            </div>

            <TabList selectedValue={tab} onTabSelect={(_e, data) => setTab(data.value as string)} className={styles.tabListSpacing}>
                <Tab value="active" icon={<PlayRegular />}>Active ({running.length})</Tab>
                <Tab value="completed" icon={<CheckmarkCircleRegular />}>Completed ({completed.length})</Tab>
                <Tab value="failed" icon={<ErrorCircleRegular />}>Failed ({failed.length})</Tab>
                <Tab value="all">All ({MOCK_EXECUTIONS.length})</Tab>
            </TabList>

            <div className={styles.mainContent}>
                <div className={styles.executionPanel}>
                    <div className={styles.executionList}>
                        {filteredExecutions.map((execution) => (
                            <ExecutionCard key={execution.id} execution={execution} />
                        ))}
                    </div>
                </div>

                <LogPanel logs={MOCK_LOGS} />
            </div>
        </div>
    )
}
