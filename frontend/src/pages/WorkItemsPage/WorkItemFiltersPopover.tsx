import { Button, Checkbox, Divider, Popover, PopoverSurface, PopoverTrigger, Text, ToolbarButton, makeStyles, mergeClasses } from '@fluentui/react-components'
import { DismissRegular, FilterRegular } from '@fluentui/react-icons'
import type { WorkItemLevel, WorkItemState } from '../../models'
import { appTokens } from '../../styles/appTokens'
import {
    ALL_WORK_ITEM_PRIORITIES,
    ALL_WORK_ITEM_STATES,
    EMPTY_WORK_ITEM_FILTERS,
    NO_TYPE_FILTER_KEY,
    areWorkItemFiltersActive,
    type WorkItemFilters,
} from './workItemFilters'

const useStyles = makeStyles({
    filterSurface: {
        padding: '0.875rem',
        display: 'flex',
        flexDirection: 'column',
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
        flexDirection: 'column',
        gap: appTokens.space.xxs,
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

interface WorkItemFiltersPopoverProps {
    isMobile: boolean
    filters: WorkItemFilters
    levels: WorkItemLevel[]
    onChange: (filters: WorkItemFilters) => void
}

export function WorkItemFiltersPopover({ isMobile, filters, levels, onChange }: WorkItemFiltersPopoverProps) {
    const styles = useStyles()
    const isActive = areWorkItemFiltersActive(filters)

    const updateSetFilter = <T,>(source: Set<T>, value: T, checked: boolean): Set<T> => {
        const next = new Set(source)
        if (checked) {
            next.add(value)
        } else {
            next.delete(value)
        }
        return next
    }

    const updateState = (state: WorkItemState, checked: boolean) => {
        onChange({ ...filters, states: updateSetFilter(filters.states, state, checked) })
    }

    const updatePriority = (priority: number, checked: boolean) => {
        onChange({ ...filters, priorities: updateSetFilter(filters.priorities, priority, checked) })
    }

    const updateLevel = (levelKey: string, checked: boolean) => {
        onChange({ ...filters, levelKeys: updateSetFilter(filters.levelKeys, levelKey, checked) })
    }

    return (
        <Popover withArrow>
            <PopoverTrigger disableButtonEnhancement>
                <ToolbarButton
                    className={mergeClasses(styles.filterTriggerButton, isActive && styles.filterTriggerActive)}
                    icon={<FilterRegular />}
                    appearance="transparent"
                    aria-label={isActive ? 'Filters active' : 'Filters'}
                    title={isActive ? 'Filters active' : 'Filters'}
                />
            </PopoverTrigger>
            <PopoverSurface className={mergeClasses(styles.filterSurface, isMobile && styles.filterSurfaceMobile)}>
                <div className={styles.filterHeader}>
                    <Text weight="semibold" size={300}>Filters</Text>
                    {isActive && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<DismissRegular />}
                            onClick={() => onChange(EMPTY_WORK_ITEM_FILTERS)}
                        >
                            Clear
                        </Button>
                    )}
                </div>
                <Divider />
                <div className={styles.filterSection}>
                    <Text size={200} weight="semibold">State</Text>
                    {ALL_WORK_ITEM_STATES.map((state) => (
                        <Checkbox
                            key={state}
                            label={state}
                            size="medium"
                            checked={filters.states.has(state)}
                            onChange={(_event, data) => updateState(state, data.checked === true)}
                        />
                    ))}
                </div>
                <Divider />
                <div className={styles.filterSection}>
                    <Text size={200} weight="semibold">Priority</Text>
                    {ALL_WORK_ITEM_PRIORITIES.map((priority) => (
                        <Checkbox
                            key={priority}
                            label={`Priority ${priority}`}
                            size="medium"
                            checked={filters.priorities.has(priority)}
                            onChange={(_event, data) => updatePriority(priority, data.checked === true)}
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
                        onChange={(_event, data) => updateLevel(NO_TYPE_FILTER_KEY, data.checked === true)}
                    />
                    {levels.map((level) => {
                        const key = level.id.toString()
                        return (
                            <Checkbox
                                key={level.id}
                                label={level.name}
                                size="medium"
                                checked={filters.levelKeys.has(key)}
                                onChange={(_event, data) => updateLevel(key, data.checked === true)}
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
                        onChange={(_event, data) => onChange({ ...filters, aiOnly: data.checked ? true : null })}
                    />
                    <Checkbox
                        label="Human-assigned only"
                        size="medium"
                        checked={filters.aiOnly === false}
                        onChange={(_event, data) => onChange({ ...filters, aiOnly: data.checked ? false : null })}
                    />
                </div>
            </PopoverSurface>
        </Popover>
    )
}
