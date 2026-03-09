export interface ProjectCreationGateInput {
  title: string
  slugPreview: string
  slugAvailable: boolean
  hasGitHub: boolean
  repoMode: 'existing' | 'new'
  hasSelectedRepo: boolean
  hasSelectedAccount: boolean
  newRepoNameValid: boolean
  newRepoNameTaken: boolean
  isPending: boolean
}

export function canCreateProject(input: ProjectCreationGateInput): boolean {
  const baseChecksPass = input.title.trim().length > 0
    && input.slugPreview.length > 0
    && input.slugAvailable
    && input.hasGitHub
    && !input.isPending

  if (!baseChecksPass) {
    return false
  }

  if (input.repoMode === 'new') {
    return input.hasSelectedAccount && input.newRepoNameValid && !input.newRepoNameTaken
  }

  return input.hasSelectedRepo
}
