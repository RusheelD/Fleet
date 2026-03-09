import {
    makeStyles,
    mergeClasses,
    tokens,
    Title3,
    Caption1,
    Text,
    Card,
    Divider,
    Badge,
} from '@fluentui/react-components'
import { ShieldKeyholeRegular, LockClosedRegular, PersonRegular } from '@fluentui/react-icons'
import { useIsMobile } from '../../hooks'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
        '@media (max-width: 900px)': {
            paddingTop: '0.875rem',
            paddingBottom: '0.875rem',
            paddingLeft: '0.75rem',
            paddingRight: '0.75rem',
            gap: '0.75rem',
        },
    },
    settingRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.5rem 0',
        gap: '0.75rem',
    },
    settingRowMobile: {
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: '0.5rem',
    },
    settingInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
    },
    infoCard: {
        padding: '1rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    infoCardMobile: {
        alignItems: 'flex-start',
        paddingTop: '0.75rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
    },
    infoIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    managedBadge: {
        flexShrink: 0,
    },
})

export function SecurityTab() {
    const styles = useStyles()
    const isMobile = useIsMobile()

    return (
        <Card className={styles.section}>
            <Title3>Security</Title3>
            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <ShieldKeyholeRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Secured by Microsoft Entra ID</Text>
                    <Caption1>Your account is protected by your organization&apos;s identity provider. Multi-factor authentication and session management are handled by Entra ID.</Caption1>
                </div>
            </div>

            <div className={mergeClasses(styles.settingRow, isMobile && styles.settingRowMobile)}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Two-Factor Authentication</Text>
                    <Caption1>Managed by your Entra ID administrator</Caption1>
                </div>
                <Badge appearance="tint" color="informative" className={styles.managedBadge}>Managed by Entra ID</Badge>
            </div>

            <div className={mergeClasses(styles.settingRow, isMobile && styles.settingRowMobile)}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Active Sessions</Text>
                    <Caption1>View and manage sessions via your Microsoft account</Caption1>
                </div>
                <Badge appearance="tint" color="informative" className={styles.managedBadge}>Managed by Entra ID</Badge>
            </div>

            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <LockClosedRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Data &amp; Privacy</Text>
                    <Caption1>Contact your workspace administrator for account deletion or data export requests.</Caption1>
                </div>
            </div>

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <PersonRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Social Sign-In</Text>
                    <Caption1>You can also sign in using Google or GitHub through Entra ID. These are configured by your administrator.</Caption1>
                </div>
            </div>
        </Card>
    )
}
