import type { ExecutionDocumentation } from '../../proxies'

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
}

function buildDocumentHtml(docs: ExecutionDocumentation): string {
  const pullRequestLink = docs.pullRequestUrl ? `<a href="${docs.pullRequestUrl}" target="_blank" rel="noopener noreferrer">Pull Request</a>` : ''
  const diffLink = docs.diffUrl ? `<a href="${docs.diffUrl}" target="_blank" rel="noopener noreferrer">Diff</a>` : ''
  const links = [pullRequestLink, diffLink].filter(Boolean).join(' | ')
  const linksSection = links ? `<p>${links}</p>` : ''

  return `<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(docs.title)}</title>
  <style>
    body { font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; color: #1f2937; }
    h1 { margin-top: 0; font-size: 20px; }
    p { margin: 8px 0 16px; }
    pre { white-space: pre-wrap; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; line-height: 1.4; }
    a { color: #0f6cbd; text-decoration: none; }
    a:hover { text-decoration: underline; }
  </style>
</head>
<body>
  <h1>${escapeHtml(docs.title)}</h1>
  ${linksSection}
  <pre>${escapeHtml(docs.markdown)}</pre>
</body>
</html>`
}

export function openExecutionDocumentation(
  docs: ExecutionDocumentation,
  opener: (url?: string | URL, target?: string, features?: string) => Window | null = window.open,
): boolean {
  if (!docs.markdown?.trim()) {
    return false
  }

  const html = buildDocumentHtml(docs)
  const blob = new Blob([html], { type: 'text/html;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const opened = opener(url, '_blank', 'noopener,noreferrer')
  if (!opened) {
    URL.revokeObjectURL(url)
    return false
  }

  setTimeout(() => URL.revokeObjectURL(url), 60_000)
  return true
}
