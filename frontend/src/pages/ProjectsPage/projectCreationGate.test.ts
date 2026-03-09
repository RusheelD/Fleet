import { describe, expect, it } from 'vitest'
import { canCreateProject, type ProjectCreationGateInput } from './projectCreationGate'

const validInput: ProjectCreationGateInput = {
  title: 'New Project',
  slugPreview: 'new-project',
  slugAvailable: true,
  hasGitHub: true,
  repoMode: 'existing',
  hasSelectedRepo: true,
  hasSelectedAccount: true,
  newRepoNameValid: true,
  newRepoNameTaken: false,
  isPending: false,
}

describe('canCreateProject', () => {
  it('returns true only when all MVP gating conditions are satisfied', () => {
    expect(canCreateProject(validInput)).toBe(true)
  })

  it('requires a linked GitHub account and selected repository for existing mode', () => {
    expect(canCreateProject({ ...validInput, hasGitHub: false })).toBe(false)
    expect(canCreateProject({ ...validInput, hasSelectedRepo: false })).toBe(false)
  })

  it('blocks creation when slug is unavailable or request is pending', () => {
    expect(canCreateProject({ ...validInput, slugAvailable: false })).toBe(false)
    expect(canCreateProject({ ...validInput, isPending: true })).toBe(false)
  })

  it('requires account selection and valid repository name for new mode', () => {
    const newModeInput: ProjectCreationGateInput = {
      ...validInput,
      repoMode: 'new',
      hasSelectedRepo: false,
      hasSelectedAccount: true,
      newRepoNameValid: true,
    }
    expect(canCreateProject(newModeInput)).toBe(true)
    expect(canCreateProject({ ...newModeInput, hasSelectedAccount: false })).toBe(false)
    expect(canCreateProject({ ...newModeInput, newRepoNameValid: false })).toBe(false)
    expect(canCreateProject({ ...newModeInput, newRepoNameTaken: true })).toBe(false)
  })
})
