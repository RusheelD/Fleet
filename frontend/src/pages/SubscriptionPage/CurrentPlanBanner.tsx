import {
    makeStyles,
    mergeClasses,
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
import { usePreferences } from '../../hooks'

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
    currentPlanCompact: {
        paddingTop: '0.625rem',
        paddingBottom: '0.625rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
        marginBottom: '1rem',
        gap: '0.5rem',
    },
    planInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
    },
    planInfoCompact: {
        gap: '0.25rem',
    },
    planBadge: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    planBadgeCompact: {
        gap: '0.375rem',
    },
    planBadgeIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
    },
    planBadgeIconCompact: {
        fontSize: '16px',
    },
    compactBody: {
        fontSize: '12px',
        lineHeight: '16px',
    },
})

interface CurrentPlanBannerProps {
    currentPlan: CurrentPlan
}

export function CurrentPlanBanner({ currentPlan }: CurrentPlanBannerProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const toasterId = useId('banner-toaster')
    const { dispatchToast } = useToastController(toasterId)

    return (
        <Card className={mergeClasses(styles.currentPlan, isCompact && styles.currentPlanCompact)}>
            <Toaster toasterId={toasterId} />
            <div className={mergeClasses(styles.planInfo, isCompact && styles.planInfoCompact)}>
                <div className={mergeClasses(styles.planBadge, isCompact && styles.planBadgeCompact)}>
                    <RocketRegular className={mergeClasses(styles.planBadgeIcon, isCompact && styles.planBadgeIconCompact)} />
                    <Title3>{currentPlan.name}</Title3>
                    <Badge appearance="filled" color="brand">Current</Badge>
                </div>
                <Body1 className={isCompact ? styles.compactBody : undefined}>{currentPlan.description}</Body1>
            </div>
            <Button
                appearance="primary"
                icon={<ArrowUpRegular />}
                size={isCompact ? 'small' : 'medium'}
                onClick={() => dispatchToast(<Toast><ToastTitle>Plan upgrades are not available in this version</ToastTitle></Toast>, { intent: 'info' })}
            >
                Upgrade Plan
            </Button>
        </Card>
    )
}
