import { useState } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Input,
    Tab,
    TabList,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components'
import {
    SearchRegular,
    FilterRegular,
    BoardRegular,
    TextAlignJustifyRegular,
    ChatRegular,
} from '@fluentui/react-icons'
import { ChatDrawer } from '../../components/chat'
import { PageHeader } from '../../components/shared'
import { KanbanColumn } from './KanbanColumn'
import { BacklogList } from './BacklogList'
import { CreateWorkItemDialog } from './CreateWorkItemDialog'
import type { WorkItem } from '../../models'

const MOCK_WORK_ITEMS: WorkItem[] = [
    { id: 101, title: 'Set up authentication with OAuth', state: 'In Progress (AI)', priority: 1, assignedTo: 'Agent', tags: ['auth', 'backend'], isAI: true, description: 'Implement GitHub and Google OAuth sign-in flow.' },
    { id: 102, title: 'Design landing page', state: 'New', priority: 2, assignedTo: 'Unassigned', tags: ['frontend', 'design'], isAI: false, description: 'Create the initial landing page design with hero section.' },
    { id: 103, title: 'Create project model & API', state: 'Active', priority: 1, assignedTo: 'Agent', tags: ['backend', 'api'], isAI: true, description: 'Define the Project data model and CRUD endpoints.' },
    { id: 104, title: 'Implement work item board view', state: 'In Progress (AI)', priority: 2, assignedTo: 'Agent', tags: ['frontend', 'ui'], isAI: true, description: 'Build the Kanban-style board view for work items.' },
    { id: 105, title: 'Set up CI/CD pipeline', state: 'Resolved (AI)', priority: 1, assignedTo: 'Agent', tags: ['devops'], isAI: true, description: 'Configure GitHub Actions for build, test, and deploy.' },
    { id: 106, title: 'Add Redis caching layer', state: 'Active', priority: 3, assignedTo: 'Unassigned', tags: ['backend', 'performance'], isAI: false, description: 'Integrate Redis output caching for API endpoints.' },
    { id: 107, title: 'User profile page', state: 'New', priority: 3, assignedTo: 'Unassigned', tags: ['frontend'], isAI: false, description: 'Build the user profile and account settings page.' },
    { id: 108, title: 'Agent execution logs', state: 'In Progress', priority: 2, assignedTo: 'Agent', tags: ['backend', 'agents'], isAI: true, description: 'Implement log capture and storage for agent executions.' },
    { id: 109, title: 'GitHub repo integration', state: 'Resolved', priority: 1, assignedTo: 'You', tags: ['integration'], isAI: false, description: 'Enable linking GitHub repos to Fleet projects.' },
    { id: 110, title: 'Dark mode support', state: 'Closed', priority: 4, assignedTo: 'You', tags: ['frontend', 'theme'], isAI: false, description: 'Add dark/light theme toggle support.' },
    { id: 111, title: 'Search functionality', state: 'New', priority: 2, assignedTo: 'Unassigned', tags: ['frontend', 'backend'], isAI: false, description: 'Global search across projects, work items, and chats.' },
    { id: 112, title: 'Notification system', state: 'New', priority: 3, assignedTo: 'Unassigned', tags: ['backend', 'frontend'], isAI: false, description: 'Push notifications for PR ready, agent errors, task completion.' },
]

const BOARD_STATES = ['New', 'Active', 'In Progress', 'Resolved', 'Closed']

function getBoardColumns(items: WorkItem[]) {
    return BOARD_STATES.map((state) => ({
        state,
        items: items.filter((item) => {
            if (state === 'In Progress') return item.state === 'In Progress' || item.state === 'In Progress (AI)'
            if (state === 'Resolved') return item.state === 'Resolved' || item.state === 'Resolved (AI)'
            return item.state === state
        }),
    }))
}

const useStyles = makeStyles({
    root: {
        display: 'flex',
        height: '100%',
        overflow: 'hidden',
    },
    page: {
        padding: '1.5rem 2rem',
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        overflow: 'hidden',
        minWidth: 0,
    },
    headerActions: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'center',
    },
    toolbarRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '1rem',
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
        maxWidth: '280px',
        flex: 1,
    },
    toolbarButtonActive: {
        color: tokens.colorBrandForeground1,
    },
    boardContainer: {
        flex: 1,
        display: 'flex',
        gap: '0.75rem',
        overflow: 'auto',
        paddingBottom: '1rem',
    },
})

export function WorkItemsPage() {
    const styles = useStyles()
    const [viewMode, setViewMode] = useState<'board' | 'list'>('board')
    const [createDialogOpen, setCreateDialogOpen] = useState(false)
    const [chatOpen, setChatOpen] = useState(false)

    const boardColumns = getBoardColumns(MOCK_WORK_ITEMS)

    return (
        <div className={styles.root}>
            <div className={styles.page}>
                <PageHeader
                    title="Work Items"
                    subtitle="Manage your backlog, track progress, and assign agents"
                    actions={
                        <div className={styles.headerActions}>
                            <Button
                                appearance={chatOpen ? 'primary' : 'outline'}
                                icon={<ChatRegular />}
                                onClick={() => setChatOpen(!chatOpen)}
                            >
                                AI Chat
                            </Button>
                            <CreateWorkItemDialog open={createDialogOpen} onOpenChange={setCreateDialogOpen} />
                        </div>
                    }
                />

                <div className={styles.toolbarRow}>
                    <div className={styles.toolbarLeft}>
                        <Input
                            className={styles.searchInput}
                            contentBefore={<SearchRegular />}
                            placeholder="Search work items..."
                            size="medium"
                        />
                        <Toolbar>
                            <ToolbarButton icon={<FilterRegular />}>Filter</ToolbarButton>
                            <ToolbarDivider />
                            <ToolbarButton
                                icon={<BoardRegular />}
                                aria-label="Board view"
                                onClick={() => setViewMode('board')}
                                className={viewMode === 'board' ? styles.toolbarButtonActive : undefined}
                            />
                            <ToolbarButton
                                icon={<TextAlignJustifyRegular />}
                                aria-label="List view"
                                onClick={() => setViewMode('list')}
                                className={viewMode === 'list' ? styles.toolbarButtonActive : undefined}
                            />
                        </Toolbar>
                    </div>
                    <TabList selectedValue={viewMode} onTabSelect={(_e, data) => setViewMode(data.value as 'board' | 'list')}>
                        <Tab value="board" icon={<BoardRegular />}>Board</Tab>
                        <Tab value="list" icon={<TextAlignJustifyRegular />}>Backlog</Tab>
                    </TabList>
                </div>

                {viewMode === 'board' ? (
                    <div className={styles.boardContainer}>
                        {boardColumns.map((col) => (
                            <KanbanColumn key={col.state} state={col.state} items={col.items} />
                        ))}
                    </div>
                ) : (
                    <BacklogList items={MOCK_WORK_ITEMS} />
                )}
            </div>

            {chatOpen && <ChatDrawer onClose={() => setChatOpen(false)} />}
        </div>
    )
}
