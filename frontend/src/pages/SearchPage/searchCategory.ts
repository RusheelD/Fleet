export type SearchCategory = 'all' | 'projects' | 'workitems' | 'chats' | 'agents'

const categoryToType: Record<SearchCategory, string | undefined> = {
  all: undefined,
  projects: 'projects',
  workitems: 'workitems',
  chats: 'chats',
  agents: 'agents',
}

export function getSearchTypeForCategory(category: SearchCategory): string | undefined {
  return categoryToType[category]
}
