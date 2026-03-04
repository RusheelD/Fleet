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
    Popover,
    PopoverTrigger,
    PopoverSurface,
    Checkbox,
    Text,
    Divider,
} from '@fluentui/react-components'
import {
    SearchRegular,
    FilterRegular,
    BoardRegular,
    TextBulletListLtrRegular,
    TextBulletListTreeRegular,
    AddRegular,
    DismissRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { KanbanColumn, BacklogTreeTable, BacklogList, CreateWorkItemDialog, WorkItemDetailDialog, ManageLevelsDialog } from './'
import { useWorkItems, useWorkItemLevels, useUpdateWorkItem } from '../../proxies'
import { useCurrentProject } from '../../hooks'
import type { WorkItem, WorkItemLevel, WorkItemState } from '../../models'

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
    filterSurface: {
        padding: '0.75rem',
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.5rem',
        minWidth: '200px',
    },
    filterSection: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.25rem',
    },
    filterHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
})

const ALL_STATES: WorkItemState[] = ['New', 'Active', 'In Progress', 'In Progress (AI)', 'Resolved', 'Resolved (AI)', 'Closed']
const ALL_PRIORITIES = [1, 2, 3, 4] as const

interface WorkItemFilters {
    states: Set<WorkItemState>
    priorities: Set<number>
    aiOnly: boolean | null
}

const EMPTY_FILTERS: WorkItemFilters = { states: new Set(), priorities: new Set(), aiOnly: null }

function isFiltered(filters: WorkItemFilters): boolean {
    return filters.states.size > 0 || filters.priorities.size > 0 || filters.aiOnly !== null
}

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
    const [filters, setFilters] = useState<WorkItemFilters>(EMPTY_FILTERS)

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
        let all = workItems ?? []

        // Apply filters
        if (filters.states.size > 0) {
            all = all.filter((wi) => filters.states.has(wi.state))
        }
        if (filters.priorities.size > 0) {
            all = all.filter((wi) => filters.priorities.has(wi.priority))
        }
        if (filters.aiOnly === true) {
            all = all.filter((wi) => wi.isAI)
        } else if (filters.aiOnly === false) {
            all = all.filter((wi) => !wi.isAI)
        }

        // Apply text search
        if (searchQuery) {
            const q = searchQuery.toLowerCase()
            all = all.filter(
                (wi) =>
                    wi.title.toLowerCase().includes(q) ||
                    wi.description.toLowerCase().includes(q) ||
                    wi.assignedTo.toLowerCase().includes(q) ||
                    wi.tags.some((t) => t.toLowerCase().includes(q)),
            )
        }

        return all
    }, [workItems, searchQuery, filters])

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
                            <Popover withArrow>
                                <PopoverTrigger disableButtonEnhancement>
                                    <ToolbarButton icon={<FilterRegular />}
                                        appearance={isFiltered(filters) ? 'primary' : undefined}
                                    >
                                        Filter{isFiltered(filters) ? ' (active)' : ''}
                                    </ToolbarButton>
                                </PopoverTrigger>
                                <PopoverSurface className={styles.filterSurface}>
                                    <div className={styles.filterHeader}>
                                        <Text weight="semibold" size={300}>Filters</Text>
                                        {isFiltered(filters) && (
                                            <Button
                                                appearance="subtle"
                                                size="small"
                                                icon={<DismissRegular />}
                                                onClick={() => setFilters(EMPTY_FILTERS)}
                                            >
                                                Clear
                                            </Button>
                                        )}
                                    </div>
                                    <Divider />
                                    <div className={styles.filterSection}>
                                        <Text size={200} weight="semibold">State</Text>
                                        {ALL_STATES.map((state) => (
                                            <Checkbox
                                                key={state}
                                                label={state}
                                                size="medium"
                                                checked={filters.states.has(state)}
                                                onChange={(_e, data) => {
                                                    setFilters((prev) => {
                                                        const next = new Set(prev.states)
                                                        if (data.checked) next.add(state); else next.delete(state)
                                                        return { ...prev, states: next }
                                                    })
                                                }}
                                            />
                                        ))}
                                    </div>
                                    <Divider />
                                    <div className={styles.filterSection}>
                                        <Text size={200} weight="semibold">Priority</Text>
                                        {ALL_PRIORITIES.map((p) => (
                                            <Checkbox
                                                key={p}
                                                label={`Priority ${p}`}
                                                size="medium"
                                                checked={filters.priorities.has(p)}
                                                onChange={(_e, data) => {
                                                    setFilters((prev) => {
                                                        const next = new Set(prev.priorities)
                                                        if (data.checked) next.add(p); else next.delete(p)
                                                        return { ...prev, priorities: next }
                                                    })
                                                }}
                                            />
                                        ))}
                                    </div>
                                    <Divider />
                                    <div className={styles.filterSection}>
                                        <Text size={200} weight="semibold">AI</Text>
                                        <Checkbox
                                            label="AI-assigned only"
                                            size="medium"
                                            checked={filters.aiOnly === true}
                                            onChange={(_e, data) => setFilters((prev) => ({ ...prev, aiOnly: data.checked ? true : null }))}
                                        />
                                        <Checkbox
                                            label="Human-assigned only"
                                            size="medium"
                                            checked={filters.aiOnly === false}
                                            onChange={(_e, data) => setFilters((prev) => ({ ...prev, aiOnly: data.checked ? false : null }))}
                                        />
                                    </div>
                                </PopoverSurface>
                            </Popover>
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
