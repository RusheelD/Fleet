import { useState, useMemo, useCallback } from 'react'
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
    Spinner,
} from '@fluentui/react-components'
import {
    SearchRegular,
    FilterRegular,
    BoardRegular,
    TextBulletListLtrRegular,
    TextBulletListTreeRegular,
    AddRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { KanbanColumn, BacklogTreeTable, BacklogList, CreateWorkItemDialog, WorkItemDetailDialog, ManageLevelsDialog } from './'
import { useWorkItems, useWorkItemLevels, useUpdateWorkItem } from '../../proxies'
import { useCurrentProject } from '../../hooks'
import type { WorkItem, WorkItemLevel } from '../../models'

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
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    toolbarRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalM,
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
    },
    toolbarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flex: 1,
        minWidth: '200px',
    },
    searchInput: {
        maxWidth: '280px',
        flex: 1,
    },
    boardContainer: {
        flex: 1,
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        overflow: 'auto',
        paddingBottom: tokens.spacingVerticalM,
    },
})

export function WorkItemsPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { data: workItems, isLoading } = useWorkItems(projectId)
    const { data: levels } = useWorkItemLevels(projectId)
    const [viewMode, setViewMode] = useState<'backlog' | 'list' | 'board'>('backlog')
    const [createDialogOpen, setCreateDialogOpen] = useState(false)
    const [manageLevelsOpen, setManageLevelsOpen] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const [selectedItem, setSelectedItem] = useState<WorkItem | null>(null)

    const updateMutation = useUpdateWorkItem(projectId)

    const handleReparent = useCallback((itemId: number, newParentId: number | null) => {
        // Send 0 to clear parent (backend treats 0 as 'set to null')
        updateMutation.mutate({ workItemNumber: itemId, data: { parentWorkItemNumber: newParentId ?? 0 } })
    }, [updateMutation])

    const handleTitleChange = useCallback((itemId: number, newTitle: string) => {
        updateMutation.mutate({ workItemNumber: itemId, data: { title: newTitle } })
    }, [updateMutation])

    const levelMap = useMemo(() => {
        const map = new Map<number, WorkItemLevel>()
        for (const level of levels ?? []) {
            map.set(level.id, level)
        }
        return map
    }, [levels])

    const items = useMemo(() => {
        const all = workItems ?? []
        if (!searchQuery) return all
        const q = searchQuery.toLowerCase()
        return all.filter(
            (wi) =>
                wi.title.toLowerCase().includes(q) ||
                wi.description.toLowerCase().includes(q) ||
                wi.assignedTo.toLowerCase().includes(q) ||
                wi.tags.some((t) => t.toLowerCase().includes(q)),
        )
    }, [workItems, searchQuery])

    const boardColumns = useMemo(() => getBoardColumns(items), [items])

    if (isLoading) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading work items..." />
            </div>
        )
    }

    return (
        <div className={styles.root}>
            <div className={styles.page}>
                <PageHeader
                    title="Work Items"
                    subtitle="Manage your backlog, track progress, and assign agents"
                    actions={
                        <div className={styles.headerActions}>
                            <Button
                                appearance="primary"
                                icon={<AddRegular />}
                                onClick={() => setCreateDialogOpen(true)}
                            >
                                New Work Item
                            </Button>
                        </div>
                    }
                />

                <div className={styles.toolbarRow}>
                    <div className={styles.toolbarLeft}>
                        <TabList
                            selectedValue={viewMode}
                            onTabSelect={(_e, data) => setViewMode(data.value as 'backlog' | 'list' | 'board')}
                            size="small"
                        >
                            <Tab value="backlog" icon={<TextBulletListTreeRegular />}>Backlog</Tab>
                            <Tab value="list" icon={<TextBulletListLtrRegular />}>List</Tab>
                            <Tab value="board" icon={<BoardRegular />}>Board</Tab>
                        </TabList>
                        <Toolbar>
                            <ToolbarDivider />
                            <Input
                                className={styles.searchInput}
                                placeholder="Search work items..."
                                size="small"
                                appearance="underline"
                                value={searchQuery}
                                onChange={(_e, data) => setSearchQuery(data.value)}
                                contentBefore={<SearchRegular />}
                            />
                            <ToolbarButton icon={<FilterRegular />}>Filter</ToolbarButton>
                            <ToolbarDivider />
                            <ToolbarButton onClick={() => setManageLevelsOpen(true)}>Levels</ToolbarButton>
                        </Toolbar>
                    </div>
                </div>

                {viewMode === 'backlog' && (
                    <BacklogTreeTable
                        items={items}
                        levelMap={levelMap}
                        selectedItemId={selectedItem?.workItemNumber}
                        onItemClick={setSelectedItem}
                        onReparent={handleReparent}
                        onTitleChange={handleTitleChange}
                    />
                )}
                {viewMode === 'list' && (
                    <BacklogList
                        items={items}
                        levelMap={levelMap}
                        selectedItemId={selectedItem?.workItemNumber}
                        onItemClick={setSelectedItem}
                        onTitleChange={handleTitleChange}
                    />
                )}
                {viewMode === 'board' && (
                    <div className={styles.boardContainer}>
                        {boardColumns.map((col) => (
                            <KanbanColumn key={col.state} state={col.state} items={col.items} levelMap={levelMap} onItemClick={setSelectedItem} />
                        ))}
                    </div>
                )}
            </div>

            <CreateWorkItemDialog projectId={projectId ?? ''} workItems={workItems ?? []} levels={levels ?? []} open={createDialogOpen} onOpenChange={setCreateDialogOpen} />
            <WorkItemDetailDialog projectId={projectId ?? ''} item={selectedItem} workItems={workItems ?? []} levels={levels ?? []} onClose={() => setSelectedItem(null)} onNavigate={setSelectedItem} />
            <ManageLevelsDialog projectId={projectId ?? ''} open={manageLevelsOpen} onOpenChange={setManageLevelsOpen} />
        </div>
    )
}
