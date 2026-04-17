import { Suspense, useState, useMemo, useRef, useCallback, type ChangeEvent, type ComponentProps, type ComponentType, type LazyExoticComponent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    Caption1,
    Input,
    Button,
    Dropdown,
    Option,
    Spinner,
} from '@fluentui/react-components'
import {
    AddRegular,
    SearchRegular,
    FolderRegular,
    GridRegular,
    TextAlignJustifyRegular,
    ArrowUploadRegular,
    ArrowDownloadRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { EmptyState } from '../../components/shared'
import { ProjectCard, ProjectRow } from './'
import { useProjects, useExportProjects, useImportProjects } from '../../proxies'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ProjectData } from '../../models'
import { lazyWithRetry } from '../../utils/staleChunkRecovery'

function lazyDialog<TProps extends object>(
    importer: () => Promise<{ default: ComponentType<TProps> }>,
): LazyExoticComponent<ComponentType<TProps>> {
    return lazyWithRetry(importer as unknown as () => Promise<{ default: ComponentType<unknown> }>) as LazyExoticComponent<ComponentType<TProps>>
}

type NewProjectDialogProps = ComponentProps<typeof import('./NewProjectDialog').NewProjectDialog>

const NewProjectDialog = lazyDialog<NewProjectDialogProps>(() => import('./NewProjectDialog').then((module) => ({ default: module.NewProjectDialog })))

const SORT_OPTIONS = ['Last activity', 'Name', 'Work items', 'Agents'] as const
type SortKey = typeof SORT_OPTIONS[number]

const sortFns: Record<SortKey, (a: ProjectData, b: ProjectData) => number> = {
    'Last activity': (a, b) => b.lastActivity.localeCompare(a.lastActivity),
    'Name': (a, b) => a.title.localeCompare(b.title),
    'Work items': (a, b) => b.workItems.total - a.workItems.total,
    'Agents': (a, b) => b.agents.total - a.agents.total,
}

const useStyles = makeStyles({
    page: {
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.pageX,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.pageX,
        maxWidth: appTokens.width.pageLarge,
        margin: '0 auto',
        width: '100%',
        height: '100%',
        overflow: 'auto',
        minWidth: 0,
    },
    pageMobile: {
        paddingTop: appTokens.space.pageYMobile,
        paddingBottom: appTokens.space.pageYMobile,
        paddingLeft: appTokens.space.pageXMobile,
        paddingRight: appTokens.space.pageXMobile,
    },
    headerActions: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    headerActionsMobile: {
        width: '100%',
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
        gap: appTokens.space.sm,
    },
    headerActionButtonMobile: {
        width: '100%',
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: appTokens.space.xl,
        gap: appTokens.space.md,
        flexWrap: 'wrap',
        paddingTop: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.sm,
        borderRadius: appTokens.radius.lg,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
    },
    toolbarMobile: {
        marginBottom: appTokens.space.pageYMobile,
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.xs,
        paddingRight: appTokens.space.xs,
        gap: appTokens.space.sm,
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    toolbarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flex: 1,
        minWidth: '200px',
    },
    toolbarLeftMobile: {
        minWidth: 0,
        width: '100%',
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    searchInput: {
        maxWidth: '300px',
        flex: 1,
    },
    searchInputMobile: {
        maxWidth: 'unset',
        minWidth: 0,
        width: '100%',
    },
    sortDropdown: {
        minWidth: '140px',
    },
    sortDropdownMobile: {
        minWidth: '120px',
        width: '100%',
    },
    toolbarRight: {
        display: 'flex',
        alignItems: 'center',
    },
    projectGrid: {
        display: 'grid',
        gridTemplateColumns: `repeat(auto-fill, minmax(min(100%, ${appTokens.width.projectCardMin}), 1fr))`,
        gap: `calc(${appTokens.space.lg} + ${appTokens.space.xxs})`,
    },
    projectGridMobile: {
        gridTemplateColumns: '1fr',
        gap: appTokens.space.md,
    },
    projectList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        paddingBottom: appTokens.space.lg,
    },
    tableHeader: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 80px 80px 80px 100px 140px',
        alignItems: 'center',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        gap: appTokens.space.md,
        borderBottom: appTokens.border.subtle,
        marginBottom: appTokens.space.xxxs,
        backgroundColor: appTokens.color.surfaceAlt,
        borderRadius: appTokens.radius.md,
        position: 'sticky',
        top: 0,
        zIndex: 1,
    },
    tableHeaderCompact: {
        gridTemplateColumns: '2fr 1.2fr 56px 56px 64px 72px 120px',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.sm,
    },
})

export function ProjectsPage() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { data: projects, isLoading } = useProjects()
    const exportProjectsMutation = useExportProjects()
    const importProjectsMutation = useImportProjects()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const importFileInputRef = useRef<HTMLInputElement | null>(null)

    const [searchQuery, setSearchQuery] = useState('')
    const [sortKey, setSortKey] = useState<SortKey>('Last activity')
    const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid')
    const [newProjectOpen, setNewProjectOpen] = useState(false)
    const effectiveViewMode: 'grid' | 'list' = isMobile ? 'grid' : (isCompact ? 'list' : viewMode)
    const hasProjects = (projects?.length ?? 0) > 0

    const filteredProjects = useMemo(() => {
        const list = projects ?? []
        const filtered = searchQuery
            ? list.filter(
                (p) =>
                    p.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                    p.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
                    p.repo.toLowerCase().includes(searchQuery.toLowerCase()),
            )
            : list
        return [...filtered].sort(sortFns[sortKey])
    }, [projects, searchQuery, sortKey])

    const handleExportProjects = useCallback(async () => {
        try {
            const blob = await exportProjectsMutation.mutateAsync()
            const downloadUrl = URL.createObjectURL(blob)
            const anchor = document.createElement('a')
            anchor.href = downloadUrl
            anchor.download = `fleet-projects-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
            document.body.appendChild(anchor)
            anchor.click()
            anchor.remove()
            URL.revokeObjectURL(downloadUrl)
        } catch {
            // Ignore: errors are surfaced by query error boundaries / API handlers.
        }
    }, [exportProjectsMutation])

    const handleImportClick = useCallback(() => {
        importFileInputRef.current?.click()
    }, [])

    const handleImportFileChange = useCallback(async (event: ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0]
        if (!file) {
            return
        }

        try {
            const text = await file.text()
            const payload = JSON.parse(text) as unknown
            await importProjectsMutation.mutateAsync(payload)
        } catch {
            // Ignore: invalid JSON or API errors are surfaced by API handlers.
        } finally {
            event.target.value = ''
        }
    }, [importProjectsMutation])

    if (isLoading) {
        return (
            <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
                <Spinner label="Loading projects..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
            <PageHeader
                title="Projects"
                subtitle="Projects, backlog, and active execution."
                actions={
                    <div className={mergeClasses(styles.headerActions, isMobile && styles.headerActionsMobile)}>
                        <input
                            ref={importFileInputRef}
                            type="file"
                            accept=".json,application/json"
                            style={{ display: 'none' }}
                            onChange={handleImportFileChange}
                        />
                        <Button
                            appearance="secondary"
                            icon={<ArrowUploadRegular />}
                            onClick={handleImportClick}
                            disabled={importProjectsMutation.isPending}
                            className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                        >
                            Import
                        </Button>
                        <Button
                            appearance="secondary"
                            icon={<ArrowDownloadRegular />}
                            onClick={() => void handleExportProjects()}
                            disabled={exportProjectsMutation.isPending || (projects?.length ?? 0) === 0}
                            className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                        >
                            Export
                        </Button>
                        <Button
                            appearance="primary"
                            icon={<AddRegular />}
                            onClick={() => setNewProjectOpen(true)}
                            className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                        >
                            New Project
                        </Button>
                    </div>
                }
            />

            <div className={mergeClasses(styles.toolbar, isMobile && styles.toolbarMobile)}>
                <div className={mergeClasses(styles.toolbarLeft, isMobile && styles.toolbarLeftMobile)}>
                    <Input
                        className={mergeClasses(styles.searchInput, isMobile && styles.searchInputMobile)}
                        contentBefore={<SearchRegular />}
                        placeholder="Search projects..."
                        size="medium"
                        value={searchQuery}
                        onChange={(_e, data) => setSearchQuery(data.value)}
                    />
                    <Dropdown
                        placeholder="Sort by"
                        className={mergeClasses(styles.sortDropdown, isMobile && styles.sortDropdownMobile)}
                        value={sortKey}
                        onOptionSelect={(_e, data) => setSortKey((data.optionText ?? 'Last activity') as SortKey)}
                    >
                        {SORT_OPTIONS.map((opt) => (
                            <Option key={opt}>{opt}</Option>
                        ))}
                    </Dropdown>
                </div>
                <div className={styles.toolbarRight}>
                    {!isDense && (
                        <>
                            <Button
                                icon={<GridRegular />}
                                aria-label="Grid view"
                                onClick={() => setViewMode('grid')}
                                appearance={viewMode === 'grid' ? 'primary' : undefined}
                                size="small"
                            />
                            <Button
                                icon={<TextAlignJustifyRegular />}
                                aria-label="List view"
                                onClick={() => setViewMode('list')}
                                appearance={viewMode === 'list' ? 'primary' : undefined}
                                size="small"
                            />
                        </>
                    )}
                </div>
            </div>

            {filteredProjects.length === 0 ? (
                <EmptyState
                    icon={<FolderRegular style={{ fontSize: '48px' }} />}
                    title={hasProjects ? 'No projects match this view' : 'No projects yet'}
                    description={hasProjects
                        ? 'Try a different search term or sort option.'
                        : 'Create your first project to start organizing work, memory, playbooks, and agent runs.'}
                    actions={hasProjects ? (
                        <Button appearance="secondary" onClick={() => setSearchQuery('')}>
                            Clear Search
                        </Button>
                    ) : (
                        <Button appearance="primary" icon={<AddRegular />} onClick={() => setNewProjectOpen(true)}>
                            New Project
                        </Button>
                    )}
                />
            ) : effectiveViewMode === 'grid' ? (
                <div className={mergeClasses(styles.projectGrid, isMobile && styles.projectGridMobile)}>
                    {filteredProjects.map((project) => (
                        <ProjectCard
                            key={project.id}
                            project={project}
                            onClick={() => navigate(`/projects/${project.slug}`)}
                        />
                    ))}
                </div>
            ) : (
                <div className={styles.projectList}>
                    <div className={mergeClasses(styles.tableHeader, isCompact && styles.tableHeaderCompact)}>
                        <Caption1><b>Project</b></Caption1>
                        <Caption1><b>Repository</b></Caption1>
                        <Caption1><b>Items</b></Caption1>
                        <Caption1><b>Active</b></Caption1>
                        <Caption1><b>Resolved</b></Caption1>
                        <Caption1><b>Agents</b></Caption1>
                        <Caption1><b>Last Activity</b></Caption1>
                    </div>
                    {filteredProjects.map((project) => (
                        <ProjectRow
                            key={project.id}
                            project={project}
                            onClick={() => navigate(`/projects/${project.slug}`)}
                        />
                    ))}
                </div>
            )}

            <Suspense fallback={null}>
                {newProjectOpen ? (
                    <NewProjectDialog
                        open={newProjectOpen}
                        onOpenChange={setNewProjectOpen}
                        onCreated={(slug) => navigate(`/projects/${slug}`)}
                    />
                ) : null}
            </Suspense>
        </div>
    )
}
