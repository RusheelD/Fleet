import {
    makeStyles,
    Title3,
    Card,
    Divider,
} from '@fluentui/react-components'
import { SettingRow } from '../../components/shared'

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

    return (
        <Card className={styles.section}>
            <Title3>Notification Preferences</Title3>
            <Divider />
            <SettingRow
                label="Agent Completed"
                description="Notify when an agent finishes a task"
                switchChecked
            />
            <SettingRow
                label="PR Opened"
                description="Notify when agents open a pull request"
                switchChecked
            />
            <SettingRow
                label="Agent Errors"
                description="Notify when an agent encounters an error"
                switchChecked
            />
            <SettingRow
                label="Work Item Updates"
                description="Notify when work item status changes"
            />
        </Card>
    )
}
