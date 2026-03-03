import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
    makeStyles,
    Spinner,
    Text,
    Card,
    tokens,
    Button,
} from '@fluentui/react-components'
import { ErrorCircleRegular, CheckmarkCircleRegular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { linkGitHub } from '../../proxies'

// Module-level set survives React strict mode remounts.
// Prevents the single-use OAuth code from being sent twice.
const processedCodes = new Set<string>()

const useStyles = makeStyles({
    container: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100vh',
        padding: '2rem',
    },
    card: {
        padding: '2rem',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '1rem',
        maxWidth: '400px',
        width: '100%',
    },
    icon: {
        fontSize: '48px',
    },
    successIcon: {
        color: tokens.colorPaletteGreenForeground1,
    },
    errorIcon: {
        color: tokens.colorPaletteRedForeground1,
    },
})

export function GitHubCallbackPage() {
    const styles = useStyles()
    const [searchParams] = useSearchParams()
    const navigate = useNavigate()
    const queryClient = useQueryClient()
    const [status, setStatus] = useState<'loading' | 'success' | 'error'>(
        searchParams.get('error') ? 'error' : 'loading'
    )
    const [errorMessage, setErrorMessage] = useState(
        searchParams.get('error_description') ?? (searchParams.get('error') ? 'GitHub authorization was denied.' : '')
    )

    useEffect(() => {
        const code = searchParams.get('code')
        if (!code || searchParams.get('error') || processedCodes.has(code)) return
        processedCodes.add(code)

        const origin = window.location.origin
        const normalizedOrigin = origin.includes('localhost') ? 'http://localhost:5250' : origin
        const redirectUri = `${normalizedOrigin}/auth/github/callback`

        // Call the proxy function directly — no hooks, no React lifecycle issues.
        linkGitHub(code, redirectUri)
            .then(() => {
                // Invalidate user-settings so the rest of the app knows GitHub is connected
                void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
                setStatus('success')
                setTimeout(() => navigate('/projects', { replace: true }), 1500)
            })
            .catch((err: unknown) => {
                setStatus('error')
                setErrorMessage(err instanceof Error ? err.message : 'Failed to link GitHub account.')
            })
        // eslint-disable-next-line react-hooks/exhaustive-deps -- run once on mount only
    }, [])

    return (
        <div className={styles.container}>
            <Card className={styles.card}>
                {status === 'loading' && (
                    <>
                        <Spinner size="large" />
                        <Text size={400} weight="semibold">Linking GitHub account...</Text>
                        <Text size={300}>Please wait while we connect your GitHub account.</Text>
                    </>
                )}
                {status === 'success' && (
                    <>
                        <CheckmarkCircleRegular className={`${styles.icon} ${styles.successIcon}`} />
                        <Text size={400} weight="semibold">GitHub Connected!</Text>
                        <Text size={300}>Redirecting to projects...</Text>
                    </>
                )}
                {status === 'error' && (
                    <>
                        <ErrorCircleRegular className={`${styles.icon} ${styles.errorIcon}`} />
                        <Text size={400} weight="semibold">Connection Failed</Text>
                        <Text size={300}>{errorMessage}</Text>
                        <Button
                            appearance="primary"
                            onClick={() => navigate('/settings?tab=connections', { replace: true })}
                        >
                            Back to Settings
                        </Button>
                    </>
                )}
            </Card>
        </div>
    )
}
