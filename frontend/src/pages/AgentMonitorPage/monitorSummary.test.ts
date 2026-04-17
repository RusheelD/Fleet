import { describe, expect, it } from 'vitest'
import type { AgentExecution } from '../../models/agent'
import { countActiveAgents, countActiveFlows, formatCountLabel } from './monitorSummary'

function createExecution(overrides: Partial<AgentExecution> = {}): AgentExecution {
    return {
        id: overrides.id ?? 'exec-1',
        workItemId: overrides.workItemId ?? 1,
        workItemTitle: overrides.workItemTitle ?? 'Work item',
        executionMode: overrides.executionMode ?? 'standard',
        status: overrides.status ?? 'running',
        agents: overrides.agents ?? [],
        startedAt: overrides.startedAt ?? new Date().toISOString(),
        duration: overrides.duration ?? '0s',
        progress: overrides.progress ?? 0,
        branchName: overrides.branchName,
        pullRequestUrl: overrides.pullRequestUrl,
        currentPhase: overrides.currentPhase,
        reviewLoopCount: overrides.reviewLoopCount,
        lastReviewRecommendation: overrides.lastReviewRecommendation,
        parentExecutionId: overrides.parentExecutionId,
        subFlows: overrides.subFlows ?? [],
    }
}

describe('monitorSummary', () => {
    it('counts nested active sub-flows in active flow totals', () => {
        const executions = [
            createExecution({
                id: 'parent',
                status: 'running',
                subFlows: [
                    createExecution({ id: 'child-running', status: 'running', parentExecutionId: 'parent' }),
                    createExecution({ id: 'child-queued', status: 'queued', parentExecutionId: 'parent' }),
                    createExecution({ id: 'child-complete', status: 'completed', parentExecutionId: 'parent' }),
                ],
            }),
        ]

        expect(countActiveFlows(executions)).toBe(3)
    })

    it('counts queued active sub-flows as active agents when no phase is running yet', () => {
        const executions = [
            createExecution({
                id: 'parent',
                status: 'running',
                agents: [{ role: 'Manager', status: 'running', currentTask: 'Coordinating', progress: 40 }],
                subFlows: [
                    createExecution({
                        id: 'child-queued',
                        status: 'queued',
                        parentExecutionId: 'parent',
                        agents: [{ role: 'Backend', status: 'idle', currentTask: '', progress: 0 }],
                    }),
                    createExecution({
                        id: 'child-running',
                        status: 'running',
                        parentExecutionId: 'parent',
                        agents: [{ role: 'Frontend', status: 'running', currentTask: 'Implementing', progress: 20 }],
                    }),
                ],
            }),
        ]

        expect(countActiveAgents(executions)).toBe(3)
    })

    it('singularizes active summary labels for a count of one', () => {
        expect(formatCountLabel(1, 'Active Flow')).toBe('Active Flow')
        expect(formatCountLabel(1, 'Active Agent')).toBe('Active Agent')
    })

    it('pluralizes active summary labels for counts other than one', () => {
        expect(formatCountLabel(0, 'Active Flow')).toBe('Active Flows')
        expect(formatCountLabel(2, 'Active Agent')).toBe('Active Agents')
    })
})
