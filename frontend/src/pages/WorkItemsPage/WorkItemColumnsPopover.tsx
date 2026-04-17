import { Button, Checkbox, Divider, Popover, PopoverSurface, PopoverTrigger, Text, ToolbarButton, makeStyles, mergeClasses } from '@fluentui/react-components'
import { DismissRegular } from '@fluentui/react-icons'
import { appTokens } from '../../styles/appTokens'
import { WORK_ITEM_TABLE_COLUMNS, type WorkItemTableColumnKey } from './workItemTableColumns'

const useStyles = makeStyles({
    columnSurface: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column',
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
})

interface WorkItemColumnsPopoverProps {
    isMobile: boolean
    collapsedColumns: ReadonlySet<WorkItemTableColumnKey>
    onToggleColumnVisibility: (column: WorkItemTableColumnKey, visible: boolean) => void
    onResetColumns: () => void
}

export function WorkItemColumnsPopover({
    isMobile,
    collapsedColumns,
    onToggleColumnVisibility,
    onResetColumns,
}: WorkItemColumnsPopoverProps) {
    const styles = useStyles()

    return (
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
                        onClick={onResetColumns}
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
                            onChange={(_event, data) => onToggleColumnVisibility(column.key, data.checked === true)}
                        />
                    )
                })}
            </PopoverSurface>
        </Popover>
    )
}
