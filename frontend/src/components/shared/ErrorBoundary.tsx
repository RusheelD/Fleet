import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button, Text, makeStyles, tokens } from '@fluentui/react-components'
import { ArrowClockwiseRegular, ErrorCircleRegular } from '@fluentui/react-icons'
import { getUserFacingErrorMessage, isStaleChunkError, tryRecoverFromStaleChunk } from '../../utils/staleChunkRecovery'

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
        tryRecoverFromStaleChunk(error)
    }

    handleReset = () => {
        this.setState({ hasError: false, error: null })
    }

    render() {
        if (this.state.hasError) {
            if (this.props.fallback) return this.props.fallback

            const { classes } = this.props
            const isChunkError = this.state.error ? isStaleChunkError(this.state.error) : false
            const detailMessage = getUserFacingErrorMessage(this.state.error)
            return (
                <div className={classes.container}>
                    <ErrorCircleRegular className={classes.icon} />
                    <Text size={500} weight="semibold">Something went wrong</Text>
                    <Text size={300}>
                        {isChunkError
                            ? 'Fleet refreshed in the background. Reload to pick up the latest version.'
                            : 'An unexpected error occurred. Try refreshing the page.'}
                    </Text>
                    {this.state.error && !isChunkError && (
                        <div className={classes.details}>{detailMessage}</div>
                    )}
                    <Button
                        appearance="primary"
                        icon={<ArrowClockwiseRegular />}
                        onClick={isChunkError ? () => window.location.reload() : this.handleReset}
                    >
                        {isChunkError ? 'Reload Fleet' : 'Try Again'}
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
