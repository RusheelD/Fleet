import { get } from './'
import type { SearchResult } from '../models'

export function search(query: string, type?: string): Promise<SearchResult[]> {
  const params = new URLSearchParams()
  if (query) params.set('q', query)
  if (type && type !== 'all') params.set('type', type)
  return get<SearchResult[]>(`/api/search?${params.toString()}`)
}
