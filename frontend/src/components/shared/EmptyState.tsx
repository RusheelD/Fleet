import { makeStyles, Body1, Caption1 } from '@fluentui/react-components'
import { SearchRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        padding: appTokens.space.xxl,
        gap: appTokens.space.md,
        color: appTokens.color.textTertiary,
        textAlign: 'center',
    },
    emptyIcon: {
        fontSize: appTokens.fontSize.iconXl,
    },
    emptyCopy: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xs,
        maxWidth: '34rem',
        alignItems: 'center',
    },
    emptyTitle: {
        color: appTokens.color.textPrimary,
        fontWeight: appTokens.fontWeight.semibold,
    },
    emptyDescription: {
        color: appTokens.color.textSecondary,
    },
    emptyActions: {
        display: 'flex',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
        justifyContent: 'center',
    },
})

interface EmptyStateProps {
    icon?: ReactNode
    title: string
    description?: string
    actions?: ReactNode
}

export function EmptyState({ icon, title, description, actions }: EmptyStateProps) {
    const styles = useStyles()

    return (
        <div className={styles.emptyState}>
            {icon ?? <SearchRegular className={styles.emptyIcon} />}
            <div className={styles.emptyCopy}>
                <Body1 className={styles.emptyTitle}>{title}</Body1>
                {description && <Caption1 className={styles.emptyDescription}>{description}</Caption1>}
            </div>
            {actions && <div className={styles.emptyActions}>{actions}</div>}
        </div>
    )
}
