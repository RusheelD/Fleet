import {
    makeStyles,
    mergeClasses,
    Text,
    Button,
} from '@fluentui/react-components'
import {
    ChevronLeftRegular,
} from '@fluentui/react-icons'
import { appTokens } from '../../styles/appTokens'
import { FleetRocketLogo } from '../shared'

const useStyles = makeStyles({
    sidebarHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        paddingTop: '0.75rem',
        paddingRight: '0.75rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.75rem',
        minHeight: appTokens.size.sidebarHeader,
        flexShrink: 0,
        borderBottom: appTokens.border.subtle,
        position: 'relative',
        backgroundColor: appTokens.color.surfaceAlt,
    },
    sidebarHeaderCollapsed: {
        justifyContent: 'center',
        padding: 0,
        gap: 0,
    },
    brandIcon: {
        flexShrink: 0,
        width: '22px',
        height: '22px',
    },
    brandName: {
        fontWeight: 700,
        fontSize: '15px',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        letterSpacing: '0.01em',
    },
    brandCopy: {
        display: 'flex',
        flexDirection: 'column',
        minWidth: 0,
        gap: '0.125rem',
    },
    brandTagline: {
        fontSize: '11px',
        lineHeight: '14px',
        color: appTokens.color.textMuted,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
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
                    <FleetRocketLogo className={styles.brandIcon} size={22} title="Fleet" />
                    <div className={styles.brandCopy}>
                        <Text className={styles.brandName}>Fleet</Text>
                        <Text className={styles.brandTagline}>Agent workspace</Text>
                    </div>
                </>
            ) : (
                <FleetRocketLogo className={styles.brandIcon} size={22} title="Fleet" />
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
