import {
    makeStyles,
    tokens,
    Text,
    Button,
    Tooltip,
} from '@fluentui/react-components'
import {
    RocketRegular,
    ChevronLeftRegular,
    NavigationRegular,
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
    collapsedToggle: {
        position: 'absolute' as const,
        left: '6px',
        top: '10px',
    },
})

interface SidebarHeaderProps {
    expanded: boolean
    onToggle: () => void
}

export function SidebarHeader({ expanded, onToggle }: SidebarHeaderProps) {
    const styles = useStyles()

    return (
        <div className={styles.sidebarHeader}>
            <RocketRegular className={styles.brandIcon} />
            {expanded && <Text className={styles.brandName}>Fleet</Text>}
            {expanded ? (
                <Button
                    appearance="subtle"
                    size="small"
                    icon={<ChevronLeftRegular />}
                    onClick={onToggle}
                    className={styles.collapseButton}
                    aria-label="Collapse sidebar"
                />
            ) : (
                <Tooltip content="Expand sidebar" relationship="label" positioning="after">
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<NavigationRegular />}
                        onClick={onToggle}
                        className={styles.collapsedToggle}
                        aria-label="Expand sidebar"
                    />
                </Tooltip>
            )}
        </div>
    )
}
