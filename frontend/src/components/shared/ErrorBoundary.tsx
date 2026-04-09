import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button, Text, makeStyles, tokens } from '@fluentui/react-components'
import { ArrowClockwiseRegular, ErrorCircleRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '300px',
        padding: tokens.spacingVerticalXXL,
        gap: tokens.spacingVerticalL,
        textAlign: 'center',
    },
    icon: {
        fontSize: '48px',
        color: tokens.colorPaletteRedForeground1,
    },
    details: {
        maxWidth: '600px',
        padding: tokens.spacingVerticalM,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
        fontFamily: 'monospace',
        fontSize: tokens.fontSizeBase200,
        overflowX: 'auto',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
})

interface ErrorBoundaryProps {
    children: ReactNode
    fallback?: ReactNode
}

interface ErrorBoundaryState {
    hasError: boolean
    error: Error | null
}

function isStaleChunkError(error: Error): boolean {
    const msg = error.message
    return msg.includes('Failed to fetch dynamically imported module')
        || msg.includes('Importing a module script failed')
        || msg.includes('error loading dynamically imported module')
}

class ErrorBoundaryInner extends Component<ErrorBoundaryProps & { classes: ReturnType<typeof useStyles> }, ErrorBoundaryState> {
    constructor(props: ErrorBoundaryProps & { classes: ReturnType<typeof useStyles> }) {
        super(props)
        this.state = { hasError: false, error: null }
    }

    static getDerivedStateFromError(error: Error): ErrorBoundaryState {
        return { hasError: true, error }
    }

    componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
        console.error('ErrorBoundary caught:', error, errorInfo)

        // After a deployment, chunk hashes change. Browsers with stale HTML
        // try to load old chunk URLs that no longer exist. Auto-reload once
        // to pick up the new HTML with correct chunk references.
        if (isStaleChunkError(error)) {
            const key = 'fleet_chunk_reload'
            const lastReload = sessionStorage.getItem(key)
            const now = Date.now()
            // Only auto-reload if we haven't done so in the past 30 seconds
            // to avoid infinite reload loops.
            if (!lastReload || now - Number(lastReload) > 30_000) {
                sessionStorage.setItem(key, String(now))
                window.location.reload()
            }
        }
    }

    handleReset = () => {
        this.setState({ hasError: false, error: null })
    }

    render() {
        if (this.state.hasError) {
            if (this.props.fallback) return this.props.fallback

            const { classes } = this.props
            return (
                <div className={classes.container}>
                    <ErrorCircleRegular className={classes.icon} />
                    <Text size={500} weight="semibold">Something went wrong</Text>
                    <Text size={300}>An unexpected error occurred. Try refreshing the page.</Text>
                    {this.state.error && (
                        <div className={classes.details}>{this.state.error.message}</div>
                    )}
                    <Button
                        appearance="primary"
                        icon={<ArrowClockwiseRegular />}
                        onClick={this.handleReset}
                    >
                        Try Again
                    </Button>
                </div>
            )
        }

        return this.props.children
    }
}

function ErrorBoundary({ children, fallback }: ErrorBoundaryProps) {
    const classes = useStyles()
    return (
        <ErrorBoundaryInner classes={classes} fallback={fallback}>
            {children}
        </ErrorBoundaryInner>
    )
}

export { ErrorBoundary }
