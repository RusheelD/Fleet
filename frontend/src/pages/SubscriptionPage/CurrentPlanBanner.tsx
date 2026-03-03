import {
    makeStyles,
    tokens,
    Title3,
    Body1,
    Card,
    Button,
    Badge,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
} from '@fluentui/react-components'
import { RocketRegular, ArrowUpRegular } from '@fluentui/react-icons'
import type { CurrentPlan } from '../../models'

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

interface CurrentPlanBannerProps {
    currentPlan: CurrentPlan
}

export function CurrentPlanBanner({ currentPlan }: CurrentPlanBannerProps) {
    const styles = useStyles()
    const toasterId = useId('banner-toaster')
    const { dispatchToast } = useToastController(toasterId)

    return (
        <Card className={styles.currentPlan}>
            <Toaster toasterId={toasterId} />
            <div className={styles.planInfo}>
                <div className={styles.planBadge}>
                    <RocketRegular className={styles.planBadgeIcon} />
                    <Title3>{currentPlan.name}</Title3>
                    <Badge appearance="filled" color="brand">Current</Badge>
                </div>
                <Body1>{currentPlan.description}</Body1>
            </div>
            <Button appearance="primary" icon={<ArrowUpRegular />} onClick={() => dispatchToast(<Toast><ToastTitle>Plan upgrades are not available in this version</ToastTitle></Toast>, { intent: 'info' })}>
                Upgrade Plan
            </Button>
        </Card>
    )
}
