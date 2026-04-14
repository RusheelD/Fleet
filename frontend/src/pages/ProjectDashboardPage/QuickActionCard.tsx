import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
    Card,
} from '@fluentui/react-components'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    quickActionCard: {
        padding: '1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        cursor: 'pointer',
        border: appTokens.border.subtle,
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
        boxShadow: appTokens.shadow.card,
        ':hover': {
            boxShadow: appTokens.shadow.cardHover,
            transform: 'translateY(-1px)',
        },
    },
    quickActionCardCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        gap: '0.5rem',
    },
    quickActionCardMobile: {
        alignItems: 'flex-start',
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.875rem',
        paddingRight: '0.875rem',
    },
    quickActionIcon: {
        fontSize: '24px',
        color: appTokens.color.brand,
        flexShrink: 0,
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
    quickActionContent: {
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.125rem',
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
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false

    return (
        <Card
            className={mergeClasses(
                styles.quickActionCard,
                isCompact && styles.quickActionCardCompact,
                isMobile && !isCompact && styles.quickActionCardMobile,
            )}
            onClick={onClick}
        >
            <span className={mergeClasses(styles.quickActionIcon, isCompact && styles.quickActionIconCompact)}>{icon}</span>
            <div className={styles.quickActionContent}>
                <Text weight="semibold" block className={isCompact ? styles.quickActionTitleCompact : undefined}>
                    {title}
                </Text>
                <Caption1 className={isCompact ? styles.quickActionDescCompact : undefined}>{description}</Caption1>
            </div>
        </Card>
    )
}
