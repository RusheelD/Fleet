import type { ExecutionDocumentation } from '../../proxies'

function slugify(value: string): string {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
}

function looksLikeMarkdownDocument(body: string): boolean {
  const lines = body.trim().split('\n')
  const nonEmptyLines = lines.map((line) => line.trim()).filter((line) => line.length > 0)
  if (nonEmptyLines.length === 0) {
    return false
  }

  let markdownSignals = 0
  let proseSignals = 0
  let codeSignals = 0
  let treeSignals = 0

  for (const line of nonEmptyLines) {
    if (/^#{1,6}\s+/.test(line) || /^[-*+]\s+/.test(line) || /^\d+\.\s+/.test(line) || /^>\s+/.test(line) || /^\|.+\|$/.test(line)) {
      markdownSignals++
    }

    if (/[├└│─]/.test(line) || /^\S+\/$/.test(line) || /^\s{2,}\S/.test(line)) {
      treeSignals++
    }

    if (/^\s*(const|let|var|if|for|while|return|using|public|private|class|function|import|export|SELECT|INSERT|UPDATE|DELETE)\b/.test(line) ||
      /[;{}<>]=?|=>/.test(line)) {
      codeSignals++
    }

    if (/[A-Za-z]/.test(line) && !/^[-*+>\d#|`]/.test(line)) {
      proseSignals++
    }
  }

  if (treeSignals > 0 && markdownSignals === 0) {
    return false
  }

  if (codeSignals > 0 && markdownSignals === 0) {
    return false
  }

  return markdownSignals >= 2 || (markdownSignals >= 1 && proseSignals >= 1)
}

function shouldUnwrapFence(info: string, body: string): boolean {
  const language = info.trim().toLowerCase().split(/\s+/)[0]
  if (language === 'md' || language === 'markdown' || language === 'mdx') {
    return true
  }

  if (language && language !== 'text' && language !== 'txt') {
    return false
  }

  return looksLikeMarkdownDocument(body)
}

function unwrapMarkdownFences(markdown: string): string {
  const normalized = markdown.replace(/\r\n/g, '\n')
  const lines = normalized.split('\n')
  const output: string[] = []

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index]
    const openFence = line.match(/^(`{3,})([^`]*)$/)
    if (!openFence) {
      output.push(line)
      continue
    }

    const fence = openFence[1]
    const info = openFence[2] ?? ''
    const blockLines: string[] = []
    let closingIndex = index + 1
    while (closingIndex < lines.length && !new RegExp(`^${fence}\\s*$`).test(lines[closingIndex])) {
      blockLines.push(lines[closingIndex])
      closingIndex++
    }

    if (closingIndex >= lines.length) {
      output.push(line)
      continue
    }

    const body = blockLines.join('\n')
    if (shouldUnwrapFence(info, body)) {
      output.push(unwrapMarkdownFences(body))
    } else {
      output.push(line, ...blockLines, lines[closingIndex])
    }

    index = closingIndex
  }

  return output.join('\n')
}

export function normalizeExecutionDocumentationMarkdown(markdown: string): string {
  return unwrapMarkdownFences(markdown).trim()
}

export function hasExecutionDocumentation(docs: ExecutionDocumentation | null | undefined): boolean {
  return !!docs?.markdown?.trim()
}

export function buildExecutionDocumentationFileName(docs: ExecutionDocumentation): string {
  const slug = slugify(docs.title)
  const baseName = slug.length > 0 ? slug.slice(0, 80) : `execution-${docs.executionId}`
  return `fleet-${baseName}.md`
}

export function downloadExecutionDocumentation(
  docs: ExecutionDocumentation,
  documentRef: Document = document,
): boolean {
  if (!hasExecutionDocumentation(docs)) {
    return false
  }

  const markdown = normalizeExecutionDocumentationMarkdown(docs.markdown)
  const blob = new Blob([markdown], { type: 'text/markdown;charset=utf-8' })
  const downloadUrl = URL.createObjectURL(blob)
  const anchor = documentRef.createElement('a')

  anchor.href = downloadUrl
  anchor.download = buildExecutionDocumentationFileName(docs)
  documentRef.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  setTimeout(() => URL.revokeObjectURL(downloadUrl), 0)

  return true
}
