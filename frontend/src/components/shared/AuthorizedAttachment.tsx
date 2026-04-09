import { useCallback, useEffect, useState } from 'react'
import type { ImgHTMLAttributes, MouseEvent, ReactNode } from 'react'
import { Link, Spinner, makeStyles, mergeClasses } from '@fluentui/react-components'
import { appTokens } from '../../styles/appTokens'
import { fetchWithAuth } from '../../proxies/proxy'
import { isProtectedChatAttachmentUrl, openProtectedAttachmentUrl } from './chatAttachmentUrl'

const useStyles = makeStyles({
    image: {
        display: 'block',
        maxWidth: '100%',
        height: 'auto',
        borderRadius: appTokens.radius.md,
    },
    imagePlaceholder: {
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '3rem',
        minWidth: '3rem',
        color: appTokens.color.textMuted,
    },
    imageError: {
        color: appTokens.color.textMuted,
        fontSize: appTokens.fontSize.xs,
    },
})

function useAuthorizedObjectUrl(sourceUrl?: string | null) {
    const [objectUrl, setObjectUrl] = useState<string | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [isLoading, setIsLoading] = useState(false)

    useEffect(() => {
        if (!sourceUrl) {
            setObjectUrl(null)
            setError(null)
            setIsLoading(false)
            return
        }

        if (!isProtectedChatAttachmentUrl(sourceUrl)) {
            setObjectUrl(sourceUrl)
            setError(null)
            setIsLoading(false)
            return
        }

        let disposed = false
        let nextObjectUrl: string | null = null
        setIsLoading(true)
        setError(null)

        void (async () => {
            try {
                const response = await fetchWithAuth(sourceUrl)
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`)
                }

                const blob = await response.blob()
                nextObjectUrl = URL.createObjectURL(blob)
                if (!disposed) {
                    setObjectUrl(nextObjectUrl)
                    setIsLoading(false)
                }
            } catch (caught) {
                if (!disposed) {
                    setObjectUrl(null)
                    setIsLoading(false)
                    setError(caught instanceof Error ? caught.message : 'Failed to load attachment.')
                }
            }
        })()

        return () => {
            disposed = true
            if (nextObjectUrl) {
                URL.revokeObjectURL(nextObjectUrl)
            }
        }
    }, [sourceUrl])

    return { objectUrl, error, isLoading }
}

interface AuthorizedAttachmentLinkProps {
    href?: string | null
    children: ReactNode
    className?: string
    downloadName?: string
    mode?: 'open' | 'download'
}

export function AuthorizedAttachmentLink({
    href,
    children,
    className,
    downloadName,
    mode = 'open',
}: AuthorizedAttachmentLinkProps) {
    const handleClick = useCallback(async (event: MouseEvent<HTMLAnchorElement>) => {
        if (!href) {
            event.preventDefault()
            return
        }

        if (!isProtectedChatAttachmentUrl(href)) {
            return
        }

        event.preventDefault()
        await openProtectedAttachmentUrl(href, downloadName, mode)
    }, [downloadName, href, mode])

    return (
        <Link
            className={className}
            href={href ?? '#'}
            target="_blank"
            rel="noopener noreferrer"
            inline
            onClick={(event) => {
                void handleClick(event)
            }}
        >
            {children}
        </Link>
    )
}

interface AuthorizedAttachmentImageProps extends ImgHTMLAttributes<HTMLImageElement> {
    src?: string
}

export function AuthorizedAttachmentImage({ src, alt, className, ...imgProps }: AuthorizedAttachmentImageProps) {
    const styles = useStyles()
    const { objectUrl, error, isLoading } = useAuthorizedObjectUrl(src)

    if (isLoading) {
        return (
            <span className={styles.imagePlaceholder}>
                <Spinner size="tiny" />
            </span>
        )
    }

    if (!objectUrl) {
        if (error) {
            return <span className={styles.imageError}>Attachment unavailable.</span>
        }
        return null
    }

    return (
        <img
            {...imgProps}
            src={objectUrl}
            alt={alt}
            className={mergeClasses(styles.image, className)}
        />
    )
}
