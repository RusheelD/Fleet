import {
    makeStyles,
    tokens,
    Body1,
    Caption1,
} from '@fluentui/react-components'
import { ClockRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    activityItem: {
        display: 'flex',
        gap: '0.75rem',
        alignItems: 'flex-start',
    },
    activityIcon: {
        marginTop: '2px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    activityContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
        flex: 1,
    },
    clockSmallIcon: {
        fontSize: '10px',
        marginRight: '0.25rem',
    },
})

interface ActivityItemProps {
    icon: ReactNode
    text: string
    time: string
}

export function ActivityItem({ icon, text, time }: ActivityItemProps) {
    const styles = useStyles()

    return (
        <div className={styles.activityItem}>
            <span className={styles.activityIcon}>{icon}</span>
            <div className={styles.activityContent}>
                <Body1>{text}</Body1>
                <Caption1>
                    <ClockRegular className={styles.clockSmallIcon} />
                    {time}
                </Caption1>
            </div>
        </div>
    )
}
