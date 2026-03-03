import {
    makeStyles,
    Title3,
    Card,
    Divider,
} from '@fluentui/react-components'
import { SettingRow } from '../../components/shared'
import { usePreferences } from '../../hooks'
import type { UserPreferences } from '../../models'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
})

export function NotificationsTab() {
    const styles = useStyles()
    const { preferences, updatePreference } = usePreferences()

    if (!preferences) return null

    const toggle = (key: keyof UserPreferences) => (checked: boolean) => {
        updatePreference(key, checked)
    }

    return (
        <Card className={styles.section}>
            <Title3>Notification Preferences</Title3>
            <Divider />
            <SettingRow
                label="Agent Completed"
                description="Notify when an agent finishes a task"
                switchChecked={preferences.agentCompletedNotification}
                onSwitchChange={toggle('agentCompletedNotification')}
            />
            <SettingRow
                label="PR Opened"
                description="Notify when agents open a pull request"
                switchChecked={preferences.prOpenedNotification}
                onSwitchChange={toggle('prOpenedNotification')}
            />
            <SettingRow
                label="Agent Errors"
                description="Notify when an agent encounters an error"
                switchChecked={preferences.agentErrorsNotification}
                onSwitchChange={toggle('agentErrorsNotification')}
            />
            <SettingRow
                label="Work Item Updates"
                description="Notify when work item status changes"
                switchChecked={preferences.workItemUpdatesNotification}
                onSwitchChange={toggle('workItemUpdatesNotification')}
            />
        </Card>
    )
}
