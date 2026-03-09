import { useNavigate } from 'react-router-dom'
import {
    makeStyles,
    tokens,
    Body1,
    Text,
    Link,
    Divider,
    Caption1,
} from '@fluentui/react-components'
import { RocketRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    footer: {
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        paddingTop: tokens.spacingVerticalXXL,
        paddingBottom: tokens.spacingVerticalL,
        backgroundColor: tokens.colorNeutralBackground2,
        '@media (max-width: 900px)': {
            paddingLeft: tokens.spacingHorizontalM,
            paddingRight: tokens.spacingHorizontalM,
            paddingTop: tokens.spacingVerticalXL,
        },
    },
    grid: {
        display: 'grid',
        gridTemplateColumns: '2fr 1fr 1fr 1fr',
        gap: tokens.spacingHorizontalXXXL,
        marginBottom: tokens.spacingVerticalXL,
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
            gap: tokens.spacingVerticalL,
        },
    },
    brandCol: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    brandRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorBrandForeground1,
    },
    linkCol: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    colTitle: {
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalXS,
    },
    bottom: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: tokens.spacingVerticalM,
        '@media (max-width: 768px)': {
            flexDirection: 'column',
            gap: tokens.spacingVerticalS,
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
                        <RocketRegular fontSize={20} />
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

            <Divider />

            <div className={styles.bottom}>
                <Caption1>&copy; {new Date().getFullYear()} Fleet. All rights reserved.</Caption1>
                <Caption1>Built with AI, for AI-powered teams.</Caption1>
            </div>
        </footer>
    )
}
