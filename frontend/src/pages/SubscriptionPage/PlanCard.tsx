import {
    makeStyles,
    mergeClasses,
    tokens,
    Title3,
    Caption1,
    Text,
    Card,
    Button,
    Badge,
    Divider,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
} from '@fluentui/react-components'
import { CheckmarkCircleRegular } from '@fluentui/react-icons'
import type { PlanData } from '../../models'
import { resolveIcon } from '../../proxies'

const useStyles = makeStyles({
    planCard: {
        padding: '1.5rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
        position: 'relative',
        overflow: 'visible',
    },
    planCardCurrent: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
    },
    planName: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    planPrice: {
        display: 'flex',
        alignItems: 'baseline',
        gap: '0.25rem',
    },
    priceAmount: {
        fontSize: '32px',
        fontWeight: 700,
    },
    priceUnit: {
        color: tokens.colorNeutralForeground3,
    },
    planFeatures: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        flex: 1,
    },
    featureRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        fontSize: '13px',
    },
    featureIcon: {
        color: tokens.colorPaletteGreenForeground1,
        flexShrink: 0,
    },
    currentBadge: {
        position: 'absolute' as const,
        top: '-10px',
        right: '16px',
    },
    fullWidthButton: {
        width: '100%',
    },
})

interface PlanCardProps {
    plan: PlanData
}

export function PlanCard({ plan }: PlanCardProps) {
    const styles = useStyles()
    const toasterId = useId('plan-toaster')
    const { dispatchToast } = useToastController(toasterId)

    const handlePlanAction = () => {
        dispatchToast(
            <Toast><ToastTitle>Plan changes are not available in this version</ToastTitle></Toast>,
            { intent: 'info' },
        )
    }

    return (
        <Card
            className={mergeClasses(styles.planCard, plan.isCurrent ? styles.planCardCurrent : undefined)}
        >
            <Toaster toasterId={toasterId} />
            {plan.isCurrent && (
                <Badge
                    appearance="filled"
                    color="brand"
                    className={styles.currentBadge}
                >
                    Current
                </Badge>
            )}
            <div className={styles.planName}>
                {resolveIcon(plan.icon)}
                <Title3>{plan.name}</Title3>
            </div>
            <div className={styles.planPrice}>
                <Text className={styles.priceAmount}>{plan.price}</Text>
                <Text className={styles.priceUnit}>{plan.period}</Text>
            </div>
            <Caption1>{plan.description}</Caption1>
            <Divider />
            <div className={styles.planFeatures}>
                {plan.features.map((feature) => (
                    <div key={feature} className={styles.featureRow}>
                        <CheckmarkCircleRegular className={styles.featureIcon} />
                        <Text>{feature}</Text>
                    </div>
                ))}
            </div>
            <Button
                appearance={plan.buttonAppearance}
                disabled={plan.isCurrent}
                className={styles.fullWidthButton}
                onClick={handlePlanAction}
            >
                {plan.buttonLabel}
            </Button>
        </Card>
    )
}
