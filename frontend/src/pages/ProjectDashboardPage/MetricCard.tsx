import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Text,
    Card,
    ProgressBar,
} from '@fluentui/react-components'
import { usePreferences } from '../../hooks'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    metricCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    metricCardCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        gap: '0.25rem',
    },
    metricHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        color: tokens.colorNeutralForeground3,
    },
    metricHeaderCompact: {
        gap: '0.375rem',
    },
    metricValue: {
        fontSize: '28px',
        fontWeight: 700,
        lineHeight: 1,
        color: tokens.colorNeutralForeground1,
    },
    metricValueCompact: {
        fontSize: '18px',
        lineHeight: '20px',
    },
    metricSubtext: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
    },
    metricSubtextCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    progressWrapper: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        marginTop: '0.5rem',
    },
    progressWrapperCompact: {
        marginTop: '0.25rem',
        gap: '0.25rem',
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
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card className={mergeClasses(styles.metricCard, isCompact && styles.metricCardCompact)}>
            <div className={mergeClasses(styles.metricHeader, isCompact && styles.metricHeaderCompact)}>
                {icon}
                <Caption1>{label}</Caption1>
            </div>
            <Text className={mergeClasses(styles.metricValue, isCompact && styles.metricValueCompact)}>{value}</Text>
            {progress !== undefined ? (
                <div className={mergeClasses(styles.progressWrapper, isCompact && styles.progressWrapperCompact)}>
                    <ProgressBar value={progress} thickness="large" color="brand" />
                </div>
            ) : (
                <Text className={mergeClasses(styles.metricSubtext, isCompact && styles.metricSubtextCompact)}>{subtext}</Text>
            )}
        </Card>
    )
}
