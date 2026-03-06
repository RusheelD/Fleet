export type PullRequestOpener = (url?: string | URL, target?: string, features?: string) => Window | null

export function toPullRequestDiffUrl(pullRequestUrl: string | null | undefined): string | null {
  if (!pullRequestUrl) {
    return null
  }

  return `${pullRequestUrl.replace(/\/+$/, '')}/files`
}

export function openPullRequest(
  pullRequestUrl: string | null | undefined,
  opener: PullRequestOpener = window.open,
): boolean {
  if (!pullRequestUrl) {
    return false
  }

  opener(pullRequestUrl, '_blank', 'noopener,noreferrer')
  return true
}

export function openPullRequestDiff(
  pullRequestUrl: string | null | undefined,
  opener: PullRequestOpener = window.open,
): boolean {
  const diffUrl = toPullRequestDiffUrl(pullRequestUrl)
  if (!diffUrl) {
    return false
  }

  opener(diffUrl, '_blank', 'noopener,noreferrer')
  return true
}
