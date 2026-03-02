import {
    makeStyles,
    Caption1,
    Text,
    Card,
    mergeClasses,
} from '@fluentui/react-components'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    summaryCard: {
        padding: '0.75rem 1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: '160px',
    },
    summaryIcon: {
        fontSize: '24px',
    },
    summaryValue: {
        fontSize: '20px',
        fontWeight: 700,
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

    return (
        <Card className={styles.summaryCard}>
            <span className={mergeClasses(styles.summaryIcon, iconClassName)}>
                {icon}
            </span>
            <div>
                <Text className={styles.summaryValue}>{value}</Text>
                <Caption1 className={styles.captionBlock}>{label}</Caption1>
            </div>
        </Card>
    )
}
