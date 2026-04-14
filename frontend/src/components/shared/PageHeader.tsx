import { Body1, Caption1, Text, makeStyles, mergeClasses } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { useIsMobile } from '../../hooks/useIsMobile'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        position: 'relative',
        overflow: 'hidden',
        marginBottom: appTokens.space.xl,
        flexWrap: 'wrap',
        gap: appTokens.space.lg,
        paddingTop: appTokens.space.xl,
        paddingRight: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.xl,
        borderRadius: appTokens.radius.xl,
        border: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
        backgroundImage: `linear-gradient(140deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 52%, ${appTokens.color.surfaceBrand} 180%)`,
        boxShadow: appTokens.shadow.card,
    },
    headerLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        minWidth: 0,
        position: 'relative',
        zIndex: 1,
        maxWidth: '56rem',
    },
    eyebrow: {
        display: 'inline-flex',
        width: 'fit-content',
        paddingTop: appTokens.space.xxxs,
        paddingRight: appTokens.space.sm,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.sm,
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.surfaceAlt,
        color: appTokens.color.textSecondary,
        boxShadow: appTokens.border.activeInset,
        textTransform: 'uppercase',
        letterSpacing: '0.08em',
    },
    title: {
        fontSize: 'clamp(1.75rem, 2vw + 1rem, 2.45rem)',
        lineHeight: 1,
        fontWeight: appTokens.fontWeight.bold,
        letterSpacing: '-0.03em',
        color: appTokens.color.textPrimary,
    },
    subtitle: {
        color: appTokens.color.textSecondary,
        maxWidth: '46rem',
    },
    headerMobile: {
        marginBottom: appTokens.space.lg,
        gap: appTokens.space.md,
        paddingTop: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        paddingBottom: appTokens.space.lg,
        paddingLeft: appTokens.space.lg,
    },
    actionsMobile: {
        width: '100%',
    },
    actionsDesktop: {
        position: 'relative',
        zIndex: 1,
        marginLeft: 'auto',
        paddingTop: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.sm,
        borderRadius: appTokens.radius.lg,
        backgroundColor: appTokens.color.surfaceRaised,
        border: appTokens.border.subtle,
    },
    accentA: {
        position: 'absolute',
        top: '-3.5rem',
        right: '-2.5rem',
        width: '10rem',
        height: '10rem',
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.surfaceBrand,
        filter: 'blur(14px)',
        opacity: 0.6,
        pointerEvents: 'none',
    },
    accentB: {
        position: 'absolute',
        bottom: '-4.5rem',
        left: '-2rem',
        width: '9rem',
        height: '9rem',
        borderRadius: appTokens.radius.full,
        backgroundColor: appTokens.color.infoSurface,
        filter: 'blur(18px)',
        opacity: 0.75,
        pointerEvents: 'none',
    },
})

interface PageHeaderProps {
    title: string
    subtitle?: string
    actions?: ReactNode
    eyebrow?: string
}

export function PageHeader({ title, subtitle, actions, eyebrow = 'Workspace' }: PageHeaderProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()

    return (
        <div className={mergeClasses(styles.header, isMobile && styles.headerMobile)}>
            <div className={styles.accentA} />
            <div className={styles.accentB} />
            <div className={styles.headerLeft}>
                <Caption1 className={styles.eyebrow}>{eyebrow}</Caption1>
                <Text className={styles.title}>{title}</Text>
                {subtitle && <Body1 className={styles.subtitle}>{subtitle}</Body1>}
            </div>
            {actions && (
                <div className={mergeClasses(isMobile ? styles.actionsMobile : styles.actionsDesktop)}>
                    {actions}
                </div>
            )}
        </div>
    )
}
