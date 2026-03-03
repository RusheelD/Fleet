import { Navigate, Outlet } from 'react-router-dom'
import { useMsal } from '@azure/msal-react'
import { InteractionStatus } from '@azure/msal-browser'
import { Spinner, makeStyles, tokens } from '@fluentui/react-components'
import { useAuth } from '../../hooks'

const useStyles = makeStyles({
    loading: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        padding: tokens.spacingVerticalXXL,
    },
})

export function ProtectedRoute() {
    const styles = useStyles()
    const { isAuthenticated, isLoading } = useAuth()
    const { inProgress } = useMsal()

    // While MSAL is processing a redirect or acquiring a token, show loading
    if (inProgress !== InteractionStatus.None || isLoading) {
        return (
            <div className={styles.loading}>
                <Spinner label="Authenticating..." size="large" />
            </div>
        )
    }

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />
    }

    return <Outlet />
}
