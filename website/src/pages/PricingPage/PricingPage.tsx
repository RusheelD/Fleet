import {
    makeStyles,
    tokens,
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

const useStyles = makeStyles({
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '80px',
        paddingBottom: '48px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        background: `linear-gradient(180deg, ${tokens.colorNeutralBackground1} 0%, ${tokens.colorNeutralBackground3} 100%)`,
    },
    heroSubtitle: {
        maxWidth: '560px',
        marginTop: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground2,
    },
    section: {
        paddingTop: '48px',
        paddingBottom: '80px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
    },
    pricingGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalXL,
        maxWidth: '1100px',
        marginLeft: 'auto',
        marginRight: 'auto',
        alignItems: 'start',
        '@media (max-width: 900px)': {
            gridTemplateColumns: '1fr',
            maxWidth: '420px',
        },
    },
    card: {
        padding: tokens.spacingVerticalL,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    cardPopular: {
        borderColor: tokens.colorBrandStroke1,
        borderWidth: '2px',
        borderStyle: 'solid',
    },
    cardHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    cardHeaderRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    priceRow: {
        display: 'flex',
        alignItems: 'baseline',
        gap: tokens.spacingHorizontalXS,
    },
    priceAmount: {
        fontSize: tokens.fontSizeHero800,
        fontWeight: tokens.fontWeightBold,
        lineHeight: tokens.lineHeightHero800,
    },
    pricePeriod: {
        color: tokens.colorNeutralForeground2,
    },
    featureList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
    },
    featureItem: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalS,
    },
    featureCheck: {
        color: tokens.colorBrandForeground1,
        fontSize: '16px',
        marginTop: '2px',
        flexShrink: 0,
    },

    // FAQ
    sectionAlt: {
        backgroundColor: tokens.colorNeutralBackground2,
    },
    faqHeader: {
        textAlign: 'center',
        marginBottom: '48px',
    },
    faqGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: tokens.spacingHorizontalXL,
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
        gap: tokens.spacingVerticalXS,
    },

    // CTA
    cta: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalL,
        paddingTop: '64px',
        paddingBottom: '64px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
    },
})

const plans: PlanData[] = [
    {
        name: 'Free',
        price: '$0',
        period: 'forever',
        description: 'Perfect for individual developers and small side projects.',
        features: [
            '1 project',
            '50 agent tasks / month',
            'Community support',
            'GitHub integration',
            'Basic work item hierarchy',
        ],
        cta: 'Get started free',
        ctaAppearance: 'outline',
    },
    {
        name: 'Pro',
        popular: true,
        price: '$29',
        period: '/ month',
        description: 'For professional developers and growing teams.',
        features: [
            'Unlimited projects',
            '500 agent tasks / month',
            'Priority support',
            'Advanced agent roles',
            'Custom work item levels',
            'Team collaboration',
            'Execution analytics',
        ],
        cta: 'Start free trial',
        ctaAppearance: 'primary',
    },
    {
        name: 'Enterprise',
        price: 'Custom',
        period: '',
        description: 'For organizations that need scale, security, and control.',
        features: [
            'Unlimited everything',
            'Unlimited agent tasks',
            'Dedicated support',
            'SSO / Entra ID',
            'Custom agent configurations',
            'Audit logging',
            'SLA guarantee',
            'On-premises option',
        ],
        cta: 'Contact sales',
        ctaAppearance: 'outline',
    },
]

const faqs = [
    { q: 'Can I try Fleet before paying?', a: 'Yes! The Free plan is free forever with generous limits. Pro includes a 14-day free trial.' },
    { q: 'What counts as an agent task?', a: 'Each discrete piece of work an agent completes — like implementing a feature, fixing a bug, or creating a PR — counts as one task.' },
    { q: 'Can I cancel anytime?', a: 'Absolutely. No contracts, no cancellation fees. Downgrade to Free whenever you want.' },
    { q: 'Do you support GitHub Enterprise?', a: 'Yes, Enterprise plans support GitHub Enterprise Server and GitHub Enterprise Cloud.' },
]

export function PricingPage() {
    const styles = useStyles()

    return (
        <>
            {/* Hero */}
            <section className={styles.hero}>
                <Title1 as="h1">Simple, transparent pricing</Title1>
                <Body1 className={styles.heroSubtitle} as="p">
                    Start free, scale as you grow. No surprises.
                </Body1>
            </section>

            {/* Pricing Cards */}
            <section className={styles.section}>
                <div className={styles.pricingGrid}>
                    {plans.map((plan) => (
                        <Card key={plan.name} className={`${styles.card} ${plan.popular ? styles.cardPopular : ''}`}>
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
                                {plan.features.map((f) => (
                                    <div key={f} className={styles.featureItem}>
                                        <CheckmarkRegular className={styles.featureCheck} />
                                        <Body2>{f}</Body2>
                                    </div>
                                ))}
                            </div>

                            <Button
                                appearance={plan.ctaAppearance}
                                size="large"
                                as="a"
                                href={plan.name === 'Enterprise' ? '/contact' : `${APP_URL}/login`}
                            >
                                {plan.cta}
                            </Button>
                        </Card>
                    ))}
                </div>
            </section>

            {/* FAQ */}
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

            {/* CTA */}
            <section className={styles.cta}>
                <Title2 as="h2">Start building with AI agents today</Title2>
                <Button
                    appearance="primary"
                    size="large"
                    icon={<ArrowRightRegular />}
                    iconPosition="after"
                    as="a"
                    href={`${APP_URL}/login`}
                >
                    Get started free
                </Button>
            </section>
        </>
    )
}
