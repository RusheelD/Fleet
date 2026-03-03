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

export function AppearanceTab() {
    const styles = useStyles()
    const { preferences, updatePreference } = usePreferences()

    if (!preferences) return null

    const toggle = (key: keyof UserPreferences) => (checked: boolean) => {
        updatePreference(key, checked)
    }

    return (
        <Card className={styles.section}>
            <Title3>Appearance</Title3>
            <Divider />
            <SettingRow
                label="Dark Mode"
                description="Use dark theme (follows system preference by default)"
                switchChecked={preferences.darkMode}
                onSwitchChange={toggle('darkMode')}
            />
            <SettingRow
                label="Compact Mode"
                description="Reduce spacing in lists and boards"
                switchChecked={preferences.compactMode}
                onSwitchChange={toggle('compactMode')}
            />
            <SettingRow
                label="Sidebar Collapsed by Default"
                description="Start with collapsed sidebar on page load"
                switchChecked={preferences.sidebarCollapsed}
                onSwitchChange={toggle('sidebarCollapsed')}
            />
        </Card>
    )
}
