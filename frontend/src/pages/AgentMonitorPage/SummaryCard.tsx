import {
    makeStyles,
    Caption1,
    Text,
    Card,
    mergeClasses,
} from '@fluentui/react-components'
import { usePreferences } from '../../hooks'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    summaryCard: {
        padding: '0.75rem 1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: '160px',
    },
    summaryCardCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
        minWidth: '120px',
    },
    summaryIcon: {
        fontSize: '24px',
    },
    summaryIconCompact: {
        fontSize: '16px',
    },
    summaryValue: {
        fontSize: '20px',
        fontWeight: 700,
    },
    summaryValueCompact: {
        fontSize: '15px',
        lineHeight: '16px',
    },
    labelCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    captionBlock: {
        display: 'block' as const,
    },
})

interface SummaryCardProps {
    icon: ReactNode
    iconClassName?: string
    value: number
    label: string
}

export function SummaryCard({ icon, iconClassName, value, label }: SummaryCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card className={mergeClasses(styles.summaryCard, isCompact && styles.summaryCardCompact)}>
            <span className={mergeClasses(styles.summaryIcon, isCompact && styles.summaryIconCompact, iconClassName)}>
                {icon}
            </span>
            <div>
                <Text className={mergeClasses(styles.summaryValue, isCompact && styles.summaryValueCompact)}>{value}</Text>
                <Caption1 className={mergeClasses(styles.captionBlock, isCompact && styles.labelCompact)}>{label}</Caption1>
            </div>
        </Card>
    )
}
