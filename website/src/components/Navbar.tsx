import { useEffect, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import {
    makeStyles,
    Button,
    Title3,
    mergeClasses,
} from '@fluentui/react-components'
import { NavigationRegular, DismissRegular } from '@fluentui/react-icons'
import { APP_URL } from '../config'
import { useIsMobile } from '../hooks'
import { FleetRocketLogo } from './FleetRocketLogo'
import { appTokens } from '../styles/appTokens'

const useStyles = makeStyles({
    nav: {
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        borderBottom: appTokens.border.subtle,
        position: 'sticky',
        top: 0,
        zIndex: 100,
        backgroundColor: appTokens.color.surface,
        '@media (max-width: 900px)': {
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
            paddingTop: appTokens.space.sm,
            paddingBottom: appTokens.space.sm,
        },
    },
    navTopRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
    },
    brand: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        cursor: 'pointer',
        textDecoration: 'none',
        color: appTokens.color.textPrimary,
    },
    brandIcon: {
        width: '24px',
        height: '24px',
    },
    desktopGroup: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.lg,
        '@media (max-width: 900px)': {
            display: 'none',
        },
    },
    links: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    navLink: {
        fontWeight: appTokens.fontWeight.regular,
        minWidth: 'auto',
    },
    navLinkActive: {
        fontWeight: appTokens.fontWeight.semibold,
        color: appTokens.color.brand,
    },
    actions: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    mobileMenuToggle: {
        display: 'none',
        '@media (max-width: 900px)': {
            display: 'inline-flex',
        },
    },
    mobilePanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        marginTop: appTokens.space.sm,
        borderTop: appTokens.border.subtle,
    },
    mobileLinks: {
        display: 'grid',
        gap: appTokens.space.xs,
    },
    mobileActions: {
        display: 'grid',
        gap: appTokens.space.xs,
        marginTop: appTokens.space.xs,
    },
    mobileButton: {
        justifyContent: 'flex-start',
    },
    mobileActionButton: {
        width: '100%',
    },
})

const navItems = [
    { label: 'Home', path: '/' },
    { label: 'About', path: '/about' },
    { label: 'Pricing', path: '/pricing' },
    { label: 'Contact', path: '/contact' },
]

export function Navbar() {
    const styles = useStyles()
    const navigate = useNavigate()
    const location = useLocation()
    const isMobile = useIsMobile()
    const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)

    useEffect(() => {
        if (!isMobile) {
            setIsMobileMenuOpen(false)
        }
    }, [isMobile])

    useEffect(() => {
        setIsMobileMenuOpen(false)
    }, [location.pathname])

    const navigateTo = (path: string) => {
        setIsMobileMenuOpen(false)
        void navigate(path)
    }

    return (
        <nav className={styles.nav}>
            <div className={styles.navTopRow}>
                <div className={styles.brand} onClick={() => navigateTo('/')}>
                    <FleetRocketLogo className={styles.brandIcon} size={24} title="Fleet" />
                    <Title3>Fleet</Title3>
                </div>

                <div className={styles.desktopGroup}>
                    <div className={styles.links}>
                        {navItems.map((item) => (
                            <Button
                                key={item.path}
                                appearance="subtle"
                                className={mergeClasses(
                                    styles.navLink,
                                    location.pathname === item.path && styles.navLinkActive,
                                )}
                                onClick={() => navigateTo(item.path)}
                            >
                                {item.label}
                            </Button>
                        ))}
                    </div>

                    <div className={styles.actions}>
                        <Button
                            appearance="subtle"
                            as="a"
                            href={`${APP_URL}/login`}
                        >
                            Log in
                        </Button>
                        <Button
                            appearance="primary"
                            as="a"
                            href={`${APP_URL}/signup`}
                        >
                            Sign up free
                        </Button>
                    </div>
                </div>

                {isMobile && (
                    <Button
                        appearance="subtle"
                        icon={isMobileMenuOpen ? <DismissRegular /> : <NavigationRegular />}
                        className={styles.mobileMenuToggle}
                        onClick={() => setIsMobileMenuOpen((open) => !open)}
                        aria-label={isMobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
                    />
                )}
            </div>

            {isMobile && isMobileMenuOpen && (
                <div className={styles.mobilePanel}>
                    <div className={styles.mobileLinks}>
                        {navItems.map((item) => (
                            <Button
                                key={item.path}
                                appearance="subtle"
                                className={mergeClasses(
                                    styles.mobileButton,
                                    location.pathname === item.path && styles.navLinkActive,
                                )}
                                onClick={() => navigateTo(item.path)}
                            >
                                {item.label}
                            </Button>
                        ))}
                    </div>

                    <div className={styles.mobileActions}>
                        <Button
                            appearance="subtle"
                            as="a"
                            href={`${APP_URL}/login`}
                            className={styles.mobileActionButton}
                            onClick={() => setIsMobileMenuOpen(false)}
                        >
                            Log in
                        </Button>
                        <Button
                            appearance="primary"
                            as="a"
                            href={`${APP_URL}/signup`}
                            className={styles.mobileActionButton}
                            onClick={() => setIsMobileMenuOpen(false)}
                        >
                            Sign up free
                        </Button>
                    </div>
                </div>
            )}
        </nav>
    )
}
