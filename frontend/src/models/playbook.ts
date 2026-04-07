export interface PromptSkill {
  id: number
  name: string
  description: string
  whenToUse: string
  content: string
  enabled: boolean
  scope: 'personal' | 'project' | string
  projectId?: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface PromptSkillTemplate {
  key: string
  name: string
  description: string
  whenToUse: string
  content: string
}
