import { describe, expect, it } from 'vitest'
import {
    parseWorkItemExpansionState,
    reconcileWorkItemExpansionState,
    serializeWorkItemExpansionState,
} from './workItemExpansionState'

describe('work item expansion state', () => {
    it('round trips expanded and collapsed item ids', () => {
        const serialized = serializeWorkItemExpansionState({
            expanded: new Set([3, 1]),
            collapsed: new Set([2]),
        })

        const parsed = parseWorkItemExpansionState(serialized)

        expect(Array.from(parsed.expanded)).toEqual([1, 3])
        expect(Array.from(parsed.collapsed)).toEqual([2])
    })

    it('ignores malformed stored state', () => {
        const parsed = parseWorkItemExpansionState('not-json')

        expect(parsed.expanded.size).toBe(0)
        expect(parsed.collapsed.size).toBe(0)
    })

    it('preserves manually collapsed parents when new children arrive', () => {
        const state = reconcileWorkItemExpansionState({
            currentExpanded: new Set([1]),
            currentCollapsed: new Set([2]),
            parentIds: new Set([1, 2, 3]),
            defaultExpandedParentIds: new Set([1, 2, 3]),
            autoCollapsedParentIds: new Set(),
        })

        expect(Array.from(state.expanded).sort((a, b) => a - b)).toEqual([1, 3])
        expect(Array.from(state.collapsed)).toEqual([2])
    })

    it('auto-collapses resolved parent branches even when they were previously expanded', () => {
        const state = reconcileWorkItemExpansionState({
            currentExpanded: new Set([1, 2]),
            currentCollapsed: new Set(),
            parentIds: new Set([1, 2]),
            defaultExpandedParentIds: new Set([1, 2]),
            autoCollapsedParentIds: new Set([2]),
        })

        expect(Array.from(state.expanded)).toEqual([1])
        expect(state.collapsed.has(2)).toBe(false)
    })
})
