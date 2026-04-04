import {
    makeStyles,
    Title1,
    Title2,
    Title3,
    Body1,
    Body2,
    Button,
    Card,
    Badge,
    Divider,
} from '@fluentui/react-components'
import {
    CheckmarkRegular,
    ArrowRightRegular,
} from '@fluentui/react-icons'
import { APP_URL } from '../../config'
import type { PlanData } from '../../models'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '80px',
        paddingBottom: '48px',
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        background: `linear-gradient(180deg, ${appTokens.color.surface} 0%, ${appTokens.color.pageBackground} 100%)`,
        '@media (max-width: 900px)': {
            paddingTop: '64px',
            paddingBottom: '40px',
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    heroSubtitle: {
        maxWidth: '560px',
        marginTop: appTokens.space.md,
        color: appTokens.color.textSecondary,
    },
    section: {
        paddingTop: '48px',
        paddingBottom: '80px',
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        '@media (max-width: 900px)': {
            paddingTop: '40px',
            paddingBottom: '56px',
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    pricingGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: appTokens.space.xl,
        maxWidth: '1200px',
        marginLeft: 'auto',
        marginRight: 'auto',
        alignItems: 'start',
        '@media (max-width: 1100px)': {
            gridTemplateColumns: 'repeat(2, 1fr)',
            maxWidth: '700px',
        },
        '@media (max-width: 600px)': {
            gridTemplateColumns: '1fr',
            maxWidth: '420px',
        },
    },
    card: {
        padding: appTokens.space.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    cardPopular: {
        border: `2px solid ${appTokens.color.brandStroke}`,
    },
    cardHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
    },
    cardHeaderRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    priceRow: {
        display: 'flex',
        alignItems: 'baseline',
        gap: appTokens.space.xs,
        flexWrap: 'wrap',
    },
    priceAmount: {
        fontSize: appTokens.fontSize.hero,
        fontWeight: appTokens.fontWeight.bold,
        lineHeight: appTokens.lineHeight.hero,
    },
    pricePeriod: {
        color: appTokens.color.textSecondary,
    },
    featureList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
    },
    featureItem: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: appTokens.space.sm,
    },
    featureCheck: {
        color: appTokens.color.brand,
        fontSize: '16px',
        marginTop: '2px',
        flexShrink: 0,
    },

    sectionAlt: {
        backgroundColor: appTokens.color.surfaceAlt,
    },
    faqHeader: {
        textAlign: 'center',
        marginBottom: '48px',
    },
    faqGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.xl,
        maxWidth: '900px',
        marginLeft: 'auto',
        marginRight: 'auto',
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
        },
    },
    faqItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
    },

    cta: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: appTokens.space.lg,
        paddingTop: '64px',
        paddingBottom: '64px',
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        '@media (max-width: 900px)': {
            paddingTop: '56px',
            paddingBottom: '56px',
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    ctaButton: {
        '@media (max-width: 600px)': {
            width: '100%',
        },
    },
})

const plans: PlanData[] = [
    {
        name: 'Free',
        price: '$0',
        period: '/ month',
        description: 'Get started with core Fleet workflows and limited monthly usage.',
        features: [
            'Limited work-item runs / month',
            'Limited coding runs / month',
            'Rate-limited API access',
            '1 concurrent agent per task',
            '1 active execution',
            'GitHub integration',
            'Community support',
        ],
        cta: 'Get started free',
        ctaAppearance: 'outline',
    },
    {
        name: 'Basic',
        price: '$200',
        period: '/ month',
        description: 'Designed for small teams that need higher monthly throughput.',
        features: [
            'Expanded work-item runs / month',
            'Expanded coding runs / month',
            'Higher API rate limits',
            '3 concurrent agents per task',
            '3 active executions',
            'Priority support',
        ],
        cta: 'Contact sales',
        ctaAppearance: 'outline',
    },
    {
        name: 'Pro',
        popular: true,
        price: '$1000',
        period: '/ month',
        description: 'High-throughput plan for organizations running multiple agent pipelines.',
        features: [
            'High work-item runs / month',
            'High coding runs / month',
            'Higher API rate limits',
            '10 concurrent agents per task',
            '10 active executions',
            'Priority support',
        ],
        cta: 'Contact sales',
        ctaAppearance: 'primary',
    },
    {
        name: 'Unlimited',
        price: '$5000',
        period: '/ month',
        description: 'No monthly run caps and no API throttling for large production usage.',
        features: [
            'Unlimited work-item runs',
            'Unlimited coding runs',
            'Unlimited API rate',
            'Unlimited concurrent agents / task',
            'Unlimited active executions',
            'Unlimited monthly credits',
            'All AI models including premium',
            'Dedicated support',
        ],
        cta: 'Contact sales',
        ctaAppearance: 'primary',
    },
]

const faqs = [
    { q: 'Can I try Fleet before paying?', a: 'Yes. The Free tier is available at $0/month and is intended for evaluation and early usage.' },
    { q: 'How does agent-based pricing work?', a: 'Plans scale on monthly run capacity, API rate limits, and concurrency for active agent execution.' },
    { q: 'Can I cancel anytime?', a: 'Absolutely. No contracts, no cancellation fees. Downgrade to Free whenever you want.' },
    { q: 'Do you support GitHub Enterprise?', a: 'Yes. Unlimited and enterprise engagements support both GitHub Enterprise Server and GitHub Enterprise Cloud.' },
]

export function PricingPage() {
    const styles = useStyles()

    return (
        <>
            <section className={styles.hero}>
                <Title1 as="h1">Simple, transparent pricing</Title1>
                <Body1 className={styles.heroSubtitle} as="p">
                    All plans include the same core platform. Scale by increasing monthly capacity and concurrency.
                </Body1>
            </section>

            <section className={styles.section}>
                <div className={styles.pricingGrid}>
                    {plans.map((plan) => (
                        <Card key={plan.name} className={`${styles.card} ${plan.popular ? styles.cardPopular : ''}`} style={{ minHeight: 0 }}>
                            <div className={styles.cardHeader}>
                                <div className={styles.cardHeaderRow}>
                                    <Title3>{plan.name}</Title3>
                                    {plan.popular && <Badge appearance="filled" color="brand">Most popular</Badge>}
                                </div>
                                <Body2>{plan.description}</Body2>
                            </div>

                            <div className={styles.priceRow}>
                                <span className={styles.priceAmount}>{plan.price}</span>
                                {plan.period && <Body1 className={styles.pricePeriod}>{plan.period}</Body1>}
                            </div>

                            <Divider />

                            <div className={styles.featureList}>
                                {plan.features.map((feature) => (
                                    <div key={feature} className={styles.featureItem}>
                                        <CheckmarkRegular className={styles.featureCheck} />
                                        <Body2>{feature}</Body2>
                                    </div>
                                ))}
                            </div>

                            <Button
                                appearance={plan.ctaAppearance}
                                size="large"
                                as="a"
                                href={plan.name === 'Unlimited' ? '/contact' : `${APP_URL}/login`}
                                className={styles.ctaButton}
                            >
                                {plan.cta}
                            </Button>
                        </Card>
                    ))}
                </div>
            </section>

            <section className={`${styles.section} ${styles.sectionAlt}`}>
                <div className={styles.faqHeader}>
                    <Title2 as="h2">Frequently asked questions</Title2>
                </div>
                <div className={styles.faqGrid}>
                    {faqs.map((faq) => (
                        <div key={faq.q} className={styles.faqItem}>
                            <Title3>{faq.q}</Title3>
                            <Body1>{faq.a}</Body1>
                        </div>
                    ))}
                </div>
            </section>

            <section className={styles.cta}>
                <Title2 as="h2">Start building with AI agents today</Title2>
                <Button
                    appearance="primary"
                    size="large"
                    icon={<ArrowRightRegular />}
                    iconPosition="after"
                    as="a"
                    href={`${APP_URL}/login`}
                    className={styles.ctaButton}
                >
                    Get started free
                </Button>
            </section>
        </>
    )
}
