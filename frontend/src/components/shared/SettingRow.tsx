import { makeStyles, Text, Caption1, Switch } from '@fluentui/react-components'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
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
})

interface SettingRowProps {
    label: string
    description: string
    action?: ReactNode
    /** Shortcut: render a Switch with this checked state */
    switchChecked?: boolean
    onSwitchChange?: (checked: boolean) => void
}

export function SettingRow({ label, description, action, switchChecked, onSwitchChange }: SettingRowProps) {
    const styles = useStyles()

    return (
        <div className={styles.settingRow}>
            <div className={styles.settingInfo}>
                <Text weight="semibold">{label}</Text>
                <Caption1>{description}</Caption1>
            </div>
            {action ?? (
                <Switch
                    checked={switchChecked}
                    onChange={(_e, data) => onSwitchChange?.(data.checked)}
                />
            )}
        </div>
    )
}
