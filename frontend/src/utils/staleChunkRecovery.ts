import { lazy, type ComponentType, type LazyExoticComponent } from 'react'

const RELOAD_SESSION_KEY = 'fleet_chunk_reload'
const RELOAD_COOLDOWN_MS = 30_000
const STALE_CHUNK_PATTERNS = [
    'failed to fetch dynamically imported module',
    'importing a module script failed',
    'error loading dynamically imported module',
    'dynamically imported module',
]

function getErrorMessage(error: unknown): string {
    if (typeof error === 'string') {
        return error
    }

    if (error && typeof error === 'object') {
        const errorWithMessage = error as { message?: unknown; reason?: unknown }

        if (typeof errorWithMessage.message === 'string') {
            return errorWithMessage.message
        }

        if (typeof errorWithMessage.reason !== 'undefined') {
            return getErrorMessage(errorWithMessage.reason)
        }
    }

    return ''
}

export function isStaleChunkError(error: unknown): boolean {
    const message = getErrorMessage(error).toLowerCase()
    return STALE_CHUNK_PATTERNS.some((pattern) => message.includes(pattern))
}

function shouldReloadForStaleChunk(): boolean {
    if (typeof window === 'undefined') {
        return false
    }

    const lastReload = window.sessionStorage.getItem(RELOAD_SESSION_KEY)
    const now = Date.now()

    if (lastReload && now - Number(lastReload) <= RELOAD_COOLDOWN_MS) {
        return false
    }

    window.sessionStorage.setItem(RELOAD_SESSION_KEY, String(now))
    return true
}

export function tryRecoverFromStaleChunk(error: unknown): boolean {
    if (!isStaleChunkError(error) || typeof window === 'undefined') {
        return false
    }

    if (!shouldReloadForStaleChunk()) {
        return false
    }

    window.location.reload()
    return true
}

export function getUserFacingErrorMessage(error: Error | null): string {
    if (!error) {
        return 'An unexpected error occurred.'
    }

    if (isStaleChunkError(error)) {
        return 'Fleet updated in the background. Reload the page to continue with the latest version.'
    }

    return error.message || 'An unexpected error occurred.'
}

export function installStaleChunkRecovery(): () => void {
    if (typeof window === 'undefined') {
        return () => undefined
    }

    const handleWindowError = (event: ErrorEvent) => {
        const errorCandidate = event.error ?? event.message

        if (tryRecoverFromStaleChunk(errorCandidate)) {
            event.preventDefault()
        }
    }

    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
        if (tryRecoverFromStaleChunk(event.reason)) {
            event.preventDefault()
        }
    }

    window.addEventListener('error', handleWindowError)
    window.addEventListener('unhandledrejection', handleUnhandledRejection)

    return () => {
        window.removeEventListener('error', handleWindowError)
        window.removeEventListener('unhandledrejection', handleUnhandledRejection)
    }
}

export function lazyWithRetry<T extends ComponentType<unknown>>(
    importer: () => Promise<{ default: T }>,
): LazyExoticComponent<T> {
    return lazy(async () => {
        try {
            return await importer()
        } catch (error) {
            if (tryRecoverFromStaleChunk(error)) {
                return await new Promise<never>(() => undefined)
            }

            throw error
        }
    })
}

export function lazyDialog<TProps extends object>(
    importer: () => Promise<{ default: ComponentType<TProps> }>,
): LazyExoticComponent<ComponentType<TProps>> {
    return lazyWithRetry(
        importer as unknown as () => Promise<{ default: ComponentType<unknown> }>,
    ) as LazyExoticComponent<ComponentType<TProps>>
}
