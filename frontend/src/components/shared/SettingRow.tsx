import { makeStyles, mergeClasses, Text, Caption1, Switch } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { useIsMobile } from '../../hooks'

const useStyles = makeStyles({
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
        minWidth: 0,
    },
    switchMobile: {
        alignSelf: 'flex-end',
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
    const isMobile = useIsMobile()

    return (
        <div className={mergeClasses(styles.settingRow, isMobile && styles.settingRowMobile)}>
            <div className={styles.settingInfo}>
                <Text weight="semibold">{label}</Text>
                <Caption1>{description}</Caption1>
            </div>
            {action ?? (
                <Switch
                    className={mergeClasses(isMobile && styles.switchMobile)}
                    checked={switchChecked}
                    onChange={(_e, data) => onSwitchChange?.(data.checked)}
                />
            )}
        </div>
    )
}
