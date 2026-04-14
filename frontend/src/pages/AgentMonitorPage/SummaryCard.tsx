import {
    makeStyles,
    Caption1,
    Text,
    Card,
    mergeClasses,
} from '@fluentui/react-components'
import { usePreferences, useIsMobile } from '../../hooks'
import type { KeyboardEvent, ReactNode } from 'react'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    summaryCard: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
        minWidth: '160px',
        flex: '1 1 160px',
        border: appTokens.border.subtle,
        transitionProperty: 'background-color, box-shadow, border-color, transform',
        transitionDuration: appTokens.motion.fast,
        backgroundImage: `linear-gradient(145deg, ${appTokens.color.surface} 0%, ${appTokens.color.surfaceAlt} 100%)`,
        boxShadow: appTokens.shadow.card,
    },
    summaryCardCompact: {
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.sm,
        minWidth: '120px',
        flex: '1 1 120px',
    },
    summaryCardMobile: {
        minWidth: `calc(50% - ${appTokens.space.sm})`,
        flex: `1 1 calc(50% - ${appTokens.space.sm})`,
    },
    summaryIcon: {
        fontSize: appTokens.fontSize.iconLg,
        width: '2rem',
        height: '2rem',
        borderRadius: appTokens.radius.md,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: appTokens.color.surfaceRaised,
    },
    summaryIconCompact: {
        fontSize: appTokens.fontSize.iconSm,
    },
    summaryValue: {
        fontSize: appTokens.fontSize.xl,
        fontWeight: appTokens.fontWeight.bold,
    },
    summaryValueCompact: {
        fontSize: appTokens.fontSize.lg,
        lineHeight: appTokens.lineHeight.snug,
    },
    labelCompact: {
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
    captionBlock: {
        display: 'block' as const,
    },
    summaryCardInteractive: {
        cursor: 'pointer',
        userSelect: 'none',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
            transform: 'translateY(-1px)',
        },
    },
    summaryCardActive: {
        backgroundColor: appTokens.color.surfaceSelected,
        boxShadow: appTokens.border.activeInset,
        borderTopColor: appTokens.color.brandStroke,
        borderRightColor: appTokens.color.brandStroke,
        borderBottomColor: appTokens.color.brandStroke,
        borderLeftColor: appTokens.color.brandStroke,
    },
})

interface SummaryCardProps {
    icon: ReactNode
    iconClassName?: string
    value: number
    label: string
    onClick?: () => void
    isActive?: boolean
}

export function SummaryCard({ icon, iconClassName, value, label, onClick, isActive = false }: SummaryCardProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const isInteractive = Boolean(onClick)

    const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
        if (!onClick) {
            return
        }

        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault()
            onClick()
        }
    }

    return (
        <Card
            className={mergeClasses(
                styles.summaryCard,
                isCompact && styles.summaryCardCompact,
                isMobile && styles.summaryCardMobile,
                isInteractive && styles.summaryCardInteractive,
                isActive && styles.summaryCardActive,
            )}
            onClick={onClick}
            onKeyDown={handleKeyDown}
            role={isInteractive ? 'button' : undefined}
            tabIndex={isInteractive ? 0 : undefined}
            aria-pressed={isInteractive ? isActive : undefined}
        >
            <span className={mergeClasses(styles.summaryIcon, isCompact && styles.summaryIconCompact, iconClassName)}>
                {icon}
            </span>
            <div>
                <Text className={mergeClasses(styles.summaryValue, isCompact && styles.summaryValueCompact)}>{value}</Text>
                <Caption1 className={mergeClasses(styles.captionBlock, isCompact && styles.labelCompact)}>{label}</Caption1>
            </div>
        </Card>
    )
}
