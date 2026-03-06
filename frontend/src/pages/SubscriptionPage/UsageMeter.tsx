import {
    makeStyles,
    mergeClasses,
    Caption1,
    Card,
    ProgressBar,
} from '@fluentui/react-components'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    usageCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
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
    },
    usageCaptionCompact: {
        fontSize: '11px',
        lineHeight: '14px',
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
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card className={mergeClasses(styles.usageCard, isCompact && styles.usageCardCompact)}>
            <div className={styles.usageHeader}>
                <Caption1 className={isCompact ? styles.usageCaptionCompact : undefined}>{label}</Caption1>
                <Caption1 className={isCompact ? styles.usageCaptionCompact : undefined}>{usage}</Caption1>
            </div>
            <ProgressBar value={value} thickness={isCompact ? 'medium' : 'large'} color={color} />
            <Caption1 className={isCompact ? styles.usageCaptionCompact : undefined}>{remaining}</Caption1>
        </Card>
    )
}
