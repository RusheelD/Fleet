import type { ReactNode } from 'react'
import {
    Body1,
    Card,
    Caption1,
    Text,
    makeStyles,
} from '@fluentui/react-components'
import {
    BotRegular,
    BookmarkRegular,
    PlugConnectedRegular,
} from '@fluentui/react-icons'
import { appTokens } from '../../styles/appTokens'
import { FleetRocketLogo } from './FleetRocketLogo'

const useStyles = makeStyles({
    root: {
        minHeight: '100dvh',
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1.1fr) minmax(360px, 460px)',
        alignItems: 'stretch',
        paddingTop: appTokens.space.xxl,
        paddingRight: appTokens.space.xxl,
        paddingBottom: appTokens.space.xxl,
        paddingLeft: appTokens.space.xxl,
        gap: appTokens.space.xl,
        backgroundImage: `radial-gradient(circle at top left, ${appTokens.color.authGlowA}, transparent 32%), radial-gradient(circle at bottom right, ${appTokens.color.authGlowB}, transparent 28%)`,
        backgroundColor: appTokens.color.pageBackground,
        '@media (max-width: 980px)': {
            gridTemplateColumns: '1fr',
            alignItems: 'start',
            paddingTop: appTokens.space.xl,
            paddingRight: appTokens.space.lg,
            paddingBottom: appTokens.space.xl,
            paddingLeft: appTokens.space.lg,
        },
    },
    hero: {
        position: 'relative',
        overflow: 'hidden',
        paddingTop: appTokens.space.xxl,
        paddingRight: appTokens.space.xxl,
        paddingBottom: appTokens.space.xxl,
        paddingLeft: appTokens.space.xxl,
        borderRadius: appTokens.radius.xl,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 58%, ${appTokens.color.surfaceBrand} 160%)`,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        gap: appTokens.space.xl,
        boxShadow: appTokens.shadow.card,
        minHeight: 'min(720px, calc(100dvh - 4rem))',
        '@media (max-width: 980px)': {
            minHeight: 'unset',
            paddingTop: appTokens.space.lg,
            paddingRight: appTokens.space.lg,
            paddingBottom: appTokens.space.lg,
            paddingLeft: appTokens.space.lg,
        },
    },
    heroOrbA: {
        position: 'absolute',
        top: '-4rem',
        right: '-3rem',
        width: '12rem',
        height: '12rem',
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.surfaceBrand,
        filter: 'blur(18px)',
        opacity: 0.55,
        pointerEvents: 'none',
    },
    heroOrbB: {
        position: 'absolute',
        bottom: '-5rem',
        left: '-2rem',
        width: '11rem',
        height: '11rem',
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.infoSurface,
        filter: 'blur(24px)',
        opacity: 0.8,
        pointerEvents: 'none',
    },
    heroTop: {
        position: 'relative',
        zIndex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.lg,
    },
    eyebrow: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        width: 'fit-content',
        paddingTop: appTokens.space.xs,
        paddingRight: appTokens.space.sm,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.surfaceAlt,
        color: appTokens.color.textSecondary,
        boxShadow: appTokens.border.activeInset,
    },
    heroTitle: {
        fontSize: 'clamp(2rem, 4vw, 3.5rem)',
        lineHeight: 1,
        fontWeight: appTokens.fontWeight.bold,
        letterSpacing: '-0.04em',
        maxWidth: '14ch',
    },
    heroSubtitle: {
        maxWidth: '52ch',
        color: appTokens.color.textSecondary,
    },
    featureGrid: {
        position: 'relative',
        zIndex: 1,
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
        gap: appTokens.space.md,
    },
    featureCard: {
        padding: appTokens.space.md,
        borderRadius: appTokens.radius.lg,
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        minWidth: 0,
    },
    featureIcon: {
        color: appTokens.color.brand,
        fontSize: appTokens.fontSize.iconMd,
    },
    heroFooter: {
        position: 'relative',
        zIndex: 1,
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
        gap: appTokens.space.sm,
    },
    signalCard: {
        padding: appTokens.space.md,
        borderRadius: appTokens.radius.lg,
        backgroundColor: appTokens.color.pageBackground,
        border: appTokens.border.subtle,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    signalValue: {
        fontSize: appTokens.fontSize.xl,
        fontWeight: appTokens.fontWeight.bold,
    },
    formColumn: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    formCard: {
        width: '100%',
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.xl,
        borderRadius: appTokens.radius.xl,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        boxShadow: appTokens.shadow.overlay,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.lg,
        '@media (max-width: 980px)': {
            paddingTop: appTokens.space.lg,
            paddingRight: appTokens.space.lg,
            paddingBottom: appTokens.space.lg,
            paddingLeft: appTokens.space.lg,
        },
    },
    formHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        textAlign: 'center',
    },
    formTitle: {
        fontSize: appTokens.fontSize.xl,
        fontWeight: appTokens.fontWeight.bold,
        lineHeight: appTokens.lineHeight.relaxed,
    },
    formSubtitle: {
        color: appTokens.color.textSecondary,
    },
})

interface AuthShellProps {
    eyebrow: string
    title: string
    subtitle: string
    footer?: ReactNode
    children: ReactNode
}

const FEATURE_ROWS = [
    {
        icon: <BookmarkRegular />,
        title: 'Keep durable context close',
        description: 'Memory, playbooks, and execution context stay available instead of disappearing between sessions.',
    },
    {
        icon: <BotRegular />,
        title: 'See agent work clearly',
        description: 'Runs, logs, retries, and sub-flows stay visible while Fleet moves work forward.',
    },
    {
        icon: <PlugConnectedRegular />,
        title: 'Connect real systems',
        description: 'Bring repositories, MCP tools, and automation into one workspace without burying setup.',
    },
]

export function AuthShell({ eyebrow, title, subtitle, footer, children }: AuthShellProps) {
    const styles = useStyles()

    return (
        <div className={styles.root}>
            <div className={styles.hero}>
                <div className={styles.heroOrbA} />
                <div className={styles.heroOrbB} />
                <div className={styles.heroTop}>
                    <div className={styles.eyebrow}>
                        <FleetRocketLogo size={16} title="Fleet" variant="outline" />
                        <Caption1>{eyebrow}</Caption1>
                    </div>
                    <Text className={styles.heroTitle}>{title}</Text>
                    <Body1 className={styles.heroSubtitle}>{subtitle}</Body1>
                </div>

                <div className={styles.featureGrid}>
                    {FEATURE_ROWS.map((feature) => (
                        <Card key={feature.title} className={styles.featureCard}>
                            <span className={styles.featureIcon}>{feature.icon}</span>
                            <Text weight="semibold">{feature.title}</Text>
                            <Caption1>{feature.description}</Caption1>
                        </Card>
                    ))}
                </div>

                <div className={styles.heroFooter}>
                    <div className={styles.signalCard}>
                        <Caption1>Workspace flow</Caption1>
                        <Text className={styles.signalValue}>Portfolio</Text>
                        <Caption1>Projects, notifications, memory, integrations, and settings in one shell.</Caption1>
                    </div>
                    <div className={styles.signalCard}>
                        <Caption1>Execution visibility</Caption1>
                        <Text className={styles.signalValue}>Live</Text>
                        <Caption1>Track work items, agent runs, logs, and docs without jumping between scattered screens.</Caption1>
                    </div>
                </div>
            </div>

            <div className={styles.formColumn}>
                <Card className={styles.formCard}>
                    <div className={styles.formHeader}>
                        <Text className={styles.formTitle}>{title}</Text>
                        <Body1 className={styles.formSubtitle}>{subtitle}</Body1>
                    </div>
                    {children}
                    {footer ? <div>{footer}</div> : null}
                </Card>
            </div>
        </div>
    )
}
