import { Suspense, useState, useMemo, useCallback, useEffect, useRef, type ChangeEvent, type ComponentProps } from 'react'
import {
    makeStyles,
    mergeClasses,
    Button,
    Spinner,
} from '@fluentui/react-components'
import {
    BoardRegular,
    AddRegular,
} from '@fluentui/react-icons'
import { EmptyState, PageHeader } from '../../components/shared'
import { KanbanColumn, BacklogTreeTable, BacklogList } from './'
import {
    useWorkItems,
    useWorkItemLevels,
    useUpdateWorkItem,
    useBulkUpdateWorkItems,
    useBulkDeleteWorkItems,
    useExportWorkItems,
    useImportWorkItems,
} from '../../proxies'
import { useCurrentProject, usePreferences, useIsMobile, useServerEventConnection } from '../../hooks'
import { resolveConnectionAwarePollingInterval } from '../../hooks/serverEventConnectionState'
import type { WorkItem, WorkItemState } from '../../models'
import type { UpdateWorkItemRequest } from '../../proxies'
import { appTokens } from '../../styles/appTokens'
import {
    getWorkItemAssignmentSettings,
} from './workItemAssignmentOptions'
import { collectDescendantWorkItemNumbers } from './workItemSelection'
import { WorkItemsHeaderActions } from './WorkItemsHeaderActions'
import { WorkItemsToolbar, type WorkItemsViewMode } from './WorkItemsToolbar'
import {
    EMPTY_WORK_ITEM_FILTERS,
    buildLevelMap,
    filterWorkItems,
    getBoardColumns,
    sortWorkItemLevels,
    type WorkItemFilters,
} from './workItemFilters'
import { useWorkItemColumnPreferences } from './useWorkItemColumnPreferences'
import { lazyDialog } from '../../utils/staleChunkRecovery'

type CreateWorkItemDialogProps = ComponentProps<typeof import('./CreateWorkItemDialog').CreateWorkItemDialog>
type WorkItemDetailDialogProps = ComponentProps<typeof import('./WorkItemDetailDialog').WorkItemDetailDialog>
type ManageLevelsDialogProps = ComponentProps<typeof import('./ManageLevelsDialog').ManageLevelsDialog>

const CreateWorkItemDialog = lazyDialog<CreateWorkItemDialogProps>(() => import('./CreateWorkItemDialog').then((module) => ({ default: module.CreateWorkItemDialog })))
const WorkItemDetailDialog = lazyDialog<WorkItemDetailDialogProps>(() => import('./WorkItemDetailDialog').then((module) => ({ default: module.WorkItemDetailDialog })))
const ManageLevelsDialog = lazyDialog<ManageLevelsDialogProps>(() => import('./ManageLevelsDialog').then((module) => ({ default: module.ManageLevelsDialog })))

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

const WORK_ITEMS_FALLBACK_POLL_MS = 15000

export function WorkItemsPage() {
    const styles = useStyles()
    const { projectId } = useCurrentProject()
    const { preferences } = usePreferences()
    const { state: serverEventState } = useServerEventConnection(projectId)
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const workItemsPollingInterval = resolveConnectionAwarePollingInterval(
        serverEventState,
        WORK_ITEMS_FALLBACK_POLL_MS,
        WORK_ITEMS_FALLBACK_POLL_MS,
    )
    const { data: workItems, isLoading } = useWorkItems(projectId, {
        pollingInterval: workItemsPollingInterval,
    })
    const { data: levels } = useWorkItemLevels(projectId)
    const [viewMode, setViewMode] = useState<WorkItemsViewMode>('backlog')
    const [createDialogOpen, setCreateDialogOpen] = useState(false)
    const [manageLevelsOpen, setManageLevelsOpen] = useState(false)
    const [searchQuery, setSearchQuery] = useState('')
    const [selectedItem, setSelectedItem] = useState<WorkItem | null>(null)
    const [filters, setFilters] = useState<WorkItemFilters>(EMPTY_WORK_ITEM_FILTERS)
    const [selectedWorkItemNumbers, setSelectedWorkItemNumbers] = useState<Set<number>>(new Set())
    const [bulkState, setBulkState] = useState<WorkItemState | ''>('')
    const [bulkAssignmentLabel, setBulkAssignmentLabel] = useState('')
    const {
        collapsedColumns,
        columnWidths,
        toggleColumnVisibility: handleToggleColumnVisibility,
        resizeColumn: handleResizeColumn,
        resetColumns: handleResetColumns,
    } = useWorkItemColumnPreferences(projectId)

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

    const levelMap = useMemo(() => buildLevelMap(levels), [levels])
    const sortedLevels = useMemo(() => sortWorkItemLevels(levels), [levels])
    const items = useMemo(() => filterWorkItems(workItems ?? [], filters, searchQuery), [workItems, searchQuery, filters])
    const totalWorkItemCount = workItems?.length ?? 0

    const boardColumns = useMemo(() => getBoardColumns(items), [items])
    const selectedCount = selectedWorkItemNumbers.size
    const canApplyBulkChanges = selectedCount > 0 && (bulkState !== '' || bulkAssignmentLabel !== '')
    const descendantSelection = useMemo(
        () => collectDescendantWorkItemNumbers(workItems ?? [], selectedWorkItemNumbers),
        [selectedWorkItemNumbers, workItems],
    )

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

        // Sort deepest-first so children are deleted before parents,
        // avoiding FK constraint failures.
        const depthOf = (num: number): number => {
            const item = workItems?.find(w => w.workItemNumber === num)
            if (!item?.parentWorkItemNumber) return 0
            return 1 + depthOf(item.parentWorkItemNumber)
        }
        const sorted = [...selectedItems].sort((a, b) => depthOf(b) - depthOf(a))

        bulkDeleteMutation.mutate(sorted, {
            onSuccess: () => {
                clearSelection()
                setBulkState('')
                setBulkAssignmentLabel('')
                if (selectedItem && selectedWorkItemNumbers.has(selectedItem.workItemNumber)) {
                    setSelectedItem(null)
                }
            },
        })
    }, [bulkDeleteMutation, clearSelection, selectedItem, selectedWorkItemNumbers, workItems])

    const handleSelectDescendants = useCallback(() => {
        if (descendantSelection.length === 0) {
            return
        }

        toggleSelectionForItems(descendantSelection, true)
    }, [descendantSelection, toggleSelectionForItems])

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
                    subtitle="Shape the backlog, filter what matters, and keep execution-ready work easy to act on."
                    actions={
                        <WorkItemsHeaderActions
                            isMobile={isMobile}
                            importFileInputRef={importFileInputRef}
                            onImportFileChange={handleImportFileChange}
                            onImportClick={handleImportClick}
                            onExport={() => void handleExportWorkItems()}
                            onCreate={() => setCreateDialogOpen(true)}
                            importPending={importWorkItemsMutation.isPending}
                            exportPending={exportWorkItemsMutation.isPending}
                            canExport={Boolean(projectId)}
                        />
                    }
                />
                <WorkItemsToolbar
                    isMobile={isMobile}
                    viewMode={viewMode}
                    searchQuery={searchQuery}
                    filters={filters}
                    levels={sortedLevels}
                    collapsedColumns={collapsedColumns}
                    selectedCount={selectedCount}
                    bulkState={bulkState}
                    bulkAssignmentLabel={bulkAssignmentLabel}
                    descendantSelectionCount={descendantSelection.length}
                    canApplyBulkChanges={canApplyBulkChanges}
                    isMutatingBulkActions={bulkDeleteMutation.isPending || bulkUpdateMutation.isPending}
                    onViewModeChange={setViewMode}
                    onSearchQueryChange={setSearchQuery}
                    onFiltersChange={setFilters}
                    onToggleColumnVisibility={handleToggleColumnVisibility}
                    onResetColumns={handleResetColumns}
                    onManageLevels={() => setManageLevelsOpen(true)}
                    onBulkStateChange={setBulkState}
                    onBulkAssignmentChange={setBulkAssignmentLabel}
                    onSelectDescendants={handleSelectDescendants}
                    onApplyBulkChanges={handleApplyBulkChanges}
                    onDeleteSelected={handleDeleteSelected}
                    onClearSelection={() => {
                        clearSelection()
                        setBulkState('')
                        setBulkAssignmentLabel('')
                    }}
                />

                {items.length === 0 ? (
                    <EmptyState
                        icon={<BoardRegular style={{ fontSize: '48px' }} />}
                        title={totalWorkItemCount === 0 ? 'No work items yet' : 'No work items match this view'}
                        description={totalWorkItemCount === 0
                            ? 'Create the first item or generate backlog from chat to start shaping execution.'
                            : 'Clear the active search or filters to bring matching work back into view.'}
                        actions={totalWorkItemCount === 0 ? (
                            <Button appearance="primary" icon={<AddRegular />} onClick={() => setCreateDialogOpen(true)}>
                                New Work Item
                            </Button>
                        ) : (
                            <Button
                                appearance="secondary"
                                onClick={() => {
                                    setSearchQuery('')
                                    setFilters(EMPTY_WORK_ITEM_FILTERS)
                                }}
                            >
                                Clear Filters
                            </Button>
                        )}
                    />
                ) : viewMode === 'backlog' && (
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
                    <Suspense fallback={null}>
                        {createDialogOpen ? (
                            <CreateWorkItemDialog projectId={projectId} workItems={workItems ?? []} levels={levels ?? []} open={createDialogOpen} onOpenChange={setCreateDialogOpen} />
                        ) : null}
                    </Suspense>
                    <Suspense fallback={null}>
                        {selectedItem ? (
                            <WorkItemDetailDialog projectId={projectId} item={selectedItem} workItems={workItems ?? []} levels={levels ?? []} onClose={() => setSelectedItem(null)} onNavigate={setSelectedItem} />
                        ) : null}
                    </Suspense>
                    <Suspense fallback={null}>
                        {manageLevelsOpen ? (
                            <ManageLevelsDialog projectId={projectId} open={manageLevelsOpen} onOpenChange={setManageLevelsOpen} />
                        ) : null}
                    </Suspense>
                </>
            )}
        </div>
    )
}
