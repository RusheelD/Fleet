import { makeStyles, Tooltip, mergeClasses } from '@fluentui/react-components'
import type { WorkItemLevel } from '../../models'
import { resolveLevelIcon } from '../../proxies/levelIconMap'

const useStyles = makeStyles({
    badge: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '0.25rem',
        fontSize: '11px',
        fontWeight: 600,
        lineHeight: 1,
        padding: '2px 6px',
        borderRadius: '3px',
        whiteSpace: 'nowrap',
        flexShrink: 0,
    },
    icon: {
        fontSize: '12px',
        display: 'flex',
        alignItems: 'center',
    },
})

interface LevelBadgeProps {
    level: WorkItemLevel | undefined
    className?: string
}

export function LevelBadge({ level, className }: LevelBadgeProps) {
    const styles = useStyles()

    if (!level) return null

    const bgColor = `${level.color}20` // 12% opacity background
    const textColor = level.color

    return (
        <Tooltip content={level.name} relationship="label">
            <span
                className={mergeClasses(styles.badge, className)}
                style={{ backgroundColor: bgColor, color: textColor }}
            >
                <span className={styles.icon}>{resolveLevelIcon(level.iconName)}</span>
                {level.name}
            </span>
        </Tooltip>
    )
}
