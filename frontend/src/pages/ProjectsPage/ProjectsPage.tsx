import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    Input,
    Button,
    Dropdown,
    Option,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components'
import {
    AddRegular,
    SearchRegular,
    GridRegular,
    TextAlignJustifyRegular,
    ArrowSortRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { ProjectCard } from './ProjectCard'
import type { ProjectData } from '../../models'

const MOCK_PROJECTS: ProjectData[] = [
    { id: '1', title: 'Fleet Platform', description: 'AI-powered project management and agent orchestration', repo: 'RusheelD/Fleet', workItems: { total: 24, active: 8, resolved: 12 }, agents: { total: 5, running: 3 }, lastActivity: '15 min ago' },
    { id: '2', title: 'E-Commerce API', description: 'RESTful API for online marketplace', repo: 'RusheelD/ecommerce-api', workItems: { total: 18, active: 5, resolved: 10 }, agents: { total: 3, running: 1 }, lastActivity: '2 hours ago' },
    { id: '3', title: 'Mobile App', description: 'Cross-platform mobile application', repo: 'RusheelD/mobile-app', workItems: { total: 31, active: 12, resolved: 15 }, agents: { total: 4, running: 2 }, lastActivity: '1 hour ago' },
    { id: '4', title: 'Data Pipeline', description: 'ETL pipeline for analytics', repo: 'RusheelD/data-pipeline', workItems: { total: 12, active: 3, resolved: 8 }, agents: { total: 2, running: 0 }, lastActivity: '1 day ago' },
    { id: '5', title: 'Auth Service', description: 'Microservice for authentication and authorization', repo: 'RusheelD/auth-service', workItems: { total: 9, active: 2, resolved: 6 }, agents: { total: 2, running: 1 }, lastActivity: '5 hours ago' },
    { id: '6', title: 'Design System', description: 'Shared UI component library', repo: 'RusheelD/design-system', workItems: { total: 15, active: 4, resolved: 7 }, agents: { total: 1, running: 0 }, lastActivity: '3 days ago' },
]

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
})

export function ProjectsPage() {
    const styles = useStyles()
    const navigate = useNavigate()

    return (
        <div className={styles.page}>
            <PageHeader
                title="All Projects"
                subtitle="Manage your projects and track AI agent progress"
                actions={
                    <Button appearance="primary" icon={<AddRegular />}>
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
                    />
                    <Dropdown placeholder="Sort by" className={styles.sortDropdown}>
                        <Option>Last activity</Option>
                        <Option>Name</Option>
                        <Option>Work items</Option>
                        <Option>Agents</Option>
                    </Dropdown>
                </div>
                <Toolbar>
                    <ToolbarButton icon={<GridRegular />} aria-label="Grid view" />
                    <ToolbarButton icon={<TextAlignJustifyRegular />} aria-label="List view" />
                    <ToolbarDivider />
                    <ToolbarButton icon={<ArrowSortRegular />} aria-label="Sort" />
                </Toolbar>
            </div>

            <div className={styles.projectGrid}>
                {MOCK_PROJECTS.map((project) => (
                    <ProjectCard
                        key={project.id}
                        project={project}
                        onClick={() => navigate(`/projects/${project.id}`)}
                    />
                ))}
            </div>
        </div>
    )
}
