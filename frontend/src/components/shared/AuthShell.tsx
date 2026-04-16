import type { ReactNode } from 'react'
import {
    Card,
    makeStyles,
} from '@fluentui/react-components'
import { appTokens } from '../../styles/appTokens'
import { FleetRocketLogo } from './FleetRocketLogo'

const useStyles = makeStyles({
    root: {
        minHeight: '100dvh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        paddingTop: `max(${appTokens.space.lg}, env(safe-area-inset-top))`,
        paddingRight: appTokens.space.lg,
        paddingBottom: `max(${appTokens.space.lg}, env(safe-area-inset-bottom))`,
        paddingLeft: appTokens.space.lg,
        backgroundImage: `radial-gradient(circle at top left, ${appTokens.color.authGlowA}, transparent 32%), radial-gradient(circle at bottom right, ${appTokens.color.authGlowB}, transparent 28%)`,
        backgroundColor: appTokens.color.pageBackground,
        '@media (max-width: 640px)': {
            paddingTop: `max(${appTokens.space.md}, env(safe-area-inset-top))`,
            paddingRight: appTokens.space.md,
            paddingBottom: `max(${appTokens.space.md}, env(safe-area-inset-bottom))`,
            paddingLeft: appTokens.space.md,
        },
    },
    card: {
        width: 'min(100%, 26rem)',
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.xl,
        borderRadius: appTokens.radius.xl,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        boxShadow: appTokens.shadow.overlay,
        display: 'grid',
        gap: appTokens.space.lg,
        '@media (max-width: 640px)': {
            paddingTop: appTokens.space.lg,
            paddingRight: appTokens.space.lg,
            paddingBottom: appTokens.space.lg,
            paddingLeft: appTokens.space.lg,
        },
    },
    logoRow: {
        display: 'flex',
        justifyContent: 'center',
    },
    footer: {
        display: 'flex',
        justifyContent: 'center',
    },
})

interface AuthShellProps {
    footer?: ReactNode
    children: ReactNode
}

export function AuthShell({ footer, children }: AuthShellProps) {
    const styles = useStyles()

    return (
        <div className={styles.root}>
            <Card className={styles.card}>
                <div className={styles.logoRow}>
                    <FleetRocketLogo size={30} title="Fleet" variant="outline" />
                </div>
                {children}
                {footer ? <div className={styles.footer}>{footer}</div> : null}
            </Card>
        </div>
    )
}
