import {
    makeStyles,
    Title3,
    Divider,
    Spinner,
    Text,
} from '@fluentui/react-components'
import { PageHeader } from '../../components/shared'
import { CurrentPlanBanner, UsageMeter, PlanCard } from './'
import { useSubscription } from '../../proxies'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '1100px',
        margin: '0 auto',
        width: '100%',
    },
    usageSection: {
        marginBottom: '1.5rem',
    },
    usageGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
        gap: '1rem',
        marginTop: '1rem',
    },
    dividerSpacing: {
        marginBottom: '1.5rem',
    },
    plansGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
        gap: '1rem',
        marginTop: '1rem',
    },
})

export function SubscriptionPage() {
    const styles = useStyles()
    const { data: subscription, isLoading } = useSubscription()

    if (isLoading || !subscription) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading subscription..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
            <PageHeader
                title="Subscription"
                subtitle="Manage your plan, usage, and billing"
            />

            <CurrentPlanBanner currentPlan={subscription.currentPlan} />

            <div className={styles.usageSection}>
                <Title3>This Month&apos;s Usage</Title3>
                <div className={styles.usageGrid}>
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

            <Divider className={styles.dividerSpacing} />

            <div style={{ marginBottom: '1.5rem' }}>
                <Title3>Available Plans</Title3>
                <div style={{ marginTop: '0.25rem' }}>
                    <Text>
                        Pricing scales based on agents and simultaneous agents purchased. All plans include the same core features.
                    </Text>
                </div>
            </div>

            <div className={styles.plansGrid}>
                {subscription.plans.map((plan) => (
                    <PlanCard key={plan.name} plan={plan} />
                ))}
            </div>
        </div>
    )
}
