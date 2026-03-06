import { describe, expect, it } from 'vitest'
import { getSearchTypeForCategory } from './searchCategory'

describe('getSearchTypeForCategory', () => {
  it('maps all category filters to backend-compatible types', () => {
    expect(getSearchTypeForCategory('all')).toBeUndefined()
    expect(getSearchTypeForCategory('projects')).toBe('projects')
    expect(getSearchTypeForCategory('workitems')).toBe('workitems')
    expect(getSearchTypeForCategory('chats')).toBe('chats')
    expect(getSearchTypeForCategory('agents')).toBe('agents')
  })
})
