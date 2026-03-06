import {
    makeStyles,
    mergeClasses,
    tokens,
    Tooltip,
} from '@fluentui/react-components'
import { useNavigate } from 'react-router-dom'
import type { NavItemConfig } from '../../models'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    navItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        padding: '0 0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        color: tokens.colorNeutralForeground2,
        transitionProperty: 'background, color, transform',
        transitionDuration: '0.12s',
        borderTopStyle: 'none',
        borderRightStyle: 'none',
        borderBottomStyle: 'none',
        borderLeftStyle: 'none',
        backgroundColor: 'transparent',
        width: '100%',
        textAlign: 'left',
        fontSize: '13px',
        minHeight: '36px',
        position: 'relative',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            color: tokens.colorNeutralForeground1,
            transform: 'translateX(1px)',
        },
        ':focus-visible': {
            outline: `2px solid ${tokens.colorStrokeFocus2}`,
            outlineOffset: '-2px',
        },
    },
    navItemCompact: {
        minHeight: '30px',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
        fontSize: '12px',
    },
    navItemCollapsed: {
        justifyContent: 'center',
        padding: 0,
    },
    navItemActive: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground1,
        fontWeight: 600,
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2Hover,
            color: tokens.colorBrandForeground1,
        },
    },
    navItemAccent: {
        position: 'absolute' as const,
        left: 0,
        top: '6px',
        bottom: '6px',
        width: '3px',
        borderRadius: '0 2px 2px 0',
        backgroundColor: tokens.colorBrandForeground1,
    },
    navItemIcon: {
        fontSize: '18px',
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '20px',
    },
    navItemIconCompact: {
        fontSize: '16px',
        width: '16px',
    },
    navItemLabel: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
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
