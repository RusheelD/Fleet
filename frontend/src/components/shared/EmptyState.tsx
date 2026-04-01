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
    },
    emptyIcon: {
        fontSize: appTokens.fontSize.iconXl,
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
