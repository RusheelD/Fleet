import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    Body1,
    Text,
    Link,
    Divider,
    Caption1,
} from '@fluentui/react-components'
import { FleetRocketLogo } from './FleetRocketLogo'
import { appTokens } from '../styles/appTokens'

const useStyles = makeStyles({
    footer: {
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        paddingTop: appTokens.space.xxl,
        paddingBottom: appTokens.space.lg,
        backgroundColor: appTokens.color.surfaceAlt,
        '@media (max-width: 900px)': {
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
            paddingTop: appTokens.space.xl,
        },
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 1fr 1fr',
        gap: appTokens.space.xxxl,
        marginBottom: appTokens.space.xl,
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
            gap: appTokens.space.lg,
        },
    },
    brandCol: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    brandRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        color: appTokens.color.brand,
    },
    linkCol: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
    },
    colTitle: {
        fontWeight: appTokens.fontWeight.semibold,
        marginBottom: appTokens.space.xs,
    },
    bottom: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: appTokens.space.md,
        '@media (max-width: 768px)': {
            flexDirection: 'column',
            gap: appTokens.space.sm,
            alignItems: 'flex-start',
        },
    },
})

export function Footer() {
    const styles = useStyles()
    const navigate = useNavigate()

    return (
        <footer className={styles.footer}>
            <div className={styles.grid}>
                <div className={styles.brandCol}>
                    <div className={styles.brandRow}>
                        <FleetRocketLogo size={20} title="Fleet" />
                        <Text weight="semibold">Fleet</Text>
                    </div>
                    <Body1>
                        AI-powered project management that helps your team ship faster.
                    </Body1>
                </div>

                <div className={styles.linkCol}>
                    <Body1 className={styles.colTitle}>Product</Body1>
                    <Link onClick={() => void navigate('/')}>Home</Link>
                    <Link onClick={() => void navigate('/about')}>About</Link>
                    <Link onClick={() => void navigate('/pricing')}>Pricing</Link>
                </div>

                <div className={styles.linkCol}>
                    <Body1 className={styles.colTitle}>Company</Body1>
                    <Link onClick={() => void navigate('/about')}>Team</Link>
                    <Link onClick={() => void navigate('/contact')}>Contact</Link>
                </div>

                <div className={styles.linkCol}>
                    <Body1 className={styles.colTitle}>Legal</Body1>
                    <Link>Privacy Policy</Link>
                    <Link>Terms of Service</Link>
                </div>
            </div>

            <Divider style={{ borderColor: appTokens.color.border }} />

            <div className={styles.bottom}>
                <Caption1>&copy; {new Date().getFullYear()} Fleet. All rights reserved.</Caption1>
                <Caption1>Built with AI, for AI-powered teams.</Caption1>
            </div>
        </footer>
    )
}
