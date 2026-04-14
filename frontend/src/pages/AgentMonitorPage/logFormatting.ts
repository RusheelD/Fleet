import type { LogEntry } from '../../models'

export interface ParsedLogBadge {
  label: string
}

export interface ParsedLogMeta {
  key: string
  value: string
}

export interface ParsedStructuredLog {
  kind: 'summary' | 'diagnostics'
  headline: string
  context?: string
  body?: string
  badges: ParsedLogBadge[]
  metadata: ParsedLogMeta[]
}

function splitOnce(value: string, separator: string): [string, string] | null {
  const separatorIndex = value.indexOf(separator)
  if (separatorIndex < 0) {
    return null
  }

  return [
    value.slice(0, separatorIndex).trim(),
    value.slice(separatorIndex + separator.length).trim(),
  ]
}

function splitLast(value: string, separator: string): [string, string] | null {
  const separatorIndex = value.lastIndexOf(separator)
  if (separatorIndex < 0) {
    return null
  }

  return [
    value.slice(0, separatorIndex).trim(),
    value.slice(separatorIndex + separator.length).trim(),
  ]
}

function collectBadges(source: string): ParsedLogBadge[] {
  const normalized = source.toLowerCase()
  const labels: string[] = []

  if (normalized.includes('azure openai')) {
    labels.push('Azure OpenAI')
  }
  if (normalized.includes('content filter') || normalized.includes('content_filter')) {
    labels.push('Content Filter')
  }
  if (normalized.includes('jailbreak')) {
    labels.push('Jailbreak')
  }
  if (normalized.includes('rate-limit') || normalized.includes('rate limit') || normalized.includes('429')) {
    labels.push('Rate Limit')
  }
  if (normalized.includes('timed out') || normalized.includes('timeout')) {
    labels.push('Timeout')
  }

  return labels.map((label) => ({ label }))
}

function prettifyKey(key: string): string {
  return key
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase())
}

function parseDiagnostics(message: string): ParsedStructuredLog | null {
  const diagnosticPrefix = 'Provider diagnostics:'
  if (!message.startsWith(diagnosticPrefix)) {
    return null
  }

  const payload = message.slice(diagnosticPrefix.length).trim()
  const parts = payload
    .split(';')
    .map((part) => part.trim())
    .filter(Boolean)

  const metadata: ParsedLogMeta[] = []
  let providerMessage = ''

  for (const part of parts) {
    const [rawKey, rawValue] = part.split('=', 2)
    if (!rawKey || !rawValue) {
      continue
    }

    if (rawKey === 'provider_message') {
      providerMessage = rawValue.trim()
      continue
    }

    metadata.push({
      key: prettifyKey(rawKey.trim()),
      value: rawValue.trim(),
    })
  }

  return {
    kind: 'diagnostics',
    headline: 'Provider diagnostics',
    body: providerMessage || undefined,
    badges: collectBadges(payload),
    metadata,
  }
}

function parseSummary(message: string, level: LogEntry['level']): ParsedStructuredLog | null {
  if (level !== 'error' && level !== 'warn') {
    return null
  }

  const directSplit = splitOnce(message, ': ')
  if (!directSplit) {
    return null
  }

  const [headline, remainder] = directSplit
  if (!headline || !remainder) {
    return null
  }

  let context = ''
  let body = remainder

  const nestedSplit = splitLast(remainder, ': ')
  if (nestedSplit && /(failed after|blocked|rejected|timed out|rate-limit|rate limit)/i.test(nestedSplit[1])) {
    context = nestedSplit[0]
    body = nestedSplit[1]
  }

  if (headline.length < 8 && context.length === 0 && body.length < 60) {
    return null
  }

  return {
    kind: 'summary',
    headline,
    context: context || undefined,
    body,
    badges: collectBadges(`${headline} ${context} ${body}`),
    metadata: [],
  }
}

export function parseStructuredLogMessage(message: string, level: LogEntry['level']): ParsedStructuredLog | null {
  return parseDiagnostics(message) ?? parseSummary(message, level)
}
