import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Text,
    Card,
} from '@fluentui/react-components'
import { usePreferences } from '../../hooks'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    quickActionCard: {
        padding: '1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        cursor: 'pointer',
        ':hover': {
            boxShadow: tokens.shadow4,
        },
    },
    quickActionCardCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        gap: '0.5rem',
    },
    quickActionIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
    },
    quickActionIconCompact: {
        fontSize: '16px',
    },
    quickActionTitleCompact: {
        fontSize: '12px',
        lineHeight: '16px',
    },
    quickActionDescCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
})

interface QuickActionCardProps {
    icon: ReactNode
    title: string
    description: string
    onClick?: () => void
}

export function QuickActionCard({ icon, title, description, onClick }: QuickActionCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card className={mergeClasses(styles.quickActionCard, isCompact && styles.quickActionCardCompact)} onClick={onClick}>
            <span className={mergeClasses(styles.quickActionIcon, isCompact && styles.quickActionIconCompact)}>{icon}</span>
            <div>
                <Text weight="semibold" block className={isCompact ? styles.quickActionTitleCompact : undefined}>
                    {title}
                </Text>
                <Caption1 className={isCompact ? styles.quickActionDescCompact : undefined}>{description}</Caption1>
            </div>
        </Card>
    )
}
