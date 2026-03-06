import { describe, expect, it } from 'vitest'
import { canCreateProject, type ProjectCreationGateInput } from './projectCreationGate'

const validInput: ProjectCreationGateInput = {
  title: 'New Project',
  slugPreview: 'new-project',
  slugAvailable: true,
  hasGitHub: true,
  hasSelectedRepo: true,
  isPending: false,
}

describe('canCreateProject', () => {
  it('returns true only when all MVP gating conditions are satisfied', () => {
    expect(canCreateProject(validInput)).toBe(true)
  })

  it('requires a linked GitHub account and selected repository', () => {
    expect(canCreateProject({ ...validInput, hasGitHub: false })).toBe(false)
    expect(canCreateProject({ ...validInput, hasSelectedRepo: false })).toBe(false)
  })

  it('blocks creation when slug is unavailable or request is pending', () => {
    expect(canCreateProject({ ...validInput, slugAvailable: false })).toBe(false)
    expect(canCreateProject({ ...validInput, isPending: true })).toBe(false)
  })
})
