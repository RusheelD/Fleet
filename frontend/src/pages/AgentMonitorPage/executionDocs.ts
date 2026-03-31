import type { ExecutionDocumentation } from '../../proxies'

function slugify(value: string): string {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
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

  const blob = new Blob([docs.markdown], { type: 'text/markdown;charset=utf-8' })
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
