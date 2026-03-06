import {
    makeStyles,
    mergeClasses,
    Title3,
    Divider,
    Spinner,
    Text,
} from '@fluentui/react-components'
import { PageHeader } from '../../components/shared'
import { CurrentPlanBanner, UsageMeter, PlanCard } from './'
import { useSubscription } from '../../proxies'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '1100px',
        margin: '0 auto',
        width: '100%',
    },
    pageCompact: {
        paddingTop: '1rem',
        paddingBottom: '1rem',
        paddingLeft: '1rem',
        paddingRight: '1rem',
        maxWidth: '980px',
    },
    usageSection: {
        marginBottom: '1.5rem',
    },
    usageSectionCompact: {
        marginBottom: '1rem',
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
    plansGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
        gap: '1rem',
        marginTop: '1rem',
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
    const isCompact = preferences?.compactMode ?? false
    const { data: subscription, isLoading } = useSubscription()

    if (isLoading || !subscription) {
        return (
            <div className={mergeClasses(styles.page, isCompact && styles.pageCompact)}>
                <Spinner label="Loading subscription..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isCompact && styles.pageCompact)}>
            <PageHeader
                title="Subscription"
                subtitle="Manage your plan, usage, and billing"
            />

            <CurrentPlanBanner currentPlan={subscription.currentPlan} />

            <div className={mergeClasses(styles.usageSection, isCompact && styles.usageSectionCompact)}>
                <Title3>This Month&apos;s Usage</Title3>
                <div className={mergeClasses(styles.usageGrid, isCompact && styles.usageGridCompact)}>
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
            </div>

            <Divider className={mergeClasses(styles.dividerSpacing, isCompact && styles.dividerSpacingCompact)} />

            <div className={isCompact ? styles.planSectionCompact : undefined}>
                <Title3>Available Plans</Title3>
                <div className={isCompact ? styles.planSectionSubtextCompact : undefined} style={!isCompact ? { marginTop: '0.25rem' } : undefined}>
                    <Text>
                        Pricing scales based on agents and simultaneous agents purchased. All plans include the same core features.
                    </Text>
                </div>
            </div>

            <div className={mergeClasses(styles.plansGrid, isCompact && styles.plansGridCompact)}>
                {subscription.plans.map((plan) => (
                    <PlanCard key={plan.name} plan={plan} />
                ))}
            </div>
        </div>
    )
}
