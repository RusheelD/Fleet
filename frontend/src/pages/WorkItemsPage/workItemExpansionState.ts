export interface WorkItemExpansionState {
    expanded: Set<number>
    collapsed: Set<number>
}

interface SerializedWorkItemExpansionState {
    expanded?: unknown
    collapsed?: unknown
}

interface ReconcileWorkItemExpansionStateOptions {
    currentExpanded: ReadonlySet<number>
    currentCollapsed: ReadonlySet<number>
    parentIds: ReadonlySet<number>
    defaultExpandedParentIds: ReadonlySet<number>
    autoCollapsedParentIds: ReadonlySet<number>
}

export function getWorkItemExpansionStorageKey(projectId: string | undefined): string {
    return `fleet:work-items:${projectId ?? 'global'}:expansion`
}

export function parseWorkItemExpansionState(raw: string | null): WorkItemExpansionState {
    if (!raw) {
        return { expanded: new Set(), collapsed: new Set() }
    }

    try {
        const parsed = JSON.parse(raw) as SerializedWorkItemExpansionState
        return {
            expanded: sanitizeNumberSet(parsed.expanded),
            collapsed: sanitizeNumberSet(parsed.collapsed),
        }
    } catch {
        return { expanded: new Set(), collapsed: new Set() }
    }
}

export function serializeWorkItemExpansionState(state: WorkItemExpansionState): string {
    return JSON.stringify({
        expanded: Array.from(state.expanded).sort((a, b) => a - b),
        collapsed: Array.from(state.collapsed).sort((a, b) => a - b),
    })
}

export function reconcileWorkItemExpansionState({
    currentExpanded,
    currentCollapsed,
    parentIds,
    defaultExpandedParentIds,
    autoCollapsedParentIds,
}: ReconcileWorkItemExpansionStateOptions): WorkItemExpansionState {
    const expanded = new Set<number>()
    const collapsed = new Set<number>()

    for (const id of currentCollapsed) {
        if (parentIds.has(id) && !autoCollapsedParentIds.has(id)) {
            collapsed.add(id)
        }
    }

    for (const id of currentExpanded) {
        if (parentIds.has(id) && !autoCollapsedParentIds.has(id) && !collapsed.has(id)) {
            expanded.add(id)
        }
    }

    for (const id of defaultExpandedParentIds) {
        if (!autoCollapsedParentIds.has(id) && !collapsed.has(id)) {
            expanded.add(id)
        }
    }

    return { expanded, collapsed }
}

export function areWorkItemExpansionStatesEqual(
    left: WorkItemExpansionState,
    right: WorkItemExpansionState,
): boolean {
    return areSetsEqual(left.expanded, right.expanded) && areSetsEqual(left.collapsed, right.collapsed)
}

function sanitizeNumberSet(value: unknown): Set<number> {
    if (!Array.isArray(value)) {
        return new Set()
    }

    const numbers = value
        .filter((candidate): candidate is number => Number.isInteger(candidate) && candidate > 0)

    return new Set(numbers)
}

function areSetsEqual(left: ReadonlySet<number>, right: ReadonlySet<number>): boolean {
    if (left.size !== right.size) {
        return false
    }

    for (const value of left) {
        if (!right.has(value)) {
            return false
        }
    }

    return true
}
