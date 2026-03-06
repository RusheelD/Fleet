const TOKEN_SPLIT_REGEX = /[\s._-]+/

function sanitizeTokens(value: string): string[] {
  return value
    .trim()
    .split(TOKEN_SPLIT_REGEX)
    .map(token => token.trim())
    .filter(Boolean)
}

export function resolveChatUserIdentity(displayName?: string, email?: string): string {
  if (displayName && displayName.trim().length > 0) {
    return displayName.trim()
  }
  if (email && email.trim().length > 0) {
    return email.trim()
  }
  return 'Me'
}

export function formatInitials(identity?: string, fallback = 'Me'): string {
  const source = (identity ?? '').trim()
  const normalizedSource = source.includes('@')
    ? source.split('@')[0]
    : source

  const tokens = sanitizeTokens(normalizedSource)
  if (tokens.length === 0) {
    const fallbackTokens = sanitizeTokens(fallback)
    if (fallbackTokens.length === 0) {
      return 'ME'
    }
    if (fallbackTokens.length === 1) {
      return fallbackTokens[0].slice(0, 2).toUpperCase()
    }
    return `${fallbackTokens[0][0]}${fallbackTokens[1][0]}`.toUpperCase()
  }

  if (tokens.length === 1) {
    return tokens[0].slice(0, 2).toUpperCase()
  }

  return `${tokens[0][0]}${tokens[1][0]}`.toUpperCase()
}
