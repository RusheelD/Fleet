import { useCallback, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    Title3,
    Card,
    Button,
    Divider,
    Spinner,
    Text,
    Caption1,
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
    LinkRegular,
    OpenRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { MetricCard, ActivityItem, AgentStatusRow } from './'
import {
    useProjectDashboardBySlug,
    useDeleteProject,
    useUpdateProject,
    useProjectMemories,
    useProjectSkills,
    resolveIcon,
} from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile } from '../../hooks'
import { appTokens, APP_MOBILE_MEDIA_QUERY } from '../../styles/appTokens'

const useStyles = makeStyles({
    page: {
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.pageX,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.pageX,
        maxWidth: appTokens.width.pageLarge,
        margin: '0 auto',
        width: '100%',
        minWidth: 0,
    },
    pageMobile: {
        paddingTop: appTokens.space.pageYMobile,
        paddingBottom: appTokens.space.pageYMobile,
        paddingLeft: appTokens.space.pageXMobile,
        paddingRight: appTokens.space.pageXMobile,
    },
    repoLink: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: appTokens.color.brand,
        fontSize: '13px',
        cursor: 'pointer',
        minWidth: 0,
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
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
        gap: '0.5rem',
    },
    headerActionButtonMobile: {
        width: '100%',
    },
    metricsGrid: {
        display: 'grid',
        gridTemplateColumns: `repeat(auto-fill, minmax(${appTokens.width.metricCardMin}, 1fr))`,
        gap: appTokens.space.lg,
        marginBottom: appTokens.space.xl,
    },
    metricsGridCompact: {
        gridTemplateColumns: '1fr 1fr',
        gap: '0.5rem',
        marginBottom: '1rem',
    },
    twoColumns: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.xl,
        marginBottom: appTokens.space.xl,
        [`@media ${APP_MOBILE_MEDIA_QUERY}`]: {
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
        gap: appTokens.space.lg,
        minWidth: 0,
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
    metricsGridMobile: {
        gridTemplateColumns: '1fr 1fr',
    },
    repoLinkMobile: {
        display: 'flex',
        flexWrap: 'wrap',
        marginBottom: '0.75rem',
        wordBreak: 'break-word',
    },
    projectOverview: {
        display: 'grid',
        gridTemplateColumns: '1fr',
        marginBottom: appTokens.space.xl,
    },
    projectOverviewPrimary: {
        padding: appTokens.space.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        backgroundColor: appTokens.color.surface,
    },
    projectOverviewMeta: {
        color: appTokens.color.textSecondary,
        overflowWrap: 'anywhere',
    },
    overviewCaption: {
        color: appTokens.color.textTertiary,
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
    const projectMemories = useProjectMemories(dashboard?.id, Boolean(dashboard?.id))
    const projectSkills = useProjectSkills(dashboard?.id, Boolean(dashboard?.id))
    const deleteProject = useDeleteProject()
    const updateProject = useUpdateProject()
    const [unlinkRepoOpen, setUnlinkRepoOpen] = useState(false)
    const [deleteProjectOpen, setDeleteProjectOpen] = useState(false)
    const projectMemoryCount = projectMemories.data?.length ?? 0
    const projectPlaybookCount = projectSkills.data?.length ?? 0
    const activeAgentCount = dashboard?.agents.filter((agent) => agent.status.toLowerCase() === 'running').length ?? 0

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
                    </div>
                }
            />

            <div className={styles.projectOverview}>
                <Card className={styles.projectOverviewPrimary}>
                    <Text weight="semibold">Repository</Text>
                    {dashboard.repo ? (
                        <span className={mergeClasses(styles.repoLink, isMobile && styles.repoLinkMobile)} onClick={() => window.open(`https://github.com/${dashboard.repo}`, '_blank')}>
                            <LinkRegular />
                            {dashboard.repo}
                            <OpenRegular className={styles.openLinkIcon} />
                        </span>
                    ) : (
                        <Text size={200}>No repository linked to this project.</Text>
                    )}
                    <Text size={200} className={styles.overviewCaption}>
                        Use the project pages to shape work, monitor runs, and keep context close to the code.
                    </Text>
                    <Caption1 className={styles.projectOverviewMeta}>
                        {projectMemoryCount} memory note{projectMemoryCount === 1 ? '' : 's'} | {projectPlaybookCount} playbook{projectPlaybookCount === 1 ? '' : 's'} | {activeAgentCount} active agent{activeAgentCount === 1 ? '' : 's'} | {dashboard.activities.length} recent activit{dashboard.activities.length === 1 ? 'y' : 'ies'}
                    </Caption1>
                </Card>
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
