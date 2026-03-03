import {
    makeStyles,
    tokens,
    Title1,
    Title2,
    Title3,
    Body1,
    Body2,
    Card,
    CardHeader,
    Button,
} from '@fluentui/react-components'
import {
    TargetArrowRegular,
    LightbulbRegular,
    HeartRegular,
    StarRegular,
    ArrowRightRegular,
} from '@fluentui/react-icons'
import { APP_URL } from '../../config'

const useStyles = makeStyles({
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '80px',
        paddingBottom: '64px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        background: `linear-gradient(180deg, ${tokens.colorNeutralBackground1} 0%, ${tokens.colorNeutralBackground3} 100%)`,
    },
    heroSubtitle: {
        maxWidth: '600px',
        marginTop: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground2,
    },
    section: {
        position: 'relative' as const,
        paddingTop: '64px',
        paddingBottom: '64px',
        paddingLeft: tokens.spacingHorizontalXXL,
        paddingRight: tokens.spacingHorizontalXXL,
        maxWidth: '1000px',
        marginLeft: 'auto',
        marginRight: 'auto',
        overflow: 'hidden',
    },
    sectionAlt: {
        backgroundColor: tokens.colorNeutralBackground2,
        maxWidth: 'unset',
    },
    sectionInner: {
        maxWidth: '1000px',
        marginLeft: 'auto',
        marginRight: 'auto',
    },
    sectionHeader: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        marginBottom: '48px',
        gap: tokens.spacingVerticalS,
    },
    sectionSubtitle: {
        color: tokens.colorNeutralForeground2,
    },

    // Mission / Vision
    missionGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: tokens.spacingHorizontalXXL,
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
        },
    },
    missionCard: {
        padding: tokens.spacingVerticalL,
    },
    missionIcon: {
        fontSize: '28px',
        color: tokens.colorBrandForeground1,
    },

    // Values
    valuesGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: tokens.spacingHorizontalL,
        '@media (max-width: 900px)': {
            gridTemplateColumns: 'repeat(2, 1fr)',
        },
        '@media (max-width: 500px)': {
            gridTemplateColumns: '1fr',
        },
    },
    valueCard: {
        padding: tokens.spacingVerticalL,
        textAlign: 'center',
    },
    valueIcon: {
        fontSize: '32px',
        color: tokens.colorBrandForeground1,
        marginBottom: tokens.spacingVerticalS,
    },

    // Story
    storyContent: {
        maxWidth: '720px',
        marginLeft: 'auto',
        marginRight: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
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

const values = [
    { icon: <TargetArrowRegular />, title: 'Focus', description: 'We build tools that eliminate busywork so teams can focus on what matters.' },
    { icon: <LightbulbRegular />, title: 'Innovation', description: 'We push the boundaries of what AI agents can do for software teams.' },
    { icon: <HeartRegular />, title: 'Craft', description: 'We care deeply about developer experience and product quality.' },
    { icon: <StarRegular />, title: 'Transparency', description: 'We believe AI should be observable, auditable, and under your control.' },
]

export function AboutPage() {
    const styles = useStyles()

    return (
        <>
            {/* Hero */}
            <section className={styles.hero}>
                <Title1 as="h1">About Fleet</Title1>
                <Body1 className={styles.heroSubtitle} as="p">
                    We&apos;re building the future of software project management — where AI agents
                    are first-class team members that plan, code, and ship alongside humans.
                </Body1>
            </section>

            {/* Mission & Vision */}
            <section className={styles.section}>
                <div className={styles.missionGrid}>
                    <Card className={styles.missionCard}>
                        <CardHeader
                            image={<TargetArrowRegular className={styles.missionIcon} />}
                            header={<Title3>Our Mission</Title3>}
                            description={
                                <Body1>
                                    To empower every software team with AI agents that handle the heavy lifting —
                                    from breaking down requirements to shipping production-ready code.
                                </Body1>
                            }
                        />
                    </Card>
                    <Card className={styles.missionCard}>
                        <CardHeader
                            image={<LightbulbRegular className={styles.missionIcon} />}
                            header={<Title3>Our Vision</Title3>}
                            description={
                                <Body1>
                                    A world where development teams of any size can ship software at the pace of their
                                    ideas, with AI agents as trusted collaborators.
                                </Body1>
                            }
                        />
                    </Card>
                </div>
            </section>

            {/* Our Story */}
            <section className={`${styles.section} ${styles.sectionAlt}`}>
                <div className={styles.sectionInner}>
                    <div className={styles.sectionHeader}>
                        <Title2 as="h2">Our Story</Title2>
                    </div>
                    <div className={styles.storyContent}>
                        <Body1>
                            Fleet started from a simple observation: software teams spend more time managing work
                            than doing work. Issue trackers, sprint planning, code reviews, deployments — the overhead
                            is real and it slows everyone down.
                        </Body1>
                        <Body1>
                            We asked: what if AI agents could handle the repetitive parts? Not just suggest code snippets,
                            but actually plan tasks, write implementations, create pull requests, and respond to review feedback.
                        </Body1>
                        <Body1>
                            That&apos;s Fleet. It&apos;s a project management platform where AI agents are first-class team members.
                            They understand your codebase, follow your conventions, and ship code that meets your standards.
                        </Body1>
                    </div>
                </div>
            </section>

            {/* Values */}
            <section className={styles.section}>
                <div className={styles.sectionHeader}>
                    <Title2 as="h2">Our Values</Title2>
                    <Body1 className={styles.sectionSubtitle} as="p">
                        The principles that guide everything we build.
                    </Body1>
                </div>
                <div className={styles.valuesGrid}>
                    {values.map((v) => (
                        <Card key={v.title} className={styles.valueCard}>
                            <div className={styles.valueIcon}>{v.icon}</div>
                            <Title3>{v.title}</Title3>
                            <Body2>{v.description}</Body2>
                        </Card>
                    ))}
                </div>
            </section>

            {/* CTA */}
            <section className={styles.cta}>
                <Title2 as="h2">Join the future of software development</Title2>
                <Body1>Start using Fleet for free today.</Body1>
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
