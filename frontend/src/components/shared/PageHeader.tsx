import { makeStyles, mergeClasses, tokens, Title2, Body1 } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { useIsMobile } from '../../hooks'

const useStyles = makeStyles({
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: '1.5rem',
        flexWrap: 'wrap',
        gap: '1rem',
    },
    headerLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
        minWidth: 0,
    },
    subtitle: {
        color: tokens.colorNeutralForeground3,
    },
    headerMobile: {
        marginBottom: '1rem',
        gap: '0.75rem',
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
