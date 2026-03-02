import { makeStyles, tokens, Title2, Body1 } from '@fluentui/react-components'
import type { ReactNode } from 'react'

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
    },
    subtitle: {
        color: tokens.colorNeutralForeground3,
    },
})

interface PageHeaderProps {
    title: string
    subtitle?: string
    actions?: ReactNode
}

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
    const styles = useStyles()

    return (
        <div className={styles.header}>
            <div className={styles.headerLeft}>
                <Title2>{title}</Title2>
                {subtitle && <Body1 className={styles.subtitle}>{subtitle}</Body1>}
            </div>
            {actions}
        </div>
    )
}
