import {
    Card,
    makeStyles,
    mergeClasses,
    Title3,
    Divider,
    Spinner,
    Text,
} from '@fluentui/react-components'
import { PageShell } from '../../components/shared'
import { CurrentPlanBanner, UsageMeter, PlanCard } from './'
import { useSubscription } from '../../proxies'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    usageSection: {
        padding: appTokens.space.lg,
        marginBottom: '1.5rem',
        backgroundColor: appTokens.color.surface,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    usageSectionCompact: {
        marginBottom: '1rem',
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
    },
    usageGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
        gap: '1rem',
        marginTop: '1rem',
    },
    usageGridCompact: {
        gridTemplateColumns: '1fr 1fr',
        gap: '0.5rem',
        marginTop: '0.5rem',
    },
    dividerSpacing: {
        marginBottom: '1.5rem',
    },
    dividerSpacingCompact: {
        marginBottom: '1rem',
    },
    planSectionCard: {
        padding: appTokens.space.lg,
        backgroundColor: appTokens.color.surface,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    plansGrid: {
        display: 'grid',
        gridTemplateColumns: `repeat(auto-fill, minmax(${appTokens.width.planCardMin}, 1fr))`,
        gap: appTokens.space.lg,
        marginTop: appTokens.space.lg,
    },
    plansGridCompact: {
        gridTemplateColumns: '1fr',
        gap: '0.5rem',
        marginTop: '0.5rem',
    },
    planSectionCompact: {
        marginBottom: '1rem',
    },
    planSectionSubtextCompact: {
        marginTop: '0.125rem',
    },
})

export function SubscriptionPage() {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isDense = isCompact || isMobile
    const { data: subscription, isLoading } = useSubscription()

    if (isLoading || !subscription) {
        return (
            <PageShell
                title="Subscription"
                subtitle="Manage plan details, track usage, and understand what capacity is available right now."
                maxWidth="large"
            >
                <Spinner label="Loading subscription..." />
            </PageShell>
        )
    }

    return (
        <PageShell
                title="Subscription"
                subtitle="Manage plan details, track usage, and understand what capacity is available right now."
                maxWidth="large"
            >
            <CurrentPlanBanner currentPlan={subscription.currentPlan} />

            <Card className={mergeClasses(styles.usageSection, isDense && styles.usageSectionCompact)}>
                <Title3>This Month&apos;s Usage</Title3>
                <div className={mergeClasses(styles.usageGrid, isDense && styles.usageGridCompact)}>
                    {subscription.usage.map((meter) => (
                        <UsageMeter
                            key={meter.label}
                            label={meter.label}
                            usage={meter.usage}
                            value={meter.value}
                            color={meter.color as 'brand' | 'warning'}
                            remaining={meter.remaining}
                        />
                    ))}
                </div>
            </Card>

            <Divider className={mergeClasses(styles.dividerSpacing, isDense && styles.dividerSpacingCompact)} />

            <Card className={styles.planSectionCard}>
                <div className={isDense ? styles.planSectionCompact : undefined}>
                    <Title3>Available Plans</Title3>
                    <div className={isDense ? styles.planSectionSubtextCompact : undefined} style={!isDense ? { marginTop: '0.25rem' } : undefined}>
                        <Text>
                            Pricing scales based on agents and simultaneous agents purchased. All plans include the same core features.
                        </Text>
                    </div>
                </div>

                <div className={mergeClasses(styles.plansGrid, isDense && styles.plansGridCompact)}>
                    {subscription.plans.map((plan) => (
                        <PlanCard key={plan.name} plan={plan} />
                    ))}
                </div>
            </Card>
        </PageShell>
    )
}
