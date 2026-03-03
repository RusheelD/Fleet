import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
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
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { ProjectCard, ProjectRow, NewProjectDialog } from './'
import { useProjects } from '../../proxies'
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
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '1.5rem',
        gap: '0.75rem',
        flexWrap: 'wrap',
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
        gap: '1rem',
    },
    projectList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    tableHeader: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 80px 80px 80px 100px 140px',
        alignItems: 'center',
        padding: '0.5rem 0.75rem',
        gap: '0.75rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        marginBottom: '0.25rem',
    },
})

export function ProjectsPage() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { data: projects, isLoading } = useProjects()

    const [searchQuery, setSearchQuery] = useState('')
    const [sortKey, setSortKey] = useState<SortKey>('Last activity')
    const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid')
    const [newProjectOpen, setNewProjectOpen] = useState(false)

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
                    <Button
                        appearance="primary"
                        icon={<AddRegular />}
                        onClick={() => setNewProjectOpen(true)}
                    >
                        New Project
                    </Button>
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

            {viewMode === 'grid' ? (
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
                    <div className={styles.tableHeader}>
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
