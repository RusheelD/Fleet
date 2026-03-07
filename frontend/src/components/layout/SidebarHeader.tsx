import {
    makeStyles,
    mergeClasses,
    tokens,
    Text,
    Button,
} from '@fluentui/react-components'
import {
    RocketRegular,
    ChevronLeftRegular,
} from '@fluentui/react-icons'

const useStyles = makeStyles({
    sidebarHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.625rem',
        padding: '0 0.625rem',
        minHeight: '52px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        position: 'relative',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    sidebarHeaderCollapsed: {
        justifyContent: 'center',
        padding: 0,
        gap: 0,
    },
    brandIcon: {
        color: tokens.colorPaletteDarkOrangeForeground1,
        fontSize: '20px',
        flexShrink: 0,
    },
    brandName: {
        fontWeight: 700,
        fontSize: '15px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        letterSpacing: '0.01em',
    },
    collapseButton: {
        marginLeft: 'auto',
        flexShrink: 0,
    },
})

interface SidebarHeaderProps {
    expanded: boolean
    onToggle: () => void
}

export function SidebarHeader({ expanded, onToggle }: SidebarHeaderProps) {
    const styles = useStyles()

    return (
        <div className={mergeClasses(styles.sidebarHeader, !expanded && styles.sidebarHeaderCollapsed)}>
            {expanded ? (
                <>
                    <RocketRegular className={styles.brandIcon} />
                    <Text className={styles.brandName}>Fleet</Text>
                </>
            ) : (
                <RocketRegular className={styles.brandIcon} />
            )}
            {expanded && (
                <Button
                    appearance="subtle"
                    size="small"
                    icon={<ChevronLeftRegular />}
                    onClick={onToggle}
                    className={styles.collapseButton}
                    aria-label="Collapse sidebar"
                />
            )}
        </div>
    )
}
