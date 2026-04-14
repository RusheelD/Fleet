import { Body1, Caption1, Text, makeStyles, mergeClasses } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { useIsMobile } from '../../hooks/useIsMobile'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: appTokens.space.xl,
        flexWrap: 'wrap',
        gap: appTokens.space.md,
        paddingBottom: appTokens.space.sm,
        borderBottom: appTokens.border.subtle,
    },
    headerLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        minWidth: 0,
        maxWidth: '56rem',
    },
    eyebrow: {
        display: 'inline-flex',
        width: 'fit-content',
        color: appTokens.color.textMuted,
        fontWeight: appTokens.fontWeight.medium,
        textTransform: 'uppercase',
        letterSpacing: '0.08em',
    },
    title: {
        display: 'block',
        fontSize: 'clamp(1.65rem, 1.6vw + 1rem, 2.2rem)',
        lineHeight: 1.05,
        fontWeight: appTokens.fontWeight.bold,
        letterSpacing: '-0.03em',
        color: appTokens.color.textPrimary,
    },
    subtitle: {
        display: 'block',
        color: appTokens.color.textSecondary,
        maxWidth: '44rem',
    },
    headerMobile: {
        marginBottom: appTokens.space.lg,
        gap: appTokens.space.sm,
        paddingBottom: appTokens.space.xs,
    },
    actionsMobile: {
        width: '100%',
    },
    actionsDesktop: {
        marginLeft: 'auto',
    },
})

interface PageHeaderProps {
    title: string
    subtitle?: string
    actions?: ReactNode
    eyebrow?: string
}

export function PageHeader({ title, subtitle, actions, eyebrow }: PageHeaderProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()

    return (
        <div className={mergeClasses(styles.header, isMobile && styles.headerMobile)}>
            <div className={styles.headerLeft}>
                {eyebrow ? <Caption1 className={styles.eyebrow}>{eyebrow}</Caption1> : null}
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
