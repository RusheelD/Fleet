import {
    makeStyles,
    Caption1,
    Text,
    Card,
    tokens,
    mergeClasses,
} from '@fluentui/react-components'
import { usePreferences, useIsMobile } from '../../hooks'
import type { KeyboardEvent, ReactNode } from 'react'

const useStyles = makeStyles({
    summaryCard: {
        padding: '0.75rem 1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: '160px',
        flex: '1 1 160px',
    },
    summaryCardCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
        minWidth: '120px',
        flex: '1 1 120px',
    },
    summaryCardMobile: {
        minWidth: 'calc(50% - 0.5rem)',
        flex: '1 1 calc(50% - 0.5rem)',
    },
    summaryIcon: {
        fontSize: '24px',
    },
    summaryIconCompact: {
        fontSize: '16px',
    },
    summaryValue: {
        fontSize: '20px',
        fontWeight: 700,
    },
    summaryValueCompact: {
        fontSize: '15px',
        lineHeight: '16px',
    },
    labelCompact: {
        fontSize: '11px',
        lineHeight: '14px',
    },
    captionBlock: {
        display: 'block' as const,
    },
    summaryCardInteractive: {
        cursor: 'pointer',
        userSelect: 'none',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    summaryCardActive: {
        backgroundColor: tokens.colorBrandBackground2,
        boxShadow: `inset 0 0 0 1px ${tokens.colorBrandStroke1}`,
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
