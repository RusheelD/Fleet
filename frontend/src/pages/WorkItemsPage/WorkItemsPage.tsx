import { useState, useMemo, useCallback, useEffect, useRef, type ChangeEvent } from 'react'
import {
    makeStyles,
    mergeClasses,
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
    ArrowUploadRegular,
    ArrowDownloadRegular,
    DeleteRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { KanbanColumn, BacklogTreeTable, BacklogList, CreateWorkItemDialog, WorkItemDetailDialog, ManageLevelsDialog } from './'
import {
    useWorkItems,
    useWorkItemLevels,
    useUpdateWorkItem,
    useBulkUpdateWorkItems,
    useBulkDeleteWorkItems,
    useExportWorkItems,
    useImportWorkItems,
} from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile } from '../../hooks'
import type { WorkItem, WorkItemLevel, WorkItemState } from '../../models'
import type { UpdateWorkItemRequest } from '../../proxies'
import {
    DEFAULT_WORK_ITEM_COLUMN_WIDTHS,
    MIN_WORK_ITEM_COLUMN_WIDTHS,
    WORK_ITEM_TABLE_COLUMNS,
    type WorkItemTableColumnKey,
} from './workItemTableColumns'
import { appTokens } from '../../styles/appTokens'
import {
    WORK_ITEM_ASSIGNMENT_OPTION_LABELS,
    getWorkItemAssignmentSettings,
} from './workItemAssignmentOptions'

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
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.pageX,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.pageX,
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        overflow: 'hidden',
        minWidth: 0,
        minHeight: 0,
        backgroundColor: appTokens.color.pageBackground,
    },
    pageMobile: {
        paddingTop: appTokens.space.pageYMobile,
        paddingBottom: appTokens.space.pageYMobile,
        paddingLeft: appTokens.space.pageXMobile,
        paddingRight: appTokens.space.pageXMobile,
    },
    headerActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        alignItems: 'center',
        flexWrap: 'wrap',
    },
    headerActionsMobile: {
        width: '100%',
    },
    headerActionButtonMobile: {
        flex: '1 1 140px',
    },
    toolbarRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: appTokens.space.md,
        gap: appTokens.space.md,
        flexWrap: 'wrap',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.lg,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
    },
    toolbarRowMobile: {
        marginBottom: appTokens.space.sm,
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.xxxs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.xs,
        paddingRight: appTokens.space.xs,
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    toolbarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flex: 1,
        minWidth: '200px',
        flexWrap: 'wrap',
    },
    toolbarLeftMobile: {
        minWidth: 0,
        width: '100%',
        flexDirection: 'column',
        alignItems: 'stretch',
    },
    viewTabsMobile: {
        width: '100%',
        overflowX: 'auto',
        whiteSpace: 'nowrap',
        paddingBottom: appTokens.space.xxxs,
    },
    bulkActions: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    bulkActionsMobile: {
        width: '100%',
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
    },
    inlineToolbar: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    inlineToolbarMobile: {
        width: '100%',
        rowGap: appTokens.space.xs,
        columnGap: appTokens.space.xs,
    },
    bulkLabel: {
        color: appTokens.color.textSecondary,
        whiteSpace: 'nowrap',
    },
    bulkStateDropdown: {
        minWidth: '170px',
    },
    bulkStateDropdownMobile: {
        minWidth: '140px',
        flex: '1 1 140px',
    },
    bulkAssignmentDropdown: {
        minWidth: '210px',
    },
    bulkAssignmentDropdownMobile: {
        minWidth: '160px',
        flex: '1 1 180px',
    },
    searchInput: {
        maxWidth: '280px',
        minWidth: '210px',
        flex: 1,
    },
    searchInputMobile: {
        maxWidth: 'unset',
        minWidth: '140px',
        width: '100%',
        flex: '1 1 100%',
    },
    boardContainer: {
        flex: 1,
        display: 'flex',
        gap: appTokens.space.md,
        overflow: 'auto',
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.md,
    },
    boardContainerCompact: {
        gap: appTokens.space.sm,
        paddingTop: 0,
        paddingBottom: appTokens.space.sm,
    },
    boardContainerMobile: {
        flexDirection: 'column',
        overflowX: 'hidden',
        overflowY: 'auto',
        gap: appTokens.space.sm,
    },
    filterSurface: {
        padding: '0.875rem',
        display: 'flex',
        flexDirection: 'column' as const,
        gap: appTokens.space.sm,
        minWidth: '200px',
        borderRadius: appTokens.radius.lg,
    },
    filterSurfaceMobile: {
        minWidth: 'min(260px, calc(100vw - 2rem))',
        maxWidth: 'calc(100vw - 2rem)',
    },
    filterSection: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: appTokens.space.xxs,
    },
    columnSurface: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column' as const,
        gap: appTokens.space.sm,
        minWidth: '220px',
        borderRadius: appTokens.radius.lg,
    },
    columnSurfaceMobile: {
        minWidth: 'min(280px, calc(100vw - 2rem))',
        maxWidth: 'calc(100vw - 2rem)',
    },
    columnHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    columnHint: {
        color: appTokens.color.textTertiary,
        fontSize: appTokens.fontSize.sm,
        lineHeight: appTokens.lineHeight.snug,
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
        color: appTokens.color.textTertiary,
        ':hover': {
            borderTopStyle: 'none',
            borderRightStyle: 'none',
            borderBottomStyle: 'none',
            borderLeftStyle: 'none',
            backgroundColor: appTokens.color.surfaceHover,
        },
        ':active': {
            borderTopStyle: 'none',
            borderRightStyle: 'none',
            borderBottomStyle: 'none',
            borderLeftStyle: 'none',
            backgroundColor: appTokens.color.surfacePressed,
        },
    },
    filterTriggerActive: {
        color: appTokens.color.brand,
    },
})

const ALL_STATES: WorkItemState[] = ['New', 'Active', 'Planning (AI)', 'In Progress', 'In Progress (AI)', 'In-PR', 'In-PR (AI)', 'Resolved', 'Resolved (AI)', 'Closed']
const ALL_PRIORITIES = [1, 2, 3, 4] as const
const COLUMN_PREFS_STORAGE_PREFIX = 'fleet.work-items.columns.v1'

interface StoredColumnPreferences {
    collapsedColumns?: WorkItemTableColumnKey[]
    columnWidths?: Partial<Record<WorkItemTableColumnKey, number>>
}

function getColumnPrefsStorageKey(projectId?: string): string {
    return `${COLUMN_PREFS_STORAGE_PREFIX}:${projectId ?? 'global'}`
}

function sanitizeCollapsedColumns(
    value: unknown,
): Set<WorkItemTableColumnKey> {
    if (!Array.isArray(value)) {
        return new Set()
    }

    const allowed = new Set(WORK_ITEM_TABLE_COLUMNS.map((column) => column.key))
    const next = new Set<WorkItemTableColumnKey>()
    for (const item of value) {
        if (typeof item !== 'string') {
            continue
        }

        if (allowed.has(item as WorkItemTableColumnKey)) {
            next.add(item as WorkItemTableColumnKey)
        }
    }

    return next
}

function sanitizeColumnWidths(
    value: unknown,
): Record<WorkItemTableColumnKey, number> {
    const next = { ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS }
    if (!value || typeof value !== 'object') {
        return next
    }

    for (const column of WORK_ITEM_TABLE_COLUMNS) {
        const key = column.key
        const rawWidth = (value as Partial<Record<WorkItemTableColumnKey, unknown>>)[key]
        if (typeof rawWidth !== 'number' || Number.isNaN(rawWidth) || !Number.isFinite(rawWidth)) {
            continue
        }

        next[key] = Math.max(
            MIN_WORK_ITEM_COLUMN_WIDTHS[key],
            Math.round(rawWidth),
        )
    }

    return next
}

interface WorkItemFilters {
    states: Set<WorkItemState>
    priorities: Set<number>
    levelKeys: Set<string>
    aiOnly: boolean | null
}

const NO_TYPE_FILTER_KEY = '__none__'
const EMPTY_FILTERS: WorkItemFilters = { states: new Set(), priorities: new Set(), levelKeys: new Set(), aiOnly: null }

function isFiltered(filters: WorkItemFilters): boolean {
    return filters.states.size > 0 || filters.priorities.size > 0 || filters.levelKeys.size > 0 || filters.aiOnly !== null
}

export function WorkItemsPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
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
    const [bulkAssignmentLabel, setBulkAssignmentLabel] = useState('')
    const [collapsedColumns, setCollapsedColumns] = useState<Set<WorkItemTableColumnKey>>(() => new Set())
    const [columnWidths, setColumnWidths] = useState<Record<WorkItemTableColumnKey, number>>(
        () => ({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS }),
    )

    const updateMutation = useUpdateWorkItem(projectId)
    const bulkUpdateMutation = useBulkUpdateWorkItems(projectId)
    const bulkDeleteMutation = useBulkDeleteWorkItems(projectId)
    const exportWorkItemsMutation = useExportWorkItems(projectId)
    const importWorkItemsMutation = useImportWorkItems(projectId)
    const importFileInputRef = useRef<HTMLInputElement | null>(null)

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

    useEffect(() => {
        if (typeof window === 'undefined') {
            return
        }

        try {
            const raw = window.localStorage.getItem(getColumnPrefsStorageKey(projectId))
            if (!raw) {
                setCollapsedColumns(new Set())
                setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
                return
            }

            const parsed = JSON.parse(raw) as StoredColumnPreferences
            setCollapsedColumns(sanitizeCollapsedColumns(parsed.collapsedColumns))
            setColumnWidths(sanitizeColumnWidths(parsed.columnWidths))
        } catch {
            setCollapsedColumns(new Set())
            setColumnWidths({ ...DEFAULT_WORK_ITEM_COLUMN_WIDTHS })
        }
    }, [projectId])

    useEffect(() => {
        if (typeof window === 'undefined') {
            return
        }

        const payload: StoredColumnPreferences = {
            collapsedColumns: Array.from(collapsedColumns),
            columnWidths,
        }

        try {
            window.localStorage.setItem(getColumnPrefsStorageKey(projectId), JSON.stringify(payload))
        } catch {
            // Ignore storage errors (for example private mode quota restrictions).
        }
    }, [collapsedColumns, columnWidths, projectId])

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
            const nextWidth = Math.max(
                MIN_WORK_ITEM_COLUMN_WIDTHS[column],
                Math.round(width),
            )
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

    const handleExportWorkItems = useCallback(async () => {
        try {
            const blob = await exportWorkItemsMutation.mutateAsync()
            const downloadUrl = URL.createObjectURL(blob)
            const anchor = document.createElement('a')
            anchor.href = downloadUrl
            anchor.download = `fleet-work-items-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
            document.body.appendChild(anchor)
            anchor.click()
            anchor.remove()
            URL.revokeObjectURL(downloadUrl)
        } catch {
            // Ignore: errors are surfaced by API handlers.
        }
    }, [exportWorkItemsMutation])

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
            await importWorkItemsMutation.mutateAsync(payload)
        } catch {
            // Ignore: invalid JSON or API errors are surfaced by API handlers.
        } finally {
            event.target.value = ''
        }
    }, [importWorkItemsMutation])

    const levelMap = useMemo(() => {
        const map = new Map<number, WorkItemLevel>()
        for (const level of levels ?? []) {
            map.set(level.id, level)
        }
        return map
    }, [levels])
    const sortedLevels = useMemo(
        () => [...(levels ?? [])].sort((a, b) => a.ordinal - b.ordinal || a.name.localeCompare(b.name)),
        [levels],
    )

    const items = useMemo(() => {
        let all = workItems ?? []

        // Apply filters
        if (filters.states.size > 0) {
            all = all.filter((wi) => filters.states.has(wi.state))
        }
        if (filters.priorities.size > 0) {
            all = all.filter((wi) => filters.priorities.has(wi.priority))
        }
        if (filters.levelKeys.size > 0) {
            all = all.filter((wi) => {
                const key = wi.levelId == null ? NO_TYPE_FILTER_KEY : wi.levelId.toString()
                return filters.levelKeys.has(key)
            })
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
    const canApplyBulkChanges = selectedCount > 0 && (bulkState !== '' || bulkAssignmentLabel !== '')

    const handleApplyBulkChanges = useCallback(() => {
        if (!canApplyBulkChanges) {
            return
        }

        const request: UpdateWorkItemRequest = {}
        if (bulkState) {
            request.state = bulkState
        }
        if (bulkAssignmentLabel) {
            const assignmentSettings = getWorkItemAssignmentSettings(bulkAssignmentLabel)
            request.assignedTo = assignmentSettings.assignedTo
            request.isAI = assignmentSettings.isAI
            request.assignmentMode = assignmentSettings.assignmentMode
            request.assignedAgentCount = assignmentSettings.assignedAgentCount
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
                    setBulkAssignmentLabel('')
                },
            },
        )
    }, [
        bulkAssignmentLabel,
        bulkState,
        bulkUpdateMutation,
        canApplyBulkChanges,
        clearSelection,
        selectedWorkItemNumbers,
    ])

    const handleDeleteSelected = useCallback(() => {
        const selectedItems = Array.from(selectedWorkItemNumbers)
        if (selectedItems.length === 0) {
            return
        }

        const confirmed = window.confirm(
            selectedItems.length === 1
                ? `Delete work item #${selectedItems[0]}?`
                : `Delete ${selectedItems.length} selected work items?`,
        )

        if (!confirmed) {
            return
        }

        bulkDeleteMutation.mutate(selectedItems, {
            onSuccess: () => {
                clearSelection()
                setBulkState('')
                setBulkAssignmentLabel('')
                if (selectedItem && selectedWorkItemNumbers.has(selectedItem.workItemNumber)) {
                    setSelectedItem(null)
                }
            },
        })
    }, [bulkDeleteMutation, clearSelection, selectedItem, selectedWorkItemNumbers])

    if (isLoading) {
        return (
            <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
                <Spinner label="Loading work items..." />
            </div>
        )
    }

    return (
        <div className={styles.root}>
            <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
                <PageHeader
                    title="Work Items"
                    subtitle="Manage your backlog, track progress, and assign agents"
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
                                disabled={importWorkItemsMutation.isPending}
                                className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                            >
                                Import
                            </Button>
                            <Button
                                appearance="secondary"
                                icon={<ArrowDownloadRegular />}
                                onClick={() => void handleExportWorkItems()}
                                disabled={exportWorkItemsMutation.isPending || !projectId}
                                className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                            >
                                Export
                            </Button>
                            <Button
                                appearance="primary"
                                icon={<AddRegular />}
                                onClick={() => setCreateDialogOpen(true)}
                                className={mergeClasses(isMobile && styles.headerActionButtonMobile)}
                            >
                                New Work Item
                            </Button>
                        </div>
                    }
                />

                <div className={mergeClasses(styles.toolbarRow, isMobile && styles.toolbarRowMobile)}>
                    <div className={mergeClasses(styles.toolbarLeft, isMobile && styles.toolbarLeftMobile)}>
                        <TabList
                            className={mergeClasses(isMobile && styles.viewTabsMobile)}
                            selectedValue={viewMode}
                            onTabSelect={(_e, data) => setViewMode(data.value as 'backlog' | 'list' | 'board')}
                            size="small"
                        >
                            <Tab value="backlog" icon={<TextBulletListTreeRegular />}>Backlog</Tab>
                            <Tab value="list" icon={<TextBulletListLtrRegular />}>List</Tab>
                            <Tab value="board" icon={<BoardRegular />}>Board</Tab>
                        </TabList>
                        <Toolbar className={mergeClasses(styles.inlineToolbar, isMobile && styles.inlineToolbarMobile)}>
                            <ToolbarDivider />
                            <Input
                                className={mergeClasses(styles.searchInput, isMobile && styles.searchInputMobile)}
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
                                <PopoverSurface className={mergeClasses(styles.filterSurface, isMobile && styles.filterSurfaceMobile)}>
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
                                        <Text size={200} weight="semibold">Type</Text>
                                        <Checkbox
                                            label="No type"
                                            size="medium"
                                            checked={filters.levelKeys.has(NO_TYPE_FILTER_KEY)}
                                            onChange={(_e, data) => {
                                                setFilters((prev) => {
                                                    const next = new Set(prev.levelKeys)
                                                    if (data.checked) next.add(NO_TYPE_FILTER_KEY); else next.delete(NO_TYPE_FILTER_KEY)
                                                    return { ...prev, levelKeys: next }
                                                })
                                            }}
                                        />
                                        {sortedLevels.map((level) => {
                                            const key = level.id.toString()
                                            return (
                                                <Checkbox
                                                    key={level.id}
                                                    label={level.name}
                                                    size="medium"
                                                    checked={filters.levelKeys.has(key)}
                                                    onChange={(_e, data) => {
                                                        setFilters((prev) => {
                                                            const next = new Set(prev.levelKeys)
                                                            if (data.checked) next.add(key); else next.delete(key)
                                                            return { ...prev, levelKeys: next }
                                                        })
                                                    }}
                                                />
                                            )
                                        })}
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
                                <PopoverSurface className={mergeClasses(styles.columnSurface, isMobile && styles.columnSurfaceMobile)}>
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
                                    <Text className={styles.columnHint}>
                                        Toggle visibility here. Drag table header separators to resize.
                                    </Text>
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
                                    <div className={mergeClasses(styles.bulkActions, isMobile && styles.bulkActionsMobile)}>
                                        <Text size={200} className={styles.bulkLabel}>
                                            {selectedCount} selected
                                        </Text>
                                        <Dropdown
                                            className={mergeClasses(styles.bulkStateDropdown, isMobile && styles.bulkStateDropdownMobile)}
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
                                        <Dropdown
                                            className={mergeClasses(styles.bulkAssignmentDropdown, isMobile && styles.bulkAssignmentDropdownMobile)}
                                            size="small"
                                            placeholder="Assignment"
                                            selectedOptions={bulkAssignmentLabel ? [bulkAssignmentLabel] : []}
                                            value={bulkAssignmentLabel}
                                            onOptionSelect={(_event, data) => {
                                                setBulkAssignmentLabel((data.optionValue as string | undefined) ?? '')
                                            }}
                                        >
                                            <Option value="">No assignment change</Option>
                                            {WORK_ITEM_ASSIGNMENT_OPTION_LABELS.map((label) => (
                                                <Option key={label} value={label}>{label}</Option>
                                            ))}
                                        </Dropdown>
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
                                            icon={<DeleteRegular />}
                                            disabled={bulkDeleteMutation.isPending || bulkUpdateMutation.isPending}
                                            onClick={handleDeleteSelected}
                                        >
                                            Delete Selected
                                        </Button>
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            disabled={bulkDeleteMutation.isPending || bulkUpdateMutation.isPending}
                                            onClick={() => {
                                                clearSelection()
                                                setBulkState('')
                                                setBulkAssignmentLabel('')
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
                    <div className={mergeClasses(styles.boardContainer, isDense && styles.boardContainerCompact, isMobile && styles.boardContainerMobile)}>
                        {boardColumns.map((col) => (
                            <KanbanColumn key={col.state} state={col.state} items={col.items} levelMap={levelMap} onItemClick={setSelectedItem} mobile={isMobile} />
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
