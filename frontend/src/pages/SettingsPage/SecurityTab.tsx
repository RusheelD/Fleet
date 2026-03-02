import {
    makeStyles,
    tokens,
    Title3,
    Caption1,
    Text,
    Card,
    Button,
    Divider,
} from '@fluentui/react-components'
import { DeleteRegular } from '@fluentui/react-icons'

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
    dangerZone: {
        border: `1px solid ${tokens.colorPaletteRedBorder2}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    dangerTitle: {
        color: tokens.colorPaletteRedForeground1,
    },
    dangerItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    deleteButton: {
        backgroundColor: tokens.colorPaletteRedBackground3,
    },
})

export function SecurityTab() {
    const styles = useStyles()

    return (
        <>
            <Card className={styles.section}>
                <Title3>Security</Title3>
                <Divider />
                <div className={styles.settingRow}>
                    <div className={styles.settingInfo}>
                        <Text weight="semibold">Two-Factor Authentication</Text>
                        <Caption1>Add an extra layer of security to your account</Caption1>
                    </div>
                    <Button appearance="outline" size="small">Enable</Button>
                </div>
                <div className={styles.settingRow}>
                    <div className={styles.settingInfo}>
                        <Text weight="semibold">Active Sessions</Text>
                        <Caption1>Manage your active login sessions</Caption1>
                    </div>
                    <Button appearance="outline" size="small">View Sessions</Button>
                </div>
            </Card>
            <div className={styles.dangerZone}>
                <Title3 className={styles.dangerTitle}>Danger Zone</Title3>
                <div className={styles.dangerItem}>
                    <div className={styles.settingInfo}>
                        <Text weight="semibold">Delete Account</Text>
                        <Caption1>Permanently delete your Fleet account and all data</Caption1>
                    </div>
                    <Button appearance="primary" icon={<DeleteRegular />} className={styles.deleteButton}>
                        Delete Account
                    </Button>
                </div>
            </div>
        </>
    )
}
