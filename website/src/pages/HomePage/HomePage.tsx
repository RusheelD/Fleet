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
    CardHeader,
} from '@fluentui/react-components'
import {
    RocketRegular,
    BotRegular,
    BoardRegular,
    PeopleTeamRegular,
    ShieldCheckmarkRegular,
    ArrowRightRegular,
    FlashRegular,
} from '@fluentui/react-icons'
import { APP_URL } from '../../config'

const useStyles = makeStyles({
    // Hero
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '96px',
        paddingBottom: '96px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        background: `linear-gradient(180deg, ${tokens.colorNeutralBackground1} 0%, ${tokens.colorNeutralBackground3} 100%)`,
    },
    heroTitle: {
        maxWidth: '700px',
        marginBottom: tokens.spacingVerticalL,
    },
    heroSubtitle: {
        maxWidth: '560px',
        marginBottom: tokens.spacingVerticalXL,
        color: tokens.colorNeutralForeground2,
    },
    heroCta: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        flexWrap: 'wrap',
        justifyContent: 'center',
    },

    // Features
    section: {
        position: 'relative' as const,
        paddingTop: '80px',
        paddingBottom: '80px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        overflow: 'hidden',
    },
    sectionAlt: {
        backgroundColor: tokens.colorNeutralBackground2,
    },
    sectionHeader: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        maxWidth: '600px',
        marginLeft: 'auto',
        marginRight: 'auto',
        marginBottom: '48px',
        gap: tokens.spacingVerticalS,
    },
    sectionSubtitle: {
        color: tokens.colorNeutralForeground2,
    },
    featureGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalXL,
        maxWidth: '1100px',
        marginLeft: 'auto',
        marginRight: 'auto',
        '@media (max-width: 900px)': {
            gridTemplateColumns: '1fr',
        },
    },
    featureCard: {
        padding: tokens.spacingVerticalL,
    },
    featureIcon: {
        fontSize: '32px',
        color: tokens.colorBrandForeground1,
        marginBottom: tokens.spacingVerticalS,
    },
    featureTitle: {
        marginBottom: tokens.spacingVerticalXS,
    },

    // How it works
    steps: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: tokens.spacingHorizontalXXL,
        maxWidth: '1000px',
        marginLeft: 'auto',
        marginRight: 'auto',
        '@media (max-width: 900px)': {
            gridTemplateColumns: '1fr',
        },
    },
    step: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
    },
    stepNumber: {
        width: '48px',
        height: '48px',
        borderRadius: '50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundOnBrand,
        fontWeight: tokens.fontWeightBold,
        fontSize: tokens.fontSizeBase500,
    },

    // CTA banner
    cta: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalL,
        paddingTop: '80px',
        paddingBottom: '80px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
    },
})

const features = [
    {
        icon: <BotRegular />,
        title: 'AI Agents',
        description: 'Autonomous agents plan, code, and complete tasks in your GitHub repos — from issue to pull request.',
    },
    {
        icon: <BoardRegular />,
        title: 'Smart Work Items',
        description: 'Hierarchical work items with customizable levels. Agents break down epics into actionable tasks.',
    },
    {
        icon: <FlashRegular />,
        title: 'Real-Time Execution',
        description: 'Watch agents work in real time. Monitor logs, review changes, and approve pull requests.',
    },
    {
        icon: <PeopleTeamRegular />,
        title: 'Team Collaboration',
        description: 'Chat with AI about your project. Copilot understands your codebase and helps you plan.',
    },
    {
        icon: <RocketRegular />,
        title: 'Ship Faster',
        description: 'Reduce cycle time by letting agents handle implementation while your team focuses on architecture.',
    },
    {
        icon: <ShieldCheckmarkRegular />,
        title: 'Enterprise Ready',
        description: 'Microsoft Entra ID authentication, role-based access, and audit trails built in.',
    },
]

export function HomePage() {
    const styles = useStyles()

    return (
        <>
            {/* Hero */}
            <section className={styles.hero}>
                <div className={styles.heroTitle}>
                    <Title1 as="h1">Ship software faster with AI agents</Title1>
                </div>
                <Body1 className={styles.heroSubtitle} as="p">
                    Fleet orchestrates AI agents to plan, build, and complete software tasks in your GitHub repositories.
                    From idea to pull request — automatically.
                </Body1>
                <div className={styles.heroCta}>
                    <Button
                        appearance="primary"
                        size="large"
                        icon={<ArrowRightRegular />}
                        iconPosition="after"
                        as="a"
                        href={`${APP_URL}/signup`}
                    >
                        Get started free
                    </Button>
                    <Button
                        appearance="outline"
                        size="large"
                        as="a"
                        href="/about"
                    >
                        Learn more
                    </Button>
                </div>
            </section>

            {/* Features */}
            <section className={`${styles.section} ${styles.sectionAlt}`}>
                <div className={styles.sectionHeader}>
                    <Title2 as="h2">Everything you need to automate development</Title2>
                    <Body1 className={styles.sectionSubtitle} as="p">
                        Fleet combines intelligent project management with autonomous AI agents that actually write code.
                    </Body1>
                </div>
                <div className={styles.featureGrid}>
                    {features.map((f) => (
                        <Card key={f.title} className={styles.featureCard}>
                            <CardHeader
                                image={<span className={styles.featureIcon}>{f.icon}</span>}
                                header={<Title3 className={styles.featureTitle}>{f.title}</Title3>}
                                description={<Body2>{f.description}</Body2>}
                            />
                        </Card>
                    ))}
                </div>
            </section>

            {/* How it works */}
            <section className={styles.section}>
                <div className={styles.sectionHeader}>
                    <Title2 as="h2">How it works</Title2>
                    <Body1 className={styles.sectionSubtitle} as="p">
                        Three simple steps to go from idea to shipped code.
                    </Body1>
                </div>
                <div className={styles.steps}>
                    <div className={styles.step}>
                        <div className={styles.stepNumber}>1</div>
                        <Title3>Create a project</Title3>
                        <Body1>Connect your GitHub repository and define your work items with customizable hierarchy levels.</Body1>
                    </div>
                    <div className={styles.step}>
                        <div className={styles.stepNumber}>2</div>
                        <Title3>Assign agents</Title3>
                        <Body1>AI agents pick up tasks, plan their approach, and start writing code in isolated branches.</Body1>
                    </div>
                    <div className={styles.step}>
                        <div className={styles.stepNumber}>3</div>
                        <Title3>Review &amp; ship</Title3>
                        <Body1>Review agent-created pull requests, provide feedback, and merge when ready.</Body1>
                    </div>
                </div>
            </section>

            {/* CTA Banner */}
            <section className={`${styles.cta} ${styles.sectionAlt}`}>
                <Title2 as="h2">Ready to supercharge your team?</Title2>
                <Body1>Start for free — no credit card required.</Body1>
                <Button
                    appearance="primary"
                    size="large"
                    icon={<ArrowRightRegular />}
                    iconPosition="after"
                    as="a"
                    href={`${APP_URL}/signup`}
                >
                    Get started free
                </Button>
            </section>
        </>
    )
}
