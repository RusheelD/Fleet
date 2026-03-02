import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Card,
    ProgressBar,
} from '@fluentui/react-components'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    metricCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    metricHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        color: tokens.colorNeutralForeground3,
    },
    metricValue: {
        fontSize: '28px',
        fontWeight: 700,
        lineHeight: 1,
        color: tokens.colorNeutralForeground1,
    },
    metricSubtext: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
    },
    progressWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        marginTop: '0.5rem',
    },
})

interface MetricCardProps {
    icon: ReactNode
    label: string
    value: string | number
    subtext: string
    progress?: number
}

export function MetricCard({ icon, label, value, subtext, progress }: MetricCardProps) {
    const styles = useStyles()

    return (
        <Card className={styles.metricCard}>
            <div className={styles.metricHeader}>
                {icon}
                <Caption1>{label}</Caption1>
            </div>
            <Text className={styles.metricValue}>{value}</Text>
            {progress !== undefined ? (
                <div className={styles.progressWrapper}>
                    <ProgressBar value={progress} thickness="large" color="brand" />
                </div>
            ) : (
                <Text className={styles.metricSubtext}>{subtext}</Text>
            )}
        </Card>
    )
}
