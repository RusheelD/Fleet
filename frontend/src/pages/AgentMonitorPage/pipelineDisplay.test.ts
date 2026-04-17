import { describe, expect, it } from 'vitest'
import { buildPipelineDisplaySteps, type DisplayAgentInfo, type PlannedSubFlowStep } from './pipelineDisplay'

function createAgent(role: string): DisplayAgentInfo {
    return {
        role,
        status: 'queued',
        currentTask: `${role} waiting`,
        progress: 0,
    }
}

function createSubFlow(workItemNumber: number): PlannedSubFlowStep {
    return {
        workItemNumber,
        title: `Sub-flow ${workItemNumber}`,
        status: 'queued',
        currentTask: `Sub-flow ${workItemNumber} queued`,
        progress: 0,
    }
}

describe('buildPipelineDisplaySteps', () => {
    it('inserts sub-flows between contracts and consolidation for orchestration runs', () => {
        const steps = buildPipelineDisplaySteps(
            'orchestration',
            [
                createAgent('Manager'),
                createAgent('Planner'),
                createAgent('Contracts'),
                createAgent('Consolidation'),
                createAgent('Review'),
            ],
            [createSubFlow(2), createSubFlow(3)],
        )

        expect(steps.map((step) => step.title)).toEqual([
            'Manager',
            'Planner',
            'Contracts',
            'Sub-flow #2',
            'Sub-flow #3',
            'Consolidation',
            'Review',
        ])
    })

    it('falls back to inserting sub-flows after contracts when consolidation is missing', () => {
        const steps = buildPipelineDisplaySteps(
            'orchestration',
            [
                createAgent('Manager'),
                createAgent('Planner'),
                createAgent('Contracts'),
                createAgent('Review'),
            ],
            [createSubFlow(7)],
        )

        expect(steps.map((step) => step.title)).toEqual([
            'Manager',
            'Planner',
            'Contracts',
            'Sub-flow #7',
            'Review',
        ])
    })

    it('keeps direct runs in raw agent order', () => {
        const steps = buildPipelineDisplaySteps(
            'standard',
            [
                createAgent('Planner'),
                createAgent('Backend #1'),
                createAgent('Review'),
            ],
            [createSubFlow(4)],
        )

        expect(steps.map((step) => step.title)).toEqual([
            'Planner',
            'Backend #1',
            'Review',
        ])
    })
})
