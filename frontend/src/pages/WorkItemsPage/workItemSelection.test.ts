import { describe, expect, it } from 'vitest'
import type { WorkItem } from '../../models'
import { collectDescendantWorkItemNumbers } from './workItemSelection'

function createWorkItem(
    workItemNumber: number,
    childWorkItemNumbers: number[],
    parentWorkItemNumber: number | null = null,
): WorkItem {
    return {
        workItemNumber,
        title: `Item ${workItemNumber}`,
        state: 'New',
        priority: 2,
        difficulty: 3,
        assignedTo: 'Fleet AI',
        tags: [],
        isAI: true,
        description: '',
        parentWorkItemNumber,
        childWorkItemNumbers,
        levelId: null,
        assignmentMode: 'auto',
        assignedAgentCount: null,
        acceptanceCriteria: '',
    }
}

describe('work item selection helpers', () => {
    it('collects all descendants across multiple selected roots without duplicates', () => {
        const items = [
            createWorkItem(1, [2, 3]),
            createWorkItem(2, [4], 1),
            createWorkItem(3, [5], 1),
            createWorkItem(4, [], 2),
            createWorkItem(5, [], 3),
        ]

        expect(collectDescendantWorkItemNumbers(items, [1, 2])).toEqual([3, 4, 5])
    })

    it('ignores descendants that are already selected', () => {
        const items = [
            createWorkItem(10, [11, 12]),
            createWorkItem(11, [13], 10),
            createWorkItem(12, [], 10),
            createWorkItem(13, [], 11),
        ]

        expect(collectDescendantWorkItemNumbers(items, [10, 11, 13])).toEqual([12])
    })
})
