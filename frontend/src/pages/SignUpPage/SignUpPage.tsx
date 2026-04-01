import {
    makeStyles,
    Card,
    CardHeader,
    Button,
    Title3,
    Body1,
    Caption1,
    Spinner,
    Divider,
    Link,
} from '@fluentui/react-components'
import { useAuth } from '../../hooks'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { InteractionStatus } from '@azure/msal-browser'
import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    root: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100dvh',
        padding: appTokens.space.xxl,
        backgroundImage: `radial-gradient(circle at top left, ${appTokens.color.authGlowA}, transparent 38%), radial-gradient(circle at bottom right, ${appTokens.color.authGlowB}, transparent 34%)`,
        backgroundColor: appTokens.color.pageBackground,
        overflow: 'auto',
        '@media (max-width: 900px)': {
            alignItems: 'flex-start',
            paddingTop: appTokens.space.xl,
            paddingBottom: appTokens.space.xl,
            paddingLeft: appTokens.space.lg,
            paddingRight: appTokens.space.lg,
        },
    },
    card: {
        width: '100%',
        maxWidth: '420px',
        padding: appTokens.space.xl,
        borderRadius: appTokens.radius.xl,
        boxShadow: appTokens.shadow.overlay,
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: appTokens.color.border,
        borderRightColor: appTokens.color.border,
        borderBottomColor: appTokens.color.border,
        borderLeftColor: appTokens.color.border,
        backgroundColor: appTokens.color.surface,
        '@media (max-width: 900px)': {
            paddingTop: appTokens.space.md,
            paddingBottom: appTokens.space.md,
            marginTop: appTokens.space.sm,
        },
    },
    header: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.sm,
        marginBottom: appTokens.space.md,
        textAlign: 'center',
    },
    actions: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        alignItems: 'stretch',
    },
    dividerRow: {
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
    },
    footer: {
        textAlign: 'center',
        paddingTop: appTokens.space.sm,
    },
    configError: {
        color: appTokens.color.danger,
        textAlign: 'center',
    },
    providerIcon: {
        width: '20px',
        height: '20px',
    },
})

function MicrosoftIcon({ className }: { className?: string }) {
    return (
        <svg className={className} viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" fill="none">
            <rect x="2.5" y="5" width="19" height="14" rx="2" stroke="currentColor" strokeWidth="1.8" />
            <path d="M4 7l8 6 8-6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    )
}

function GoogleIcon({ className }: { className?: string }) {
    return (
        <svg className={className} viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill={appTokens.color.googleBlue} />
            <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill={appTokens.color.googleGreen} />
            <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill={appTokens.color.googleYellow} />
            <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill={appTokens.color.googleRed} />
        </svg>
    )
}

export function SignUpPage() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { authConfigError, isAuthConfigured, signUp } = useAuth()
    const isAuthenticated = useIsAuthenticated()
    const { inProgress } = useMsal()

    useEffect(() => {
        if (isAuthenticated) {
            navigate('/projects', { replace: true })
        }
    }, [isAuthenticated, navigate])

    const isLoading = inProgress !== InteractionStatus.None
    const authDisabled = isLoading || !isAuthConfigured

    return (
        <div className={styles.root}>
            <Card className={styles.card}>
                <CardHeader
                    header={
                        <div className={styles.header}>
                            <Title3>Create your Fleet account</Title3>
                            <Body1>Choose email or Google to get started. It&apos;s free.</Body1>
                        </div>
                    }
                />
                <div className={styles.actions}>
                    {isLoading ? (
                        <Spinner label="Setting up your account..." />
                    ) : (
                        <>
                            <Button
                                appearance="primary"
                                size="large"
                                icon={<MicrosoftIcon className={styles.providerIcon} />}
                                disabled={authDisabled}
                                onClick={() => void signUp('email')}
                            >
                                Sign up with email
                            </Button>
                            <Button
                                appearance="secondary"
                                size="large"
                                icon={<GoogleIcon className={styles.providerIcon} />}
                                disabled={authDisabled}
                                onClick={() => void signUp('google')}
                            >
                                Sign up with Google
                            </Button>
                            {authConfigError && (
                                <Caption1 className={styles.configError}>
                                    {authConfigError}
                                </Caption1>
                            )}
                            <div className={styles.dividerRow}>
                                <Divider />
                            </div>
                            <Body1 align="center" className={styles.footer}>
                                Already have an account?{' '}
                                <Link onClick={() => void navigate('/login')}>Log in</Link>
                            </Body1>
                        </>
                    )}
                </div>
            </Card>
        </div>
    )
}
