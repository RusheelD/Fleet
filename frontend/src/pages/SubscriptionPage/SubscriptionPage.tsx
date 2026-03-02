import {
    makeStyles,
    Title3,
    Divider,
} from '@fluentui/react-components'
import {
    RocketRegular,
    DiamondRegular,
    SparkleRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { CurrentPlanBanner } from './CurrentPlanBanner'
import { UsageMeter } from './UsageMeter'
import { PlanCard } from './PlanCard'
import type { PlanData } from '../../models'

const PLANS: PlanData[] = [
    {
        name: 'Free',
        icon: <RocketRegular />,
        price: '$0',
        period: '/month',
        description: 'Get started with AI-assisted development',
        features: [
            '1 concurrent agent per task',
            '1 total agent',
            'Limited monthly credits',
            'Base AI model only',
            '3 projects',
        ],
        buttonLabel: 'Current Plan',
        isCurrent: true,
        buttonAppearance: 'outline',
    },
    {
        name: 'Pro',
        icon: <DiamondRegular />,
        price: '$29',
        period: '/month',
        description: 'For serious builders shipping fast',
        features: [
            '5 concurrent agents per task',
            '10 total agents',
            'Higher monthly credits',
            'Base + Mid-tier AI models',
            'Unlimited projects',
            'Priority support',
        ],
        buttonLabel: 'Upgrade to Pro',
        isCurrent: false,
        buttonAppearance: 'primary',
    },
    {
        name: 'Team',
        icon: <SparkleRegular />,
        price: '$99',
        period: '/month',
        description: 'Maximum power for teams and enterprises',
        features: [
            '10 concurrent agents per task',
            '25 total agents',
            'Highest monthly credits',
            'All AI models including premium',
            'Unlimited projects',
            'Priority support',
            'Team collaboration (coming soon)',
        ],
        buttonLabel: 'Upgrade to Team',
        isCurrent: false,
        buttonAppearance: 'primary',
    },
]

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

    return (
        <div className={styles.page}>
            <PageHeader
                title="Subscription"
                subtitle="Manage your plan, usage, and billing"
            />

            <CurrentPlanBanner />

            <div className={styles.usageSection}>
                <Title3>This Month&apos;s Usage</Title3>
                <div className={styles.usageGrid}>
                    <UsageMeter label="Agent Credits" usage="45 / 100" value={0.45} color="brand" remaining="55 credits remaining" />
                    <UsageMeter label="Agent Hours" usage="3.2 / 10 hrs" value={0.32} color="brand" remaining="6.8 hours remaining" />
                    <UsageMeter label="Active Agents" usage="1 / 1" value={1.0} color="warning" remaining="At limit — upgrade for more" />
                    <UsageMeter label="Projects" usage="2 / 3" value={0.67} color="brand" remaining="1 project slot remaining" />
                </div>
            </div>

            <Divider className={styles.dividerSpacing} />

            <Title3>Available Plans</Title3>
            <div className={styles.plansGrid}>
                {PLANS.map((plan) => (
                    <PlanCard key={plan.name} plan={plan} />
                ))}
            </div>
        </div>
    )
}
