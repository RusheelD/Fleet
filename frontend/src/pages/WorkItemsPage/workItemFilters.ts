import type { WorkItem, WorkItemLevel, WorkItemState } from '../../models'

export const BOARD_STATES = ['New', 'Active', 'In Progress', 'In PR', 'Resolved', 'Closed'] as const
export const ALL_WORK_ITEM_STATES: WorkItemState[] = ['New', 'Active', 'Planning (AI)', 'In Progress', 'In Progress (AI)', 'In-PR', 'In-PR (AI)', 'Resolved', 'Resolved (AI)', 'Closed']
export const ALL_WORK_ITEM_PRIORITIES = [1, 2, 3, 4] as const
export const NO_TYPE_FILTER_KEY = '__none__'

export interface WorkItemFilters {
    states: Set<WorkItemState>
    priorities: Set<number>
    levelKeys: Set<string>
    aiOnly: boolean | null
}

export const EMPTY_WORK_ITEM_FILTERS: WorkItemFilters = {
    states: new Set(),
    priorities: new Set(),
    levelKeys: new Set(),
    aiOnly: null,
}

export function areWorkItemFiltersActive(filters: WorkItemFilters): boolean {
    return filters.states.size > 0 ||
        filters.priorities.size > 0 ||
        filters.levelKeys.size > 0 ||
        filters.aiOnly !== null
}

export function getBoardColumns(items: WorkItem[]) {
    return BOARD_STATES.map((state) => ({
        state,
        items: items.filter((item) => {
            if (state === 'In Progress') {
                return item.state === 'Planning (AI)' || item.state === 'In Progress' || item.state === 'In Progress (AI)'
            }

            if (state === 'In PR') {
                return item.state === 'In-PR' || item.state === 'In-PR (AI)'
            }

            if (state === 'Resolved') {
                return item.state === 'Resolved' || item.state === 'Resolved (AI)'
            }

            return item.state === state
        }),
    }))
}

export function filterWorkItems(
    workItems: WorkItem[],
    filters: WorkItemFilters,
    searchQuery: string,
): WorkItem[] {
    let filtered = workItems

    if (filters.states.size > 0) {
        filtered = filtered.filter((workItem) => filters.states.has(workItem.state))
    }

    if (filters.priorities.size > 0) {
        filtered = filtered.filter((workItem) => filters.priorities.has(workItem.priority))
    }

    if (filters.levelKeys.size > 0) {
        filtered = filtered.filter((workItem) => {
            const key = workItem.levelId == null ? NO_TYPE_FILTER_KEY : workItem.levelId.toString()
            return filters.levelKeys.has(key)
        })
    }

    if (filters.aiOnly === true) {
        filtered = filtered.filter((workItem) => workItem.isAI)
    } else if (filters.aiOnly === false) {
        filtered = filtered.filter((workItem) => !workItem.isAI)
    }

    if (!searchQuery) {
        return filtered
    }

    const normalizedQuery = searchQuery.toLowerCase()
    return filtered.filter((workItem) =>
        workItem.title.toLowerCase().includes(normalizedQuery) ||
        workItem.description.toLowerCase().includes(normalizedQuery) ||
        workItem.assignedTo.toLowerCase().includes(normalizedQuery) ||
        workItem.tags.some((tag) => tag.toLowerCase().includes(normalizedQuery)))
}

export function buildLevelMap(levels: WorkItemLevel[] | undefined): Map<number, WorkItemLevel> {
    const levelMap = new Map<number, WorkItemLevel>()
    for (const level of levels ?? []) {
        levelMap.set(level.id, level)
    }

    return levelMap
}

export function sortWorkItemLevels(levels: WorkItemLevel[] | undefined): WorkItemLevel[] {
    return [...(levels ?? [])].sort((left, right) => left.ordinal - right.ordinal || left.name.localeCompare(right.name))
}
