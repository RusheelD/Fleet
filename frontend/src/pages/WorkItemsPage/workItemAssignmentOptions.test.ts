import { describe, expect, it } from 'vitest'
import { AUTO_ASSIGNMENT_LABEL, getWorkItemAssignmentLabel, MANUAL_ASSIGNMENT_LABEL } from './workItemAssignmentOptions'

describe('work item assignment options', () => {
    it('treats auto mode as auto-detect even if a stale agent count is present', () => {
        expect(getWorkItemAssignmentLabel({
            isAI: true,
            assignmentMode: 'auto',
            assignedAgentCount: 3,
        })).toBe(AUTO_ASSIGNMENT_LABEL)
    })

    it('keeps non-ai work items as manual assignment', () => {
        expect(getWorkItemAssignmentLabel({
            isAI: false,
            assignmentMode: 'manual',
            assignedAgentCount: 5,
        })).toBe(MANUAL_ASSIGNMENT_LABEL)
    })
})
