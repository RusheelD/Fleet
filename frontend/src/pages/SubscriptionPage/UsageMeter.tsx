import {
    makeStyles,
    mergeClasses,
    Caption1,
    Card,
    ProgressBar,
    Text,
} from '@fluentui/react-components'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    usageCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.pageBackground,
    },
    usageCardCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        gap: '0.375rem',
    },
    usageHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '0.5rem',
        flexWrap: 'wrap',
    },
    usageCaptionCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    usageValue: {
        fontSize: appTokens.fontSize.lg,
        fontWeight: appTokens.fontWeight.bold,
        lineHeight: appTokens.lineHeight.snug,
    },
})

interface UsageMeterProps {
    label: string
    usage: string
    value: number
    color: 'brand' | 'warning'
    remaining: string
}

export function UsageMeter({ label, usage, value, color, remaining }: UsageMeterProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card className={mergeClasses(styles.usageCard, (isCompact || isMobile) && styles.usageCardCompact)}>
            <div className={styles.usageHeader}>
                <Caption1 className={(isCompact || isMobile) ? styles.usageCaptionCompact : undefined}>{label}</Caption1>
                <Caption1 className={(isCompact || isMobile) ? styles.usageCaptionCompact : undefined}>{usage}</Caption1>
            </div>
            <Text className={styles.usageValue}>{remaining}</Text>
            <ProgressBar value={value} thickness={(isCompact || isMobile) ? 'medium' : 'large'} color={color} />
            <Caption1 className={(isCompact || isMobile) ? styles.usageCaptionCompact : undefined}>Remaining capacity snapshot</Caption1>
        </Card>
    )
}
