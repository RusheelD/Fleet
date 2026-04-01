import {
    makeStyles,
    Body1,
    Caption1,
} from '@fluentui/react-components'
import { ClockRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    activityItem: {
        display: 'flex',
        gap: appTokens.space.md,
        alignItems: 'flex-start',
    },
    activityIcon: {
        marginTop: '2px',
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    activityContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        flex: 1,
    },
    clockSmallIcon: {
        fontSize: appTokens.fontSize.xxs,
        marginRight: appTokens.space.xxs,
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
