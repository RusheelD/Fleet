import { fetchWithAuth } from '../../proxies/proxy'

const ProtectedAttachmentPathPattern = /\/api\/(?:chat\/attachments\/[^/]+\/content|projects\/[^/]+\/work-items\/\d+\/attachments\/[^/]+\/content)(?:$|\?)/i

export function isProtectedChatAttachmentUrl(url?: string | null): boolean {
    return typeof url === 'string' && ProtectedAttachmentPathPattern.test(url)
}

export async function openProtectedAttachmentUrl(
    sourceUrl: string,
    fileName?: string,
    mode: 'open' | 'download' = 'open',
): Promise<void> {
    if (!isProtectedChatAttachmentUrl(sourceUrl)) {
        if (mode === 'download') {
            const anchor = document.createElement('a')
            anchor.href = sourceUrl
            if (fileName) {
                anchor.download = fileName
            }
            anchor.click()
            return
        }

        window.open(sourceUrl, '_blank', 'noopener,noreferrer')
        return
    }

    const response = await fetchWithAuth(sourceUrl)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }

    const blob = await response.blob()
    const objectUrl = URL.createObjectURL(blob)
    const cleanup = () => window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)

    if (mode === 'download') {
        const anchor = document.createElement('a')
        anchor.href = objectUrl
        anchor.download = fileName ?? 'attachment'
        anchor.click()
        cleanup()
        return
    }

    const popup = window.open(objectUrl, '_blank', 'noopener,noreferrer')
    if (!popup) {
        const anchor = document.createElement('a')
        anchor.href = objectUrl
        anchor.target = '_blank'
        anchor.rel = 'noopener noreferrer'
        anchor.click()
    }

    cleanup()
}
