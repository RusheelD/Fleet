import {
    makeStyles,
    Caption1,
    Card,
    ProgressBar,
} from '@fluentui/react-components'

const useStyles = makeStyles({
    usageCard: {
        padding: '1rem 1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    usageHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
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

    return (
        <Card className={styles.usageCard}>
            <div className={styles.usageHeader}>
                <Caption1>{label}</Caption1>
                <Caption1>{usage}</Caption1>
            </div>
            <ProgressBar value={value} thickness="large" color={color} />
            <Caption1>{remaining}</Caption1>
        </Card>
    )
}
