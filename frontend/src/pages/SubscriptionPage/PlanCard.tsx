import {
    makeStyles,
    mergeClasses,
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
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    planCard: {
        padding: '1.5rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
        position: 'relative',
        overflow: 'visible',
    },
    planCardCompact: {
        paddingTop: '0.75rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
        gap: '0.5rem',
    },
    planCardMobile: {
        paddingTop: '1rem',
        paddingBottom: '1rem',
        paddingLeft: '0.875rem',
        paddingRight: '0.875rem',
    },
    planCardCurrent: {
        borderTopWidth: '2px',
        borderRightWidth: '2px',
        borderBottomWidth: '2px',
        borderLeftWidth: '2px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
    },
    planName: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    planNameCompact: {
        gap: '0.375rem',
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
    priceAmountCompact: {
        fontSize: '20px',
        lineHeight: '24px',
    },
    priceUnit: {
        color: appTokens.color.textTertiary,
    },
    planFeatures: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        flex: 1,
    },
    planFeaturesCompact: {
        gap: '0.25rem',
    },
    featureRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        fontSize: '13px',
    },
    featureRowCompact: {
        gap: '0.375rem',
        fontSize: '12px',
        lineHeight: '16px',
    },
    featureRowMobile: {
        alignItems: 'flex-start',
    },
    featureIcon: {
        color: appTokens.color.success,
        flexShrink: 0,
    },
    currentBadge: {
        position: 'absolute' as const,
        top: '-10px',
        right: '16px',
    },
    currentBadgeMobile: {
        position: 'static',
        alignSelf: 'flex-start',
    },
    fullWidthButton: {
        width: '100%',
    },
    compactDescription: {
        fontSize: '12px',
        lineHeight: '16px',
    },
})

interface PlanCardProps {
    plan: PlanData
}

export function PlanCard({ plan }: PlanCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
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
            className={mergeClasses(
                styles.planCard,
                isCompact && styles.planCardCompact,
                isMobile && !isCompact && styles.planCardMobile,
                plan.isCurrent ? styles.planCardCurrent : undefined,
            )}
        >
            <Toaster toasterId={toasterId} />
            {plan.isCurrent && (
                <Badge
                    appearance="filled"
                    color="brand"
                    className={mergeClasses(styles.currentBadge, isMobile && styles.currentBadgeMobile)}
                >
                    Current
                </Badge>
            )}
            <div className={mergeClasses(styles.planName, isCompact && styles.planNameCompact)}>
                {resolveIcon(plan.icon)}
                <Title3>{plan.name}</Title3>
            </div>
            <div className={styles.planPrice}>
                <Text className={mergeClasses(styles.priceAmount, isCompact && styles.priceAmountCompact)}>{plan.price}</Text>
                <Text className={styles.priceUnit}>{plan.period}</Text>
            </div>
            <Caption1 className={isCompact ? styles.compactDescription : undefined}>{plan.description}</Caption1>
            <Divider />
            <div className={mergeClasses(styles.planFeatures, isCompact && styles.planFeaturesCompact)}>
                {plan.features.map((feature) => (
                    <div
                        key={feature}
                        className={mergeClasses(
                            styles.featureRow,
                            isCompact && styles.featureRowCompact,
                            isMobile && styles.featureRowMobile,
                        )}
                    >
                        <CheckmarkCircleRegular className={styles.featureIcon} />
                        <Text>{feature}</Text>
                    </div>
                ))}
            </div>
            <Button
                appearance={plan.buttonAppearance}
                disabled={plan.isCurrent}
                className={styles.fullWidthButton}
                size={isCompact ? 'small' : 'medium'}
                onClick={handlePlanAction}
            >
                {plan.buttonLabel}
            </Button>
        </Card>
    )
}
