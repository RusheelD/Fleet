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
    BoardRegular,
    BotRegular,
    LinkRegular,
    OpenRegular,
} from '@fluentui/react-icons'
import { FleetRocketLogo, MemoryWorkspace, PageHeader, PlaybookWorkspace } from '../../components/shared'
import { MetricCard, ActivityItem, AgentStatusRow, QuickActionCard } from './'
import {
    useProjectDashboardBySlug,
    useDeleteProject,
    useUpdateProject,
    useProjectMemories,
    useCreateProjectMemory,
    useUpdateProjectMemory,
    useDeleteProjectMemory,
    useSkillTemplates,
    useProjectSkills,
    useCreateProjectSkill,
    useUpdateProjectSkill,
    useDeleteProjectSkill,
    resolveIcon,
    type UpsertMemoryEntryRequest,
    type UpsertPromptSkillRequest,
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
    quickActions: {
        display: 'grid',
        gridTemplateColumns: `repeat(auto-fill, minmax(min(100%, ${appTokens.width.quickActionMin}), 1fr))`,
        gap: appTokens.space.md,
        marginBottom: appTokens.space.xl,
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
        display: 'flex',
        flexWrap: 'wrap',
        marginBottom: '0.75rem',
        wordBreak: 'break-word',
    },
    projectOverview: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1.4fr) minmax(280px, 0.9fr)',
        gap: appTokens.space.md,
        marginBottom: appTokens.space.xl,
        '@media (max-width: 980px)': {
            gridTemplateColumns: '1fr',
        },
    },
    projectOverviewPrimary: {
        padding: appTokens.space.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    projectOverviewSignals: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        gap: appTokens.space.md,
    },
    signalCard: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
        backgroundColor: appTokens.color.surface,
        border: appTokens.border.subtle,
    },
    signalValue: {
        fontSize: appTokens.fontSize.xl,
        fontWeight: appTokens.fontWeight.bold,
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
    const createProjectMemory = useCreateProjectMemory(dashboard?.id)
    const updateProjectMemory = useUpdateProjectMemory(dashboard?.id)
    const deleteProjectMemory = useDeleteProjectMemory(dashboard?.id)
    const skillTemplates = useSkillTemplates()
    const projectSkills = useProjectSkills(dashboard?.id, Boolean(dashboard?.id))
    const createProjectSkill = useCreateProjectSkill(dashboard?.id)
    const updateProjectSkill = useUpdateProjectSkill(dashboard?.id)
    const deleteProjectSkill = useDeleteProjectSkill(dashboard?.id)
    const deleteProject = useDeleteProject()
    const updateProject = useUpdateProject()
    const [unlinkRepoOpen, setUnlinkRepoOpen] = useState(false)
    const [deleteProjectOpen, setDeleteProjectOpen] = useState(false)
    const isMemorySaving = createProjectMemory.isPending || updateProjectMemory.isPending || deleteProjectMemory.isPending
    const isPlaybookSaving = createProjectSkill.isPending || updateProjectSkill.isPending || deleteProjectSkill.isPending
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

    const handleCreateMemory = useCallback(
        (request: UpsertMemoryEntryRequest) => createProjectMemory.mutateAsync(request),
        [createProjectMemory],
    )

    const handleUpdateMemory = useCallback(
        (id: number, request: UpsertMemoryEntryRequest) => updateProjectMemory.mutateAsync({ id, data: request }),
        [updateProjectMemory],
    )

    const handleDeleteMemory = useCallback(
        (id: number) => deleteProjectMemory.mutateAsync(id),
        [deleteProjectMemory],
    )

    const handleCreatePlaybook = useCallback(
        (request: UpsertPromptSkillRequest) => createProjectSkill.mutateAsync(request),
        [createProjectSkill],
    )

    const handleUpdatePlaybook = useCallback(
        (id: number, request: UpsertPromptSkillRequest) => updateProjectSkill.mutateAsync({ id, data: request }),
        [updateProjectSkill],
    )

    const handleDeletePlaybook = useCallback(
        (id: number) => deleteProjectSkill.mutateAsync(id),
        [deleteProjectSkill],
    )

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

            <div className={styles.projectOverview}>
                <Card className={styles.projectOverviewPrimary}>
                    <Text weight="semibold">Project context</Text>
                    <Text size={400}>
                        Keep planning, execution, memory, and repo-linked delivery together so the team never has to reconstruct context from scratch.
                    </Text>
                    {dashboard.repo ? (
                        <span className={mergeClasses(styles.repoLink, isMobile && styles.repoLinkMobile)} onClick={() => window.open(`https://github.com/${dashboard.repo}`, '_blank')}>
                            <LinkRegular />
                            {dashboard.repo}
                            <OpenRegular className={styles.openLinkIcon} />
                        </span>
                    ) : (
                        <Text size={200}>No repository linked to this project yet. Link one to unlock PR-driven execution.</Text>
                    )}
                    <Text size={200} className={styles.overviewCaption}>
                        The quick actions below are tuned for the most common next steps: shape work, open chat, and monitor execution.
                    </Text>
                </Card>
                <div className={styles.projectOverviewSignals}>
                    <Card className={styles.signalCard}>
                        <Caption1>Project memory</Caption1>
                        <Text className={styles.signalValue}>{projectMemoryCount}</Text>
                        <Caption1>Durable notes and references saved for this project.</Caption1>
                    </Card>
                    <Card className={styles.signalCard}>
                        <Caption1>Playbooks</Caption1>
                        <Text className={styles.signalValue}>{projectPlaybookCount}</Text>
                        <Caption1>Reusable project-specific workflows available to Fleet.</Caption1>
                    </Card>
                    <Card className={styles.signalCard}>
                        <Caption1>Active agents</Caption1>
                        <Text className={styles.signalValue}>{activeAgentCount}</Text>
                        <Caption1>Agents currently moving work forward in this project.</Caption1>
                    </Card>
                    <Card className={styles.signalCard}>
                        <Caption1>Recent activity</Caption1>
                        <Text className={styles.signalValue}>{dashboard.activities.length}</Text>
                        <Caption1>Recent events surfaced on the dashboard right now.</Caption1>
                    </Card>
                </div>
            </div>

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
                    icon={<FleetRocketLogo size={20} title="Run agents" variant="outline" />}
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

            <MemoryWorkspace
                title="Project Memory"
                subtitle="Keep non-code context close to the work: deadlines, known gotchas, stakeholder notes, and reference links that matter across sessions."
                memories={projectMemories.data}
                isLoading={projectMemories.isLoading}
                isSaving={isMemorySaving}
                emptyMessage="No project memory has been saved yet."
                createLabel="New Project Memory"
                onCreate={handleCreateMemory}
                onUpdate={handleUpdateMemory}
                onDelete={handleDeleteMemory}
            />

            <PlaybookWorkspace
                title="Project Playbooks"
                subtitle="Create project-specific workflows Fleet should reuse for this team, such as rollout checklists, triage standards, or backlog shaping conventions."
                templates={skillTemplates.data}
                playbooks={projectSkills.data}
                isLoading={skillTemplates.isLoading || projectSkills.isLoading}
                isSaving={isPlaybookSaving}
                emptyMessage="No project playbooks have been created yet."
                createLabel="New Project Playbook"
                onCreate={handleCreatePlaybook}
                onUpdate={handleUpdatePlaybook}
                onDelete={handleDeletePlaybook}
            />

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
