import {
    makeStyles,
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
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    hero: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        paddingTop: '80px',
        paddingBottom: '64px',
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        background: `linear-gradient(180deg, ${appTokens.color.surface} 0%, ${appTokens.color.pageBackground} 100%)`,
        '@media (max-width: 900px)': {
            paddingTop: '64px',
            paddingBottom: '48px',
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    heroSubtitle: {
        maxWidth: '600px',
        marginTop: appTokens.space.md,
        color: appTokens.color.textSecondary,
    },
    section: {
        position: 'relative' as const,
        paddingTop: '64px',
        paddingBottom: '64px',
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        maxWidth: '1000px',
        marginLeft: 'auto',
        marginRight: 'auto',
        overflow: 'hidden',
        '@media (max-width: 900px)': {
            paddingTop: '56px',
            paddingBottom: '56px',
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    sectionAlt: {
        backgroundColor: appTokens.color.surfaceAlt,
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
        gap: appTokens.space.sm,
    },
    sectionSubtitle: {
        color: appTokens.color.textSecondary,
    },

    // Mission / Vision
    missionGrid: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.xxl,
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
        },
    },
    missionCard: {
        padding: appTokens.space.lg,
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    missionIcon: {
        fontSize: '28px',
        color: appTokens.color.brand,
    },

    // Values
    valuesGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: appTokens.space.lg,
        '@media (max-width: 900px)': {
            gridTemplateColumns: 'repeat(2, 1fr)',
        },
        '@media (max-width: 500px)': {
            gridTemplateColumns: '1fr',
        },
    },
    valueCard: {
        padding: appTokens.space.lg,
        textAlign: 'center',
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
        boxShadow: appTokens.shadow.card,
    },
    valueIcon: {
        fontSize: '32px',
        color: appTokens.color.brand,
        marginBottom: appTokens.space.sm,
    },

    // Story
    storyContent: {
        maxWidth: '720px',
        marginLeft: 'auto',
        marginRight: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
    },

    // CTA
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
                    className={styles.ctaButton}
                >
                    Get started free
                </Button>
            </section>
        </>
    )
}
