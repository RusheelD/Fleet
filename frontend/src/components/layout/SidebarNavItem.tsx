import {
    Badge,
    makeStyles,
    mergeClasses,
    Tooltip,
} from '@fluentui/react-components'
import { useNavigate } from 'react-router-dom'
import type { NavItemConfig } from '../../models'
import { usePreferences } from '../../hooks/PreferencesContext'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    navItem: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
        padding: `0 ${appTokens.space.md}`,
        borderRadius: appTokens.radius.md,
        cursor: 'pointer',
        color: appTokens.color.textSecondary,
        transitionProperty: 'background, color, transform',
        transitionDuration: appTokens.motion.fast,
        borderTopStyle: 'none',
        borderRightStyle: 'none',
        borderBottomStyle: 'none',
        borderLeftStyle: 'none',
        backgroundColor: 'transparent',
        width: '100%',
        textAlign: 'left',
        fontSize: appTokens.fontSize.md,
        minHeight: '36px',
        position: 'relative',
        ':hover': {
            backgroundColor: appTokens.color.surfaceHover,
            color: appTokens.color.textPrimary,
            transform: 'translateX(1px)',
        },
        ':focus-visible': {
            outline: `2px solid ${appTokens.color.focusOutline}`,
            outlineOffset: '-2px',
        },
    },
    navItemCompact: {
        minHeight: '30px',
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.sm,
        fontSize: appTokens.fontSize.sm,
    },
    navItemCollapsed: {
        justifyContent: 'center',
        padding: 0,
    },
    navItemActive: {
        backgroundColor: appTokens.color.surfaceBrand,
        color: appTokens.color.brand,
        fontWeight: appTokens.fontWeight.semibold,
        ':hover': {
            backgroundColor: appTokens.color.surfaceBrandHover,
            color: appTokens.color.brand,
        },
    },
    navItemAccent: {
        position: 'absolute' as const,
        left: 0,
        top: '6px',
        bottom: '6px',
        width: '3px',
        borderRadius: '0 2px 2px 0',
        backgroundColor: appTokens.color.brand,
    },
    navItemIcon: {
        fontSize: appTokens.fontSize.iconMd,
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '20px',
    },
    navItemIconCompact: {
        fontSize: appTokens.fontSize.iconSm,
        width: '16px',
    },
    navItemLabel: {
        flex: 1,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    navItemBadge: {
        marginLeft: 'auto',
        maxWidth: '64px',
    },
    navItemBadgeCollapsed: {
        position: 'absolute',
        top: '4px',
        right: '4px',
        pointerEvents: 'none',
    },
})

interface SidebarNavItemProps {
    item: NavItemConfig
    active: boolean
    expanded: boolean
}

export function SidebarNavItem({ item, active, expanded }: SidebarNavItemProps) {
    const styles = useStyles()
    const navigate = useNavigate()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const hasBadge = item.badge !== undefined && item.badge !== null && item.badge !== '' && item.badge !== 0
    const badgeLabel = typeof item.badge === 'number' && item.badge > 99 ? '99+' : item.badge

    const button = (
        <button
            type="button"
            className={mergeClasses(
                styles.navItem,
                isCompact && styles.navItemCompact,
                !expanded && styles.navItemCollapsed,
                active ? styles.navItemActive : undefined,
            )}
            onClick={() => navigate(item.path)}
        >
            {active && <span className={styles.navItemAccent} />}
            <span className={mergeClasses(styles.navItemIcon, isCompact && styles.navItemIconCompact)}>{item.icon}</span>
            {expanded && <span className={styles.navItemLabel}>{item.label}</span>}
            {hasBadge && (
                <Badge
                    appearance="filled"
                    color="danger"
                    size="tiny"
                    className={expanded ? styles.navItemBadge : styles.navItemBadgeCollapsed}
                >
                    {badgeLabel}
                </Badge>
            )}
        </button>
    )

    if (!expanded) {
        return (
            <Tooltip content={item.label} relationship="label" positioning="after">
                {button}
            </Tooltip>
        )
    }
    return button
}
