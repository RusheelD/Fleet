import { Button, Dropdown, Option, Text, makeStyles, mergeClasses } from '@fluentui/react-components'
import { DeleteRegular } from '@fluentui/react-icons'
import type { WorkItemState } from '../../models'
import { appTokens } from '../../styles/appTokens'
import { WORK_ITEM_ASSIGNMENT_OPTION_LABELS } from './workItemAssignmentOptions'
import { ALL_WORK_ITEM_STATES } from './workItemFilters'

const useStyles = makeStyles({
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
})

interface WorkItemsBulkActionsProps {
    isMobile: boolean
    selectedCount: number
    bulkState: WorkItemState | ''
    bulkAssignmentLabel: string
    descendantSelectionCount: number
    canApplyBulkChanges: boolean
    isMutating: boolean
    onBulkStateChange: (value: WorkItemState | '') => void
    onBulkAssignmentChange: (value: string) => void
    onSelectDescendants: () => void
    onApply: () => void
    onDeleteSelected: () => void
    onClearSelection: () => void
}

export function WorkItemsBulkActions({
    isMobile,
    selectedCount,
    bulkState,
    bulkAssignmentLabel,
    descendantSelectionCount,
    canApplyBulkChanges,
    isMutating,
    onBulkStateChange,
    onBulkAssignmentChange,
    onSelectDescendants,
    onApply,
    onDeleteSelected,
    onClearSelection,
}: WorkItemsBulkActionsProps) {
    const styles = useStyles()

    return (
        <div className={mergeClasses(styles.bulkActions, isMobile && styles.bulkActionsMobile)}>
            <Text size={200} className={styles.bulkLabel}>
                {selectedCount} selected
            </Text>
            <Dropdown
                className={mergeClasses(styles.bulkStateDropdown, isMobile && styles.bulkStateDropdownMobile)}
                size="small"
                placeholder="State"
                selectedOptions={bulkState ? [bulkState] : []}
                onOptionSelect={(_event, data) => onBulkStateChange((data.optionValue as WorkItemState | undefined) ?? '')}
            >
                <Option value="">No state change</Option>
                {ALL_WORK_ITEM_STATES.map((state) => (
                    <Option key={state} value={state}>{state}</Option>
                ))}
            </Dropdown>
            <Dropdown
                className={mergeClasses(styles.bulkAssignmentDropdown, isMobile && styles.bulkAssignmentDropdownMobile)}
                size="small"
                placeholder="Assignment"
                selectedOptions={bulkAssignmentLabel ? [bulkAssignmentLabel] : []}
                value={bulkAssignmentLabel}
                onOptionSelect={(_event, data) => onBulkAssignmentChange((data.optionValue as string | undefined) ?? '')}
            >
                <Option value="">No assignment change</Option>
                {WORK_ITEM_ASSIGNMENT_OPTION_LABELS.map((label) => (
                    <Option key={label} value={label}>{label}</Option>
                ))}
            </Dropdown>
            <Button
                appearance="secondary"
                size="small"
                disabled={descendantSelectionCount === 0}
                onClick={onSelectDescendants}
            >
                {descendantSelectionCount > 0
                    ? `Select Descendants (${descendantSelectionCount})`
                    : 'Select Descendants'}
            </Button>
            <Button
                appearance="primary"
                size="small"
                disabled={!canApplyBulkChanges || isMutating}
                onClick={onApply}
            >
                Apply
            </Button>
            <Button
                appearance="subtle"
                size="small"
                icon={<DeleteRegular />}
                disabled={isMutating}
                onClick={onDeleteSelected}
            >
                Delete Selected
            </Button>
            <Button
                appearance="subtle"
                size="small"
                disabled={isMutating}
                onClick={onClearSelection}
            >
                Clear Selection
            </Button>
        </div>
    )
}
