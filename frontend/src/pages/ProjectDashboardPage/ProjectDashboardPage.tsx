import { useCallback, useState } from 'react'
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
    Dialog,
    DialogSurface,
    DialogBody,
    DialogTitle,
    DialogContent,
    DialogActions,
    DialogTrigger,
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
import { useProjectDashboardBySlug, useDeleteProject, useUpdateProject, resolveIcon } from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile } from '../../hooks'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '1400px',
        margin: '0 auto',
        width: '100%',
    },
    pageMobile: {
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
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
        flexWrap: 'wrap',
    },
    headerActionsMobile: {
        width: '100%',
    },
    headerActionButtonMobile: {
        flex: '1 1 140px',
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
        gap: '0.5rem',
        flexWrap: 'wrap',
    },
    sectionHeaderMobile: {
        alignItems: 'flex-start',
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
    quickActionsMobile: {
        gridTemplateColumns: '1fr',
    },
    metricsGridMobile: {
        gridTemplateColumns: '1fr 1fr',
    },
    repoLinkMobile: {
        display: 'inline-flex',
        marginBottom: '0.75rem',
    },
})

export function ProjectDashboardPage() {
    const styles = useStyles()
    const { slug } = useCurrentProject()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const navigate = useNavigate()
    const { data: dashboard, isLoading } = useProjectDashboardBySlug(slug)
    const deleteProject = useDeleteProject()
    const updateProject = useUpdateProject()
    const [unlinkRepoOpen, setUnlinkRepoOpen] = useState(false)
    const [deleteProjectOpen, setDeleteProjectOpen] = useState(false)

    const handleUnlinkRepo = useCallback(() => {
        if (!dashboard) {
            return
        }
        updateProject.mutate(
            { id: dashboard.id, data: { repo: '' } },
            { onSuccess: () => setUnlinkRepoOpen(false) },
        )
    }, [dashboard, updateProject])

    const handleDeleteProject = useCallback(() => {
        if (!dashboard) {
            return
        }
        deleteProject.mutate(
            dashboard.id,
            {
                onSuccess: () => {
                    setDeleteProjectOpen(false)
                    navigate('/projects', { replace: true })
                },
            },
        )
    }, [dashboard, deleteProject, navigate])

    if (isLoading || !dashboard) {
        return (
            <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
                <Spinner label="Loading dashboard..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
            <PageHeader
                title={dashboard.title}
                subtitle={undefined}
                actions={
                    <div className={mergeClasses(styles.headerActions, isMobile && styles.headerActionsMobile)}>
                        <Button
                            appearance="primary"
                            icon={<ChatRegular />}
                            onClick={() => navigate(`/projects/${slug}/work-items`, { state: { openChat: true } })}
                            className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                        >
                            Open Chat
                        </Button>
                        {dashboard.repo ? (
                            <Button
                                appearance="secondary"
                                onClick={() => setUnlinkRepoOpen(true)}
                                disabled={updateProject.isPending}
                                className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                            >
                                Unlink Repo
                            </Button>
                        ) : null}
                        <Button
                            appearance="secondary"
                            onClick={() => setDeleteProjectOpen(true)}
                            disabled={deleteProject.isPending}
                            className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                        >
                            Delete Project
                        </Button>
                        <Button icon={<BoardRegular />} onClick={() => navigate(`/projects/${slug}/work-items`)} className={mergeClasses(isMobile && styles.headerActionButtonMobile)}>
                            Work Items
                        </Button>
                    </div>
                }
            />

            {dashboard.repo ? (
                <span className={mergeClasses(styles.repoLink, isMobile && styles.repoLinkMobile)} onClick={() => window.open(`https://github.com/${dashboard.repo}`, '_blank')}>
                    <LinkRegular />
                    {dashboard.repo}
                    <OpenRegular className={styles.openLinkIcon} />
                </span>
            ) : (
                <Text size={200}>No repository linked to this project.</Text>
            )}

            {/* Quick Actions */}
            <div className={mergeClasses(styles.quickActions, isDense && styles.quickActionsCompact, isMobile && styles.quickActionsMobile)}>
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
            <div className={mergeClasses(styles.metricsGrid, isDense && styles.metricsGridCompact, isMobile && styles.metricsGridMobile)}>
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
            <div className={mergeClasses(styles.twoColumns, isDense && styles.twoColumnsCompact)}>
                {/* Recent Activity */}
                <Card className={mergeClasses(styles.sectionCard, isDense && styles.sectionCardCompact)}>
                    <div className={mergeClasses(styles.sectionHeader, isMobile && styles.sectionHeaderMobile)}>
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
                <Card className={mergeClasses(styles.sectionCard, isDense && styles.sectionCardCompact)}>
                    <div className={mergeClasses(styles.sectionHeader, isMobile && styles.sectionHeaderMobile)}>
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

            <Dialog open={unlinkRepoOpen} onOpenChange={(_e, data) => setUnlinkRepoOpen(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Unlink Repository</DialogTitle>
                        <DialogContent>
                            This will disconnect the repository from this project. Existing work items remain, but agent and PR workflows that require a repo will stop until you link one again.
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">Cancel</Button>
                            </DialogTrigger>
                            <Button appearance="primary" onClick={handleUnlinkRepo} disabled={updateProject.isPending}>
                                {updateProject.isPending ? 'Unlinking...' : 'Unlink Repo'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            <Dialog open={deleteProjectOpen} onOpenChange={(_e, data) => setDeleteProjectOpen(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Delete Project</DialogTitle>
                        <DialogContent>
                            Delete <b>{dashboard.title}</b>? This permanently removes the project, work items, logs, and executions.
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">Cancel</Button>
                            </DialogTrigger>
                            <Button appearance="primary" onClick={handleDeleteProject} disabled={deleteProject.isPending}>
                                {deleteProject.isPending ? 'Deleting...' : 'Delete Project'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    )
}
