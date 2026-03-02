import { useParams, useNavigate } from 'react-router-dom'
import {
    makeStyles,
    tokens,
    Title3,
    Card,
    Button,
    Divider,
} from '@fluentui/react-components'
import {
    ChatRegular,
    BoardRegular,
    BotRegular,
    CheckmarkCircleRegular,
    PersonRegular,
    RocketRegular,
    CodeRegular,
    BranchRegular,
    LinkRegular,
    OpenRegular,
    ArrowTrendingRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { MetricCard } from './MetricCard'
import { ActivityItem } from './ActivityItem'
import { AgentStatusRow } from './AgentStatusRow'
import { QuickActionCard } from './QuickActionCard'

const MOCK_ACTIVITIES = [
    { icon: <BranchRegular />, text: 'Agent opened PR #42: "Add user authentication"', time: '15 min ago' },
    { icon: <CheckmarkCircleRegular />, text: 'Work item "Setup CI/CD pipeline" resolved', time: '1 hour ago' },
    { icon: <BotRegular />, text: '3 agents started working on "Implement Search API"', time: '2 hours ago' },
    { icon: <CodeRegular />, text: 'Agent pushed 12 commits to feature/auth', time: '3 hours ago' },
    { icon: <PersonRegular />, text: 'You created work item "Add dark mode support"', time: '5 hours ago' },
]

const MOCK_AGENTS = [
    { name: 'Manager Agent', status: 'running', task: 'Coordinating auth implementation', progress: 0.6 },
    { name: 'Backend Agent', status: 'running', task: 'Implementing OAuth endpoints', progress: 0.4 },
    { name: 'Frontend Agent', status: 'running', task: 'Building login components', progress: 0.35 },
    { name: 'Testing Agent', status: 'idle', task: 'Waiting for code completion', progress: 0 },
]

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '1400px',
        margin: '0 auto',
        width: '100%',
    },
    repoLink: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorBrandForegroundLink,
        fontSize: '13px',
        cursor: 'pointer',
        ':hover': {
            textDecoration: 'underline',
        },
    },
    openLinkIcon: {
        fontSize: '12px',
    },
    headerActions: {
        display: 'flex',
        gap: '0.5rem',
    },
    metricsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
        gap: '1rem',
        marginBottom: '1.5rem',
    },
    twoColumns: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '1.5rem',
        marginBottom: '1.5rem',
        '@media (max-width: 900px)': {
            gridTemplateColumns: '1fr',
        },
    },
    sectionCard: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    sectionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    activityList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    agentList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    quickActions: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))',
        gap: '0.75rem',
        marginBottom: '1.5rem',
    },
})

export function ProjectDashboardPage() {
    const styles = useStyles()
    const { projectId } = useParams()
    const navigate = useNavigate()

    return (
        <div className={styles.page}>
            <PageHeader
                title="Fleet Platform"
                subtitle={undefined}
                actions={
                    <div className={styles.headerActions}>
                        <Button appearance="primary" icon={<ChatRegular />} onClick={() => navigate(`/projects/${projectId}/work-items`)}>
                            Open Chat
                        </Button>
                        <Button icon={<BoardRegular />} onClick={() => navigate(`/projects/${projectId}/work-items`)}>
                            Work Items
                        </Button>
                    </div>
                }
            />

            <span className={styles.repoLink}>
                <LinkRegular />
                RusheelD/Fleet
                <OpenRegular className={styles.openLinkIcon} />
            </span>

            {/* Quick Actions */}
            <div className={styles.quickActions}>
                <QuickActionCard
                    icon={<ChatRegular />}
                    title="AI Chat"
                    description="Define specs & generate items"
                    onClick={() => navigate(`/projects/${projectId}/work-items`)}
                />
                <QuickActionCard
                    icon={<BoardRegular />}
                    title="Work Items"
                    description="View board & backlog"
                    onClick={() => navigate(`/projects/${projectId}/work-items`)}
                />
                <QuickActionCard
                    icon={<BotRegular />}
                    title="Agent Monitor"
                    description="Track agent activity"
                    onClick={() => navigate(`/projects/${projectId}/agents`)}
                />
                <QuickActionCard
                    icon={<RocketRegular />}
                    title="Run Agents"
                    description="Start new agent execution"
                />
            </div>

            {/* Metrics */}
            <div className={styles.metricsGrid}>
                <MetricCard icon={<BoardRegular />} label="Total Work Items" value={24} subtext="8 active · 12 resolved · 4 closed" />
                <MetricCard icon={<BotRegular />} label="Active Agents" value={3} subtext="of 5 allocated" />
                <MetricCard icon={<BranchRegular />} label="Pull Requests" value={7} subtext="3 open · 4 merged" />
                <MetricCard icon={<ArrowTrendingRegular />} label="Completion" value="67%" subtext="" progress={0.67} />
            </div>

            {/* Two column layout */}
            <div className={styles.twoColumns}>
                {/* Recent Activity */}
                <Card className={styles.sectionCard}>
                    <div className={styles.sectionHeader}>
                        <Title3>Recent Activity</Title3>
                        <Button appearance="transparent" size="small">View all</Button>
                    </div>
                    <Divider />
                    <div className={styles.activityList}>
                        {MOCK_ACTIVITIES.map((activity, i) => (
                            <ActivityItem key={i} icon={activity.icon} text={activity.text} time={activity.time} />
                        ))}
                    </div>
                </Card>

                {/* Agent Status */}
                <Card className={styles.sectionCard}>
                    <div className={styles.sectionHeader}>
                        <Title3>Agent Status</Title3>
                        <Button
                            appearance="transparent"
                            size="small"
                            onClick={() => navigate(`/projects/${projectId}/agents`)}
                        >
                            Monitor all
                        </Button>
                    </div>
                    <Divider />
                    <div className={styles.agentList}>
                        {MOCK_AGENTS.map((agent, i) => (
                            <AgentStatusRow
                                key={i}
                                name={agent.name}
                                status={agent.status}
                                task={agent.task}
                                progress={agent.progress}
                            />
                        ))}
                    </div>
                </Card>
            </div>
        </div>
    )
}
