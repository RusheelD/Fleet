import { useEffect, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import {
    makeStyles,
    tokens,
    Button,
    Title3,
    mergeClasses,
} from '@fluentui/react-components'
import { RocketRegular, NavigationRegular, DismissRegular } from '@fluentui/react-icons'
import { APP_URL } from '../config'
import { useIsMobile } from '../hooks'

const useStyles = makeStyles({
    nav: {
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        position: 'sticky',
        top: 0,
        zIndex: 100,
        backgroundColor: tokens.colorNeutralBackground1,
        '@media (max-width: 900px)': {
            paddingLeft: tokens.spacingHorizontalM,
            paddingRight: tokens.spacingHorizontalM,
            paddingTop: tokens.spacingVerticalS,
            paddingBottom: tokens.spacingVerticalS,
        },
    },
    navTopRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalM,
    },
    brand: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        cursor: 'pointer',
        textDecoration: 'none',
        color: 'inherit',
    },
    brandIcon: {
        color: tokens.colorBrandForeground1,
        fontSize: '24px',
    },
    desktopGroup: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
        '@media (max-width: 900px)': {
            display: 'none',
        },
    },
    links: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    navLink: {
        fontWeight: tokens.fontWeightRegular,
        minWidth: 'auto',
    },
    navLinkActive: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorBrandForeground1,
    },
    actions: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
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
        gap: tokens.spacingVerticalS,
        paddingTop: tokens.spacingVerticalS,
        marginTop: tokens.spacingVerticalS,
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    mobileLinks: {
        display: 'grid',
        gap: tokens.spacingVerticalXS,
    },
    mobileActions: {
        display: 'grid',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalXS,
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
                    <RocketRegular className={styles.brandIcon} />
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
