import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Title3,
    Card,
    Button,
    Divider,
    Spinner,
    Text,
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
import { useCurrentProject, usePreferences } from '../../hooks'

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
    metricsGridCompact: {
        gridTemplateColumns: '1fr 1fr',
        gap: '0.5rem',
        marginBottom: '1rem',
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
    twoColumnsCompact: {
        gap: '0.75rem',
        marginBottom: '1rem',
        gridTemplateColumns: '1fr',
    },
    sectionCard: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    sectionCardCompact: {
        paddingTop: '0.625rem',
        paddingBottom: '0.625rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
        gap: '0.5rem',
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
    quickActionsCompact: {
        gridTemplateColumns: '1fr 1fr',
        gap: '0.5rem',
        marginBottom: '1rem',
    },
})

export function ProjectDashboardPage() {
    const styles = useStyles()
    const { slug } = useCurrentProject()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const navigate = useNavigate()
    const { data: dashboard, isLoading } = useProjectDashboardBySlug(slug)

    if (isLoading || !dashboard) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading dashboard..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
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
            <div className={mergeClasses(styles.quickActions, isCompact && styles.quickActionsCompact)}>
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
                    onClick={() => navigate(`/projects/${slug}/agents`)}
                />
            </div>

            {/* Metrics */}
            <div className={mergeClasses(styles.metricsGrid, isCompact && styles.metricsGridCompact)}>
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
            <div className={mergeClasses(styles.twoColumns, isCompact && styles.twoColumnsCompact)}>
                {/* Recent Activity */}
                <Card className={mergeClasses(styles.sectionCard, isCompact && styles.sectionCardCompact)}>
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
                <Card className={mergeClasses(styles.sectionCard, isCompact && styles.sectionCardCompact)}>
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
                        {dashboard.agents.length > 0 ? (
                            dashboard.agents.map((agent, i) => (
                                <AgentStatusRow
                                    key={i}
                                    name={agent.name}
                                    status={agent.status}
                                    task={agent.task}
                                    progress={agent.progress}
                                />
                            ))
                        ) : (
                            <Text size={200} italic>
                                No agent activity yet. Start an execution from the Agent Monitor.
                            </Text>
                        )}
                    </div>
                </Card>
            </div>
        </div>
    )
}
