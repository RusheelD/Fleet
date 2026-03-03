import {
    makeStyles,
    tokens,
    Title3,
    Caption1,
    Text,
    Card,
    Divider,
    Badge,
} from '@fluentui/react-components'
import { ShieldKeyholeRegular, LockClosedRegular, PersonRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    settingRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.5rem 0',
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
    infoIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
})

export function SecurityTab() {
    const styles = useStyles()

    return (
        <Card className={styles.section}>
            <Title3>Security</Title3>
            <Divider />

            <div className={styles.infoCard}>
                <ShieldKeyholeRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Secured by Microsoft Entra ID</Text>
                    <Caption1>Your account is protected by your organization&apos;s identity provider. Multi-factor authentication and session management are handled by Entra ID.</Caption1>
                </div>
            </div>

            <div className={styles.settingRow}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Two-Factor Authentication</Text>
                    <Caption1>Managed by your Entra ID administrator</Caption1>
                </div>
                <Badge appearance="tint" color="informative">Managed by Entra ID</Badge>
            </div>

            <div className={styles.settingRow}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Active Sessions</Text>
                    <Caption1>View and manage sessions via your Microsoft account</Caption1>
                </div>
                <Badge appearance="tint" color="informative">Managed by Entra ID</Badge>
            </div>

            <Divider />

            <div className={styles.infoCard}>
                <LockClosedRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Data &amp; Privacy</Text>
                    <Caption1>Contact your workspace administrator for account deletion or data export requests.</Caption1>
                </div>
            </div>

            <div className={styles.infoCard}>
                <PersonRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Social Sign-In</Text>
                    <Caption1>You can also sign in using Google or GitHub through Entra ID. These are configured by your administrator.</Caption1>
                </div>
            </div>
        </Card>
    )
}
