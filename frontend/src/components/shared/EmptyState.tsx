import { makeStyles, tokens, Body1, Caption1 } from '@fluentui/react-components'
import { SearchRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        padding: '3rem',
        gap: '0.75rem',
        color: tokens.colorNeutralForeground3,
    },
    emptyIcon: {
        fontSize: '48px',
    },
})

interface EmptyStateProps {
    icon?: ReactNode
    title: string
    description?: string
}

export function EmptyState({ icon, title, description }: EmptyStateProps) {
    const styles = useStyles()

    return (
        <div className={styles.emptyState}>
            {icon ?? <SearchRegular className={styles.emptyIcon} />}
            <Body1>{title}</Body1>
            {description && <Caption1>{description}</Caption1>}
        </div>
    )
}
