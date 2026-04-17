import { Input, Tab, TabList, Toolbar, ToolbarDivider, ToolbarButton, makeStyles, mergeClasses } from '@fluentui/react-components'
import { BoardRegular, SearchRegular, TextBulletListLtrRegular, TextBulletListTreeRegular } from '@fluentui/react-icons'
import type { WorkItemLevel, WorkItemState } from '../../models'
import { appTokens } from '../../styles/appTokens'
import type { WorkItemTableColumnKey } from './workItemTableColumns'
import type { WorkItemFilters } from './workItemFilters'
import { WorkItemColumnsPopover } from './WorkItemColumnsPopover'
import { WorkItemFiltersPopover } from './WorkItemFiltersPopover'
import { WorkItemsBulkActions } from './WorkItemsBulkActions'

export type WorkItemsViewMode = 'backlog' | 'list' | 'board'

const useStyles = makeStyles({
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
})

interface WorkItemsToolbarProps {
    isMobile: boolean
    viewMode: WorkItemsViewMode
    searchQuery: string
    filters: WorkItemFilters
    levels: WorkItemLevel[]
    collapsedColumns: ReadonlySet<WorkItemTableColumnKey>
    selectedCount: number
    bulkState: WorkItemState | ''
    bulkAssignmentLabel: string
    descendantSelectionCount: number
    canApplyBulkChanges: boolean
    isMutatingBulkActions: boolean
    onViewModeChange: (mode: WorkItemsViewMode) => void
    onSearchQueryChange: (value: string) => void
    onFiltersChange: (filters: WorkItemFilters) => void
    onToggleColumnVisibility: (column: WorkItemTableColumnKey, visible: boolean) => void
    onResetColumns: () => void
    onManageLevels: () => void
    onBulkStateChange: (value: WorkItemState | '') => void
    onBulkAssignmentChange: (value: string) => void
    onSelectDescendants: () => void
    onApplyBulkChanges: () => void
    onDeleteSelected: () => void
    onClearSelection: () => void
}

export function WorkItemsToolbar({
    isMobile,
    viewMode,
    searchQuery,
    filters,
    levels,
    collapsedColumns,
    selectedCount,
    bulkState,
    bulkAssignmentLabel,
    descendantSelectionCount,
    canApplyBulkChanges,
    isMutatingBulkActions,
    onViewModeChange,
    onSearchQueryChange,
    onFiltersChange,
    onToggleColumnVisibility,
    onResetColumns,
    onManageLevels,
    onBulkStateChange,
    onBulkAssignmentChange,
    onSelectDescendants,
    onApplyBulkChanges,
    onDeleteSelected,
    onClearSelection,
}: WorkItemsToolbarProps) {
    const styles = useStyles()

    return (
        <div className={mergeClasses(styles.toolbarRow, isMobile && styles.toolbarRowMobile)}>
            <div className={mergeClasses(styles.toolbarLeft, isMobile && styles.toolbarLeftMobile)}>
                <TabList
                    className={mergeClasses(isMobile && styles.viewTabsMobile)}
                    selectedValue={viewMode}
                    onTabSelect={(_event, data) => onViewModeChange(data.value as WorkItemsViewMode)}
                    size="small"
                >
                    <Tab value="backlog" icon={<TextBulletListTreeRegular />}>Backlog</Tab>
                    <Tab value="list" icon={<TextBulletListLtrRegular />}>List</Tab>
                    <Tab value="board" icon={<BoardRegular />}>Board</Tab>
                </TabList>
                <Toolbar className={mergeClasses(styles.inlineToolbar, isMobile && styles.inlineToolbarMobile)}>
                    <Input
                        className={mergeClasses(styles.searchInput, isMobile && styles.searchInputMobile)}
                        placeholder="Search work items..."
                        size="small"
                        appearance="underline"
                        value={searchQuery}
                        onChange={(_event, data) => onSearchQueryChange(data.value)}
                        contentBefore={<SearchRegular />}
                    />
                    <WorkItemFiltersPopover
                        isMobile={isMobile}
                        filters={filters}
                        levels={levels}
                        onChange={onFiltersChange}
                    />
                    <WorkItemColumnsPopover
                        isMobile={isMobile}
                        collapsedColumns={collapsedColumns}
                        onToggleColumnVisibility={onToggleColumnVisibility}
                        onResetColumns={onResetColumns}
                    />
                    <ToolbarDivider />
                    <ToolbarButton onClick={onManageLevels}>Levels</ToolbarButton>
                    {selectedCount > 0 && (
                        <>
                            <ToolbarDivider />
                            <WorkItemsBulkActions
                                isMobile={isMobile}
                                selectedCount={selectedCount}
                                bulkState={bulkState}
                                bulkAssignmentLabel={bulkAssignmentLabel}
                                descendantSelectionCount={descendantSelectionCount}
                                canApplyBulkChanges={canApplyBulkChanges}
                                isMutating={isMutatingBulkActions}
                                onBulkStateChange={onBulkStateChange}
                                onBulkAssignmentChange={onBulkAssignmentChange}
                                onSelectDescendants={onSelectDescendants}
                                onApply={onApplyBulkChanges}
                                onDeleteSelected={onDeleteSelected}
                                onClearSelection={onClearSelection}
                            />
                        </>
                    )}
                </Toolbar>
            </div>
        </div>
    )
}
