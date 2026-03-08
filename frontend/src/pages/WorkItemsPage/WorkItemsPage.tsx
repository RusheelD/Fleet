import { useState, useMemo, useCallback, useEffect } from 'react'
import {
    makeStyles,
    mergeClasses,
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
    Dropdown,
    Option,
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
import { useWorkItems, useWorkItemLevels, useUpdateWorkItem, useBulkUpdateWorkItems } from '../../proxies'
import { useCurrentProject, usePreferences } from '../../hooks'
import type { WorkItem, WorkItemLevel, WorkItemState } from '../../models'
import type { UpdateWorkItemRequest } from '../../proxies'
import {
    DEFAULT_WORK_ITEM_COLUMN_WIDTHS,
    WORK_ITEM_TABLE_COLUMNS,
    type WorkItemTableColumnKey,
} from './workItemTableColumns'

const BOARD_STATES = ['New', 'Active', 'In Progress', 'In PR', 'Resolved', 'Closed']

function getBoardColumns(items: WorkItem[]) {
    return BOARD_STATES.map((state) => ({
        state,
        items: items.filter((item) => {
            if (state === 'In Progress') {
                return item.state === 'Planning (AI)' || item.state === 'In Progress' || item.state === 'In Progress (AI)'
            }
            if (state === 'In PR') return item.state === 'In-PR' || item.state === 'In-PR (AI)'
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
        backgroundColor: tokens.colorNeutralBackground3,
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
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
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
        gap: tokens.spacingHorizontalS,
        flex: 1,
        minWidth: '200px',
        flexWrap: 'wrap',
    },
    bulkActions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    bulkLabel: {
        color: tokens.colorNeutralForeground2,
        whiteSpace: 'nowrap',
    },
    bulkStateDropdown: {
        minWidth: '170px',
    },
    bulkAssigneeInput: {
        width: '210px',
    },
    searchInput: {
        maxWidth: '280px',
        minWidth: '210px',
        flex: 1,
    },
    boardContainer: {
        flex: 1,
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        overflow: 'auto',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalM,
    },
    boardContainerCompact: {
        gap: tokens.spacingHorizontalS,
        paddingTop: 0,
        paddingBottom: tokens.spacingVerticalS,
    },
    filterSurface: {
        padding: '0.875rem',
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.5rem',
        minWidth: '200px',
        borderRadius: tokens.borderRadiusLarge,
    },
    filterSection: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.25rem',
    },
    columnSurface: {
        padding: '0.75rem',
        display: 'flex',
        flexDirection: 'column' as const,
        gap: '0.5rem',
        minWidth: '220px',
        borderRadius: tokens.borderRadiusLarge,
    },
    columnHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    filterHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    filterTriggerButton: {
        minWidth: 'unset',
        width: '28px',
        height: '28px',
        paddingTop: 0,
        paddingBottom: 0,
        paddingLeft: 0,
        paddingRight: 0,
        borderTopStyle: 'none',
        borderRightStyle: 'none',
        borderBottomStyle: 'none',
        borderLeftStyle: 'none',
        backgroundColor: 'transparent',
        boxShadow: 'none',
        color: tokens.colorNeutralForeground3,
        ':hover': {
            borderTopStyle: 'none',
            borderRightStyle: 'none',
            borderBottomStyle: 'none',
            borderLeftStyle: 'none',
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
        ':active': {
            borderTopStyle: 'none',
            borderRightStyle: 'none',
            borderBottomStyle: 'none',
            borderLeftStyle: 'none',
            backgroundColor: tokens.colorNeutralBackground1Pressed,
        },
    },
    filterTriggerActive: {
        color: tokens.colorBrandForeground1,
    },
})

const ALL_STATES: WorkItemState[] = ['New', 'Active', 'Planning (AI)', 'In Progress', 'In Progress (AI)', 'In-PR', 'In-PR (AI)', 'Resolved', 'Resolved (AI)', 'Closed']
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
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const { data: workItems, isLoading } = useWorkItems(projectId)
    const { data: levels } = useWorkItemLevels(projectId)
    const [viewMode, setViewMode] = useState<'backlog' | 'list' | 'board'>('backlog')
    const [createDialogOpen, setCreateDialogOpen] = useState(false)
    const [manageLevelsOpen, setManageLevelsOpen] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const [selectedItem, setSelectedItem] = useState<WorkItem | null>(null)
    const [filters, setFilters] = useState<WorkItemFilters>(EMPTY_FILTERS)
    const [selectedWorkItemNumbers, setSelectedWorkItemNumbers] = useState<Set<number>>(new Set())
    const [bulkState, setBulkState] = useState<WorkItemState | ''>('')
    const [bulkAssignee, setBulkAssignee] = useState('')
    const [collapsedColumns, setCollapsedColumns] = useState<Set<WorkItemTableColumnKey>>(new Set())
    const [columnWidths, setColumnWidths] =
        useState<Record<WorkItemTableColumnKey, number>>({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })

    const updateMutation = useUpdateWorkItem(projectId)
    const bulkUpdateMutation = useBulkUpdateWorkItems(projectId)

    const handleReparent = useCallback((itemId: number, newParentId: number | null) => {
        // Send 0 to clear parent (backend treats 0 as 'set to null')
        updateMutation.mutate({ workItemNumber: itemId, data: { parentWorkItemNumber: newParentId ?? 0 } })
    }, [updateMutation])

    const handleTitleChange = useCallback((itemId: number, newTitle: string) => {
        updateMutation.mutate({ workItemNumber: itemId, data: { title: newTitle } })
    }, [updateMutation])

    useEffect(() => {
        const available = new Set((workItems ?? []).map((item) => item.workItemNumber))
        setSelectedWorkItemNumbers((previous) => {
            if (previous.size === 0) {
                return previous
            }

            const next = new Set<number>()
            for (const itemNumber of previous) {
                if (available.has(itemNumber)) {
                    next.add(itemNumber)
                }
            }

            return next.size === previous.size ? previous : next
        })
    }, [workItems])

    const toggleSelection = useCallback((itemNumber: number, selected: boolean) => {
        setSelectedWorkItemNumbers((previous) => {
            const next = new Set(previous)
            if (selected) {
                next.add(itemNumber)
            } else {
                next.delete(itemNumber)
            }

            return next
        })
    }, [])

    const toggleSelectionForItems = useCallback((itemNumbers: number[], selected: boolean) => {
        setSelectedWorkItemNumbers((previous) => {
            const next = new Set(previous)
            for (const itemNumber of itemNumbers) {
                if (selected) {
                    next.add(itemNumber)
                } else {
                    next.delete(itemNumber)
                }
            }

            return next
        })
    }, [])

    const clearSelection = useCallback(() => {
        setSelectedWorkItemNumbers(new Set())
    }, [])

    const handleToggleColumnVisibility = useCallback((column: WorkItemTableColumnKey, visible: boolean) => {
        setCollapsedColumns((previous) => {
            const next = new Set(previous)
            if (visible) {
                next.delete(column)
            } else {
                const definition = WORK_ITEM_TABLE_COLUMNS.find((entry) => entry.key === column)
                if (definition?.collapsible) {
                    next.add(column)
                }
            }
            return next
        })
    }, [])

    const handleResizeColumn = useCallback((column: WorkItemTableColumnKey, width: number) => {
        setColumnWidths((previous) => {
            const nextWidth = Math.round(width)
            if (previous[column] === nextWidth) {
                return previous
            }
            return { ...previous, [column]: nextWidth }
        })
    }, [])

    const handleResetColumns = useCallback(() => {
        setCollapsedColumns(new Set())
        setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
    }, [])

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
    const selectedCount = selectedWorkItemNumbers.size
    const canApplyBulkChanges = selectedCount > 0 && (bulkState !== '' || bulkAssignee.trim().length > 0)

    const handleApplyBulkChanges = useCallback(() => {
        if (!canApplyBulkChanges) {
            return
        }

        const request: UpdateWorkItemRequest = {}
        if (bulkState) {
            request.state = bulkState
        }
        if (bulkAssignee.trim().length > 0) {
            request.assignedTo = bulkAssignee.trim()
        }

        bulkUpdateMutation.mutate(
            {
                workItemNumbers: Array.from(selectedWorkItemNumbers),
                data: request,
            },
            {
                onSuccess: () => {
                    clearSelection()
                    setBulkState('')
                    setBulkAssignee('')
                },
            },
        )
    }, [
        bulkAssignee,
        bulkState,
        bulkUpdateMutation,
        canApplyBulkChanges,
        clearSelection,
        selectedWorkItemNumbers,
    ])

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
                                    <ToolbarButton
                                        className={mergeClasses(
                                            styles.filterTriggerButton,
                                            isFiltered(filters) && styles.filterTriggerActive,
                                        )}
                                        icon={<FilterRegular />}
                                        appearance="transparent"
                                        aria-label={isFiltered(filters) ? 'Filters active' : 'Filters'}
                                        title={isFiltered(filters) ? 'Filters active' : 'Filters'}
                                    />
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
                            <Popover withArrow>
                                <PopoverTrigger disableButtonEnhancement>
                                    <ToolbarButton>Columns</ToolbarButton>
                                </PopoverTrigger>
                                <PopoverSurface className={styles.columnSurface}>
                                    <div className={styles.columnHeader}>
                                        <Text weight="semibold" size={300}>Columns</Text>
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={<DismissRegular />}
                                            onClick={handleResetColumns}
                                        >
                                            Reset
                                        </Button>
                                    </div>
                                    <Divider />
                                    {WORK_ITEM_TABLE_COLUMNS.map((column) => {
                                        const visible = !collapsedColumns.has(column.key)
                                        return (
                                            <Checkbox
                                                key={column.key}
                                                label={column.label}
                                                checked={visible}
                                                disabled={!column.collapsible}
                                                onChange={(_event, data) => {
                                                    handleToggleColumnVisibility(column.key, data.checked === true)
                                                }}
                                            />
                                        )
                                    })}
                                </PopoverSurface>
                            </Popover>
                            <ToolbarDivider />
                            <ToolbarButton onClick={() => setManageLevelsOpen(true)}>Levels</ToolbarButton>
                            {selectedCount > 0 && (
                                <>
                                    <ToolbarDivider />
                                    <div className={styles.bulkActions}>
                                        <Text size={200} className={styles.bulkLabel}>
                                            {selectedCount} selected
                                        </Text>
                                        <Dropdown
                                            className={styles.bulkStateDropdown}
                                            size="small"
                                            placeholder="State"
                                            selectedOptions={bulkState ? [bulkState] : []}
                                            onOptionSelect={(_event, data) => {
                                                setBulkState((data.optionValue as WorkItemState | undefined) ?? '')
                                            }}
                                        >
                                            <Option value="">No state change</Option>
                                            {ALL_STATES.map((state) => (
                                                <Option key={state} value={state}>{state}</Option>
                                            ))}
                                        </Dropdown>
                                        <Input
                                            className={styles.bulkAssigneeInput}
                                            size="small"
                                            appearance="outline"
                                            placeholder="Assign to..."
                                            value={bulkAssignee}
                                            onChange={(_event, data) => setBulkAssignee(data.value)}
                                        />
                                        <Button
                                            appearance="primary"
                                            size="small"
                                            disabled={!canApplyBulkChanges || bulkUpdateMutation.isPending}
                                            onClick={handleApplyBulkChanges}
                                        >
                                            Apply
                                        </Button>
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            disabled={bulkUpdateMutation.isPending}
                                            onClick={() => {
                                                clearSelection()
                                                setBulkState('')
                                                setBulkAssignee('')
                                            }}
                                        >
                                            Clear Selection
                                        </Button>
                                    </div>
                                </>
                            )}
                        </Toolbar>
                    </div>
                </div>

                {viewMode === 'backlog' && (
                    <BacklogTreeTable
                        items={items}
                        levelMap={levelMap}
                        selectedItemId={selectedItem?.workItemNumber}
                        selectedWorkItemNumbers={selectedWorkItemNumbers}
                        onItemClick={setSelectedItem}
                        onToggleSelection={toggleSelection}
                        onToggleSelectionForItems={toggleSelectionForItems}
                        onReparent={handleReparent}
                        onTitleChange={handleTitleChange}
                        columnWidths={columnWidths}
                        collapsedColumns={collapsedColumns}
                        onResizeColumn={handleResizeColumn}
                    />
                )}
                {viewMode === 'list' && (
                    <BacklogList
                        items={items}
                        levelMap={levelMap}
                        selectedItemId={selectedItem?.workItemNumber}
                        selectedWorkItemNumbers={selectedWorkItemNumbers}
                        onItemClick={setSelectedItem}
                        onToggleSelection={toggleSelection}
                        onToggleSelectionForItems={toggleSelectionForItems}
                        onTitleChange={handleTitleChange}
                        columnWidths={columnWidths}
                        collapsedColumns={collapsedColumns}
                        onResizeColumn={handleResizeColumn}
                    />
                )}
                {viewMode === 'board' && (
                    <div className={mergeClasses(styles.boardContainer, isCompact && styles.boardContainerCompact)}>
                        {boardColumns.map((col) => (
                            <KanbanColumn key={col.state} state={col.state} items={col.items} levelMap={levelMap} onItemClick={setSelectedItem} />
                        ))}
                    </div>
                )}
            </div>

            {projectId && (
                <>
                    <CreateWorkItemDialog projectId={projectId} workItems={workItems ?? []} levels={levels ?? []} open={createDialogOpen} onOpenChange={setCreateDialogOpen} />
                    <WorkItemDetailDialog projectId={projectId} item={selectedItem} workItems={workItems ?? []} levels={levels ?? []} onClose={() => setSelectedItem(null)} onNavigate={setSelectedItem} />
                    <ManageLevelsDialog projectId={projectId} open={manageLevelsOpen} onOpenChange={setManageLevelsOpen} />
                </>
            )}
        </div>
    )
}
