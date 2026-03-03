import { useNavigate, useLocation } from 'react-router-dom'
import {
    makeStyles,
    tokens,
    Button,
    Title3,
    mergeClasses,
} from '@fluentui/react-components'
import { RocketRegular } from '@fluentui/react-icons'
import { APP_URL } from '../config'

const useStyles = makeStyles({
    nav: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        paddingTop: tokens.spacingVerticalM,
        paddingBottom: tokens.spacingVerticalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        position: 'sticky',
        top: 0,
        zIndex: 100,
        backgroundColor: tokens.colorNeutralBackground1,
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

    return (
        <nav className={styles.nav}>
            <div className={styles.brand} onClick={() => void navigate('/')}>
                <RocketRegular className={styles.brandIcon} />
                <Title3>Fleet</Title3>
            </div>

            <div className={styles.links}>
                {navItems.map((item) => (
                    <Button
                        key={item.path}
                        appearance="subtle"
                        className={mergeClasses(
                            styles.navLink,
                            location.pathname === item.path && styles.navLinkActive,
                        )}
                        onClick={() => void navigate(item.path)}
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
        </nav>
    )
}
