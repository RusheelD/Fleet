import { describe, expect, it } from 'vitest'
import { getNextSubFlowExpansionState, isSubFlowExpandedByDefault } from './subFlowExpansion'

describe('subFlowExpansion', () => {
    it('defaults active and failed sub-flows to expanded', () => {
        expect(isSubFlowExpandedByDefault({ status: 'running' })).toBe(true)
        expect(isSubFlowExpandedByDefault({ status: 'paused' })).toBe(true)
        expect(isSubFlowExpandedByDefault({ status: 'failed' })).toBe(true)
        expect(isSubFlowExpandedByDefault({ status: 'queued' })).toBe(false)
    })

    it('collapses a default-expanded sub-flow on the first toggle', () => {
        expect(getNextSubFlowExpansionState(undefined, { status: 'running' })).toBe(false)
        expect(getNextSubFlowExpansionState(undefined, { status: 'failed' })).toBe(false)
    })

    it('expands a default-collapsed sub-flow on the first toggle', () => {
        expect(getNextSubFlowExpansionState(undefined, { status: 'queued' })).toBe(true)
        expect(getNextSubFlowExpansionState(undefined, { status: 'completed' })).toBe(true)
    })

    it('continues toggling from the stored user override after the first click', () => {
        expect(getNextSubFlowExpansionState(false, { status: 'running' })).toBe(true)
        expect(getNextSubFlowExpansionState(true, { status: 'queued' })).toBe(false)
    })
})
