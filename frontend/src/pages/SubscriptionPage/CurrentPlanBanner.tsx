import {
    makeStyles,
    tokens,
    Title3,
    Body1,
    Card,
    Button,
    Badge,
} from '@fluentui/react-components'
import { RocketRegular, ArrowUpRegular } from '@fluentui/react-icons'

const useStyles = makeStyles({
    currentPlan: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        padding: '1.5rem',
        marginBottom: '1.5rem',
        flexWrap: 'wrap',
        gap: '1rem',
    },
    planInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    planBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    planBadgeIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
    },
})

export function CurrentPlanBanner() {
    const styles = useStyles()

    return (
        <Card className={styles.currentPlan}>
            <div className={styles.planInfo}>
                <div className={styles.planBadge}>
                    <RocketRegular className={styles.planBadgeIcon} />
                    <Title3>Free Plan</Title3>
                    <Badge appearance="filled" color="brand">Current</Badge>
                </div>
                <Body1>You&apos;re on the Free plan. Upgrade to unlock more agents and capabilities.</Body1>
            </div>
            <Button appearance="primary" icon={<ArrowUpRegular />}>
                Upgrade Plan
            </Button>
        </Card>
    )
}
