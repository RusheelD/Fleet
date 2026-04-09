import { makeStyles, mergeClasses, Title2, Body1 } from '@fluentui/react-components'
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
        gap: appTokens.space.lg,
    },
    headerLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
        minWidth: 0,
    },
    subtitle: {
        color: appTokens.color.textTertiary,
    },
    headerMobile: {
        marginBottom: appTokens.space.lg,
        gap: appTokens.space.md,
    },
    actionsMobile: {
        width: '100%',
    },
})

interface PageHeaderProps {
    title: string
    subtitle?: string
    actions?: ReactNode
}

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()

    return (
        <div className={mergeClasses(styles.header, isMobile && styles.headerMobile)}>
            <div className={styles.headerLeft}>
                <Title2>{title}</Title2>
                {subtitle && <Body1 className={styles.subtitle}>{subtitle}</Body1>}
            </div>
            {actions && (
                <div className={isMobile ? styles.actionsMobile : undefined}>
                    {actions}
                </div>
            )}
        </div>
    )
}
