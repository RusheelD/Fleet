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

export function AppearanceTab() {
    const styles = useStyles()

    return (
        <Card className={styles.section}>
            <Title3>Appearance</Title3>
            <Divider />
            <SettingRow
                label="Dark Mode"
                description="Use dark theme (follows system preference by default)"
                switchChecked
            />
            <SettingRow
                label="Compact Mode"
                description="Reduce spacing in lists and boards"
            />
            <SettingRow
                label="Sidebar Collapsed by Default"
                description="Start with collapsed sidebar on page load"
            />
        </Card>
    )
}
