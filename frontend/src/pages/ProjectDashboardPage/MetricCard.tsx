import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
    Card,
    ProgressBar,
} from '@fluentui/react-components'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    metricCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        border: appTokens.border.subtle,
        backgroundImage: `linear-gradient(150deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
        boxShadow: appTokens.shadow.card,
    },
    metricCardCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        gap: '0.25rem',
    },
    metricCardMobile: {
        paddingTop: '0.75rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.875rem',
        paddingRight: '0.875rem',
    },
    metricHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        color: appTokens.color.textTertiary,
        minWidth: 0,
    },
    metricIconShell: {
        width: '2rem',
        height: '2rem',
        borderRadius: appTokens.radius.md,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: appTokens.color.surfaceRaised,
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    metricHeaderCompact: {
        gap: '0.375rem',
    },
    metricValue: {
        fontSize: '28px',
        fontWeight: 700,
        lineHeight: 1,
        color: appTokens.color.textPrimary,
    },
    metricValueCompact: {
        fontSize: '18px',
        lineHeight: '20px',
    },
    metricValueMobile: {
        fontSize: '24px',
        lineHeight: '26px',
    },
    metricSubtext: {
        color: appTokens.color.textTertiary,
        fontSize: '12px',
        overflowWrap: 'anywhere',
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
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card
            className={mergeClasses(
                styles.metricCard,
                isCompact && styles.metricCardCompact,
                isMobile && !isCompact && styles.metricCardMobile,
            )}
        >
            <div className={mergeClasses(styles.metricHeader, isCompact && styles.metricHeaderCompact)}>
                <span className={styles.metricIconShell}>{icon}</span>
                <Caption1>{label}</Caption1>
            </div>
            <Text className={mergeClasses(styles.metricValue, isCompact && styles.metricValueCompact, isMobile && !isCompact && styles.metricValueMobile)}>
                {value}
            </Text>
            {progress !== undefined ? (
                <div className={mergeClasses(styles.progressWrapper, isCompact && styles.progressWrapperCompact)}>
                    <ProgressBar value={progress} thickness="large" color="brand" />
                    <Text className={mergeClasses(styles.metricSubtext, isCompact && styles.metricSubtextCompact)}>{subtext}</Text>
                </div>
            ) : (
                <Text className={mergeClasses(styles.metricSubtext, isCompact && styles.metricSubtextCompact)}>{subtext}</Text>
            )}
        </Card>
    )
}
