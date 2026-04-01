import {
    makeStyles,
    mergeClasses,
    Title3,
    Caption1,
    Text,
    Card,
    Divider,
    Badge,
} from '@fluentui/react-components'
import { ShieldKeyholeRegular, LockClosedRegular, PersonRegular } from '@fluentui/react-icons'
import { useIsMobile } from '../../hooks'
import { APP_MOBILE_MEDIA_QUERY, appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    section: {
        padding: `calc(${appTokens.space.lg} + ${appTokens.space.xxs})`,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.lg,
        [APP_MOBILE_MEDIA_QUERY]: {
            paddingTop: appTokens.space.pageYMobile,
            paddingBottom: appTokens.space.pageYMobile,
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
            gap: appTokens.space.md,
        },
    },
    settingRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        gap: appTokens.space.md,
    },
    settingRowMobile: {
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: appTokens.space.sm,
    },
    settingInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    infoCard: {
        padding: appTokens.space.lg,
        backgroundColor: appTokens.color.pageBackground,
        borderRadius: appTokens.radius.md,
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
    },
    infoCardMobile: {
        alignItems: 'flex-start',
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    infoIcon: {
        fontSize: appTokens.fontSize.iconLg,
        color: appTokens.color.brand,
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
