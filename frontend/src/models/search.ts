export interface SearchResult {
  type: 'project' | 'workitem' | 'chat' | 'agent'
  title: string
  description: string
  meta: string
  projectSlug?: string
}
