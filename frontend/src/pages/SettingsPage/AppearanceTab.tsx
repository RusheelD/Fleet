import {
    makeStyles,
    mergeClasses,
    Title3,
    Card,
    Divider,
} from '@fluentui/react-components'
import { SettingRow } from '../../components/shared'
import { usePreferences, useIsMobile } from '../../hooks'
import type { UserPreferences } from '../../models'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    sectionMobile: {
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
        gap: '0.75rem',
    },
})

export function AppearanceTab() {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const { preferences, updatePreference } = usePreferences()

    if (!preferences) return null

    const toggle = (key: keyof UserPreferences) => (checked: boolean) => {
        updatePreference(key, checked)
    }

    return (
        <Card className={mergeClasses(styles.section, isMobile && styles.sectionMobile)}>
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
                description="Reduce spacing and control density across the entire app"
                switchChecked={preferences.compactMode}
                onSwitchChange={toggle('compactMode')}
            />
            <SettingRow
                label="Sidebar Collapsed"
                description="Collapse or expand the sidebar across the app"
                switchChecked={preferences.sidebarCollapsed}
                onSwitchChange={toggle('sidebarCollapsed')}
            />
        </Card>
    )
}
