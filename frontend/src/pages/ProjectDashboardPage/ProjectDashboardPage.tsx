import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    tokens,
    Title3,
    Card,
    Button,
    Divider,
    Spinner,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
} from '@fluentui/react-components'
import {
    ChatRegular,
    BoardRegular,
    BotRegular,
    RocketRegular,
    LinkRegular,
    OpenRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { MetricCard, ActivityItem, AgentStatusRow, QuickActionCard } from './'
import { useProjectDashboardBySlug, resolveIcon } from '../../proxies'
import { useCurrentProject } from '../../hooks'

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
    const { slug } = useCurrentProject()
    const navigate = useNavigate()
    const { data: dashboard, isLoading } = useProjectDashboardBySlug(slug)
    const toasterId = useId('dashboard-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const notify = (msg: string) => {
        dispatchToast(
            <Toast><ToastTitle>{msg}</ToastTitle></Toast>,
            { intent: 'info' },
        )
    }

    if (isLoading || !dashboard) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading dashboard..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
            <Toaster toasterId={toasterId} />
            <PageHeader
                title={dashboard.title}
                subtitle={undefined}
                actions={
                    <div className={styles.headerActions}>
                        <Button appearance="primary" icon={<ChatRegular />} onClick={() => navigate(`/projects/${slug}/work-items`, { state: { openChat: true } })}>
                            Open Chat
                        </Button>
                        <Button icon={<BoardRegular />} onClick={() => navigate(`/projects/${slug}/work-items`)}>
                            Work Items
                        </Button>
                    </div>
                }
            />

            <span className={styles.repoLink} onClick={() => window.open(`https://github.com/${dashboard.repo}`, '_blank')}>
                <LinkRegular />
                {dashboard.repo}
                <OpenRegular className={styles.openLinkIcon} />
            </span>

            {/* Quick Actions */}
            <div className={styles.quickActions}>
                <QuickActionCard
                    icon={<ChatRegular />}
                    title="AI Chat"
                    description="Define specs & generate items"
                    onClick={() => navigate(`/projects/${slug}/work-items`, { state: { openChat: true } })}
                />
                <QuickActionCard
                    icon={<BoardRegular />}
                    title="Work Items"
                    description="View board & backlog"
                    onClick={() => navigate(`/projects/${slug}/work-items`)}
                />
                <QuickActionCard
                    icon={<BotRegular />}
                    title="Agent Monitor"
                    description="Track agent activity"
                    onClick={() => navigate(`/projects/${slug}/agents`)}
                />
                <QuickActionCard
                    icon={<RocketRegular />}
                    title="Run Agents"
                    description="Start new agent execution"
                    onClick={() => notify('Agent execution is not available in this version')}
                />
            </div>

            {/* Metrics */}
            <div className={styles.metricsGrid}>
                {dashboard.metrics.map((metric) => (
                    <MetricCard
                        key={metric.label}
                        icon={resolveIcon(metric.icon)}
                        label={metric.label}
                        value={metric.value}
                        subtext={metric.subtext}
                        progress={metric.progress ?? undefined}
                    />
                ))}
            </div>

            {/* Two column layout */}
            <div className={styles.twoColumns}>
                {/* Recent Activity */}
                <Card className={styles.sectionCard}>
                    <div className={styles.sectionHeader}>
                        <Title3>Recent Activity</Title3>
                        <Button appearance="transparent" size="small" onClick={() => navigate(`/projects/${slug}/agents`)}>View all</Button>
                    </div>
                    <Divider />
                    <div className={styles.activityList}>
                        {dashboard.activities.map((activity, i) => (
                            <ActivityItem key={i} icon={resolveIcon(activity.icon)} text={activity.text} time={activity.time} />
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
                            onClick={() => navigate(`/projects/${slug}/agents`)}
                        >
                            Monitor all
                        </Button>
                    </div>
                    <Divider />
                    <div className={styles.agentList}>
                        {dashboard.agents.map((agent, i) => (
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
