import { useState, useMemo, useRef, useCallback, type ChangeEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Input,
    Button,
    Dropdown,
    Option,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
    Spinner,
} from '@fluentui/react-components'
import {
    AddRegular,
    SearchRegular,
    GridRegular,
    TextAlignJustifyRegular,
    ArrowSortRegular,
    ArrowUploadRegular,
    ArrowDownloadRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { ProjectCard, ProjectRow, NewProjectDialog } from './'
import { useProjects, useExportProjects, useImportProjects } from '../../proxies'
import { usePreferences } from '../../hooks'
import type { ProjectData } from '../../models'

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
        padding: '1.5rem 2rem',
        maxWidth: '1400px',
        margin: '0 auto',
        width: '100%',
        height: '100%',
        overflow: 'auto',
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '1.5rem',
        gap: '0.75rem',
        flexWrap: 'wrap',
        padding: '0.5rem 0.625rem',
        borderRadius: tokens.borderRadiusLarge,
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke2,
        borderRightColor: tokens.colorNeutralStroke2,
        borderBottomColor: tokens.colorNeutralStroke2,
        borderLeftColor: tokens.colorNeutralStroke2,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    toolbarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        flex: 1,
        minWidth: '200px',
    },
    searchInput: {
        maxWidth: '300px',
        flex: 1,
    },
    sortDropdown: {
        minWidth: '140px',
    },
    projectGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))',
        gap: '1.125rem',
    },
    projectList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.375rem',
        paddingBottom: '1rem',
    },
    tableHeader: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 80px 80px 80px 100px 140px',
        alignItems: 'center',
        padding: '0.625rem 0.75rem',
        gap: '0.75rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        marginBottom: '0.125rem',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        position: 'sticky',
        top: 0,
        zIndex: 1,
    },
    tableHeaderCompact: {
        gridTemplateColumns: '2fr 1.2fr 56px 56px 64px 72px 120px',
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
    },
})

export function ProjectsPage() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { data: projects, isLoading } = useProjects()
    const exportProjectsMutation = useExportProjects()
    const importProjectsMutation = useImportProjects()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const importFileInputRef = useRef<HTMLInputElement | null>(null)

    const [searchQuery, setSearchQuery] = useState('')
    const [sortKey, setSortKey] = useState<SortKey>('Last activity')
    const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid')
    const [newProjectOpen, setNewProjectOpen] = useState(false)
    const effectiveViewMode: 'grid' | 'list' = isCompact ? 'list' : viewMode

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
            <div className={styles.page}>
                <Spinner label="Loading projects..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
            <PageHeader
                title="Projects"
                subtitle="Manage your projects and track AI agent progress"
                actions={
                    <>
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
                        >
                            Import
                        </Button>
                        <Button
                            appearance="secondary"
                            icon={<ArrowDownloadRegular />}
                            onClick={() => void handleExportProjects()}
                            disabled={exportProjectsMutation.isPending || (projects?.length ?? 0) === 0}
                        >
                            Export
                        </Button>
                        <Button
                            appearance="primary"
                            icon={<AddRegular />}
                            onClick={() => setNewProjectOpen(true)}
                        >
                            New Project
                        </Button>
                    </>
                }
            />

            <div className={styles.toolbar}>
                <div className={styles.toolbarLeft}>
                    <Input
                        className={styles.searchInput}
                        contentBefore={<SearchRegular />}
                        placeholder="Search projects..."
                        size="medium"
                        value={searchQuery}
                        onChange={(_e, data) => setSearchQuery(data.value)}
                    />
                    <Dropdown
                        placeholder="Sort by"
                        className={styles.sortDropdown}
                        value={sortKey}
                        onOptionSelect={(_e, data) => setSortKey((data.optionText ?? 'Last activity') as SortKey)}
                    >
                        {SORT_OPTIONS.map((opt) => (
                            <Option key={opt}>{opt}</Option>
                        ))}
                    </Dropdown>
                </div>
                <Toolbar>
                    {!isCompact && (
                        <>
                            <ToolbarButton
                                icon={<GridRegular />}
                                aria-label="Grid view"
                                onClick={() => setViewMode('grid')}
                                appearance={viewMode === 'grid' ? 'primary' : undefined}
                            />
                            <ToolbarButton
                                icon={<TextAlignJustifyRegular />}
                                aria-label="List view"
                                onClick={() => setViewMode('list')}
                                appearance={viewMode === 'list' ? 'primary' : undefined}
                            />
                            <ToolbarDivider />
                        </>
                    )}
                    <ToolbarButton
                        icon={<ArrowSortRegular />}
                        aria-label="Sort"
                        onClick={() => {
                            const idx = SORT_OPTIONS.indexOf(sortKey)
                            setSortKey(SORT_OPTIONS[(idx + 1) % SORT_OPTIONS.length])
                        }}
                    />
                </Toolbar>
            </div>

            {effectiveViewMode === 'grid' ? (
                <div className={styles.projectGrid}>
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

            <NewProjectDialog
                open={newProjectOpen}
                onOpenChange={setNewProjectOpen}
                onCreated={(slug) => navigate(`/projects/${slug}`)}
            />
        </div>
    )
}
