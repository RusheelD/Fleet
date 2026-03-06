import { describe, expect, it } from 'vitest'
import { formatInitials, resolveChatUserIdentity } from './initials'

describe('chat initials', () => {
  it('uses display name first, then email, then Me', () => {
    expect(resolveChatUserIdentity('Rusheel Sharma', 'rusheel@live.com')).toBe('Rusheel Sharma')
    expect(resolveChatUserIdentity('', 'rusheel@live.com')).toBe('rusheel@live.com')
    expect(resolveChatUserIdentity('', '')).toBe('Me')
  })

  it('formats initials for multi-word names, single words, and emails', () => {
    expect(formatInitials('Rusheel Sharma')).toBe('RS')
    expect(formatInitials('rusheel')).toBe('RU')
    expect(formatInitials('rusheel@live.com')).toBe('RU')
  })
})
