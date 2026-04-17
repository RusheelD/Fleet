import { describe, expect, it } from 'vitest'
import type { AgentExecution, WorkItem } from '../../models'
import { buildPlannedSubFlowSteps } from './plannedSubFlows'

function createExecution(overrides: Partial<AgentExecution> = {}): AgentExecution {
    return {
        id: overrides.id ?? 'execution-1',
        workItemId: overrides.workItemId ?? 1,
        workItemTitle: overrides.workItemTitle ?? 'Execution',
        executionMode: overrides.executionMode ?? 'orchestration',
        status: overrides.status ?? 'running',
        agents: overrides.agents ?? [],
        startedAt: overrides.startedAt ?? '2026-04-17T00:00:00.000Z',
        duration: overrides.duration ?? '1m',
        progress: overrides.progress ?? 0.2,
        branchName: overrides.branchName ?? null,
        pullRequestUrl: overrides.pullRequestUrl ?? null,
        currentPhase: overrides.currentPhase ?? 'Working',
        reviewLoopCount: overrides.reviewLoopCount ?? 0,
        lastReviewRecommendation: overrides.lastReviewRecommendation ?? null,
        parentExecutionId: overrides.parentExecutionId ?? null,
        subFlows: overrides.subFlows ?? [],
    }
}

function createWorkItem(overrides: Partial<WorkItem>): WorkItem {
    return {
        workItemNumber: overrides.workItemNumber ?? 1,
        title: overrides.title ?? `Work item ${overrides.workItemNumber ?? 1}`,
        state: overrides.state ?? 'New',
        priority: overrides.priority ?? 2,
        difficulty: overrides.difficulty ?? 3,
        assignedTo: overrides.assignedTo ?? '',
        tags: overrides.tags ?? [],
        isAI: overrides.isAI ?? true,
        description: overrides.description ?? '',
        parentWorkItemNumber: overrides.parentWorkItemNumber ?? null,
        childWorkItemNumbers: overrides.childWorkItemNumbers ?? [],
        levelId: overrides.levelId ?? null,
        assignmentMode: overrides.assignmentMode ?? 'auto',
        assignedAgentCount: overrides.assignedAgentCount ?? null,
        acceptanceCriteria: overrides.acceptanceCriteria ?? '',
        linkedPullRequestUrl: overrides.linkedPullRequestUrl ?? null,
    }
}

describe('buildPlannedSubFlowSteps', () => {
    it('orders sub-flows as completed, then in-progress/incomplete, then not started, with lower numbers first', () => {
        const execution = createExecution({
            workItemId: 1,
            subFlows: [
                createExecution({ id: 'sub-5', workItemId: 5, workItemTitle: 'Five', status: 'queued' }),
                createExecution({ id: 'sub-3', workItemId: 3, workItemTitle: 'Three', status: 'running' }),
                createExecution({ id: 'sub-4', workItemId: 4, workItemTitle: 'Four', status: 'failed' }),
                createExecution({ id: 'sub-2', workItemId: 2, workItemTitle: 'Two', status: 'completed' }),
            ],
        })

        const workItems = [
            createWorkItem({ workItemNumber: 1, title: 'Parent', childWorkItemNumbers: [5, 3, 4, 2] }),
            createWorkItem({ workItemNumber: 2, title: 'Two', parentWorkItemNumber: 1 }),
            createWorkItem({ workItemNumber: 3, title: 'Three', parentWorkItemNumber: 1 }),
            createWorkItem({ workItemNumber: 4, title: 'Four', parentWorkItemNumber: 1 }),
            createWorkItem({ workItemNumber: 5, title: 'Five', parentWorkItemNumber: 1 }),
        ]

        const plannedSteps = buildPlannedSubFlowSteps(execution, workItems)

        expect(plannedSteps.map((step) => step.workItemNumber)).toEqual([2, 3, 4, 5])
    })
})
