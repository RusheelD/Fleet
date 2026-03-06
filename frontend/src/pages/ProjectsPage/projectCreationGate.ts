export interface ProjectCreationGateInput {
  title: string
  slugPreview: string
  slugAvailable: boolean
  hasGitHub: boolean
  hasSelectedRepo: boolean
  isPending: boolean
}

export function canCreateProject(input: ProjectCreationGateInput): boolean {
  return input.title.trim().length > 0
    && input.slugPreview.length > 0
    && input.slugAvailable
    && input.hasGitHub
    && input.hasSelectedRepo
    && !input.isPending
}
