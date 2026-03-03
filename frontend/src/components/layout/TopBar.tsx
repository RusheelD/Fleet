import {
    makeStyles,
    tokens,
    Text,
    Button,
    Tooltip,
} from '@fluentui/react-components'
import {
    NavigationRegular,
    SearchRegular,
    GridRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'
import { UserMenu } from './'

const useStyles = makeStyles({
    topBar: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 1rem',
        minHeight: '48px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    topBarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    topBarRight: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
    },
    breadcrumb: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
    },
    breadcrumbItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
    },
    breadcrumbSep: {
        color: tokens.colorNeutralForeground4,
    },
    breadcrumbCurrent: {
        color: tokens.colorNeutralForeground1,
        fontWeight: 600,
    },
})

interface BreadcrumbEntry {
    label: string
    path?: string
}

interface TopBarProps {
    breadcrumbs: BreadcrumbEntry[]
    sidebarExpanded: boolean
    onExpandSidebar: () => void
}

export function TopBar({ breadcrumbs, sidebarExpanded, onExpandSidebar }: TopBarProps) {
    const styles = useStyles()
    const navigate = useNavigate()

    return (
        <div className={styles.topBar}>
            <div className={styles.topBarLeft}>
                {!sidebarExpanded && (
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<NavigationRegular />}
                        onClick={onExpandSidebar}
                        aria-label="Expand sidebar"
                    />
                )}
                <div className={styles.breadcrumb}>
                    {breadcrumbs.map((crumb, i) => (
                        <span key={i} className={styles.breadcrumbItem}>
                            {i > 0 && <Text className={styles.breadcrumbSep}>/</Text>}
                            {i === breadcrumbs.length - 1 ? (
                                <Text className={styles.breadcrumbCurrent}>{crumb.label}</Text>
                            ) : (
                                <Button
                                    appearance="transparent"
                                    size="small"
                                    onClick={() => crumb.path && navigate(crumb.path)}
                                >
                                    {crumb.label}
                                </Button>
                            )}
                        </span>
                    ))}
                </div>
            </div>
            <div className={styles.topBarRight}>
                <Tooltip content="Search" relationship="label">
                    <Button
                        appearance="subtle"
                        icon={<SearchRegular />}
                        size="small"
                        onClick={() => navigate('/search')}
                    />
                </Tooltip>
                <Tooltip content="View all projects" relationship="label">
                    <Button
                        appearance="subtle"
                        icon={<GridRegular />}
                        size="small"
                        onClick={() => navigate('/projects')}
                    />
                </Tooltip>
                <UserMenu />
            </div>
        </div>
    )
}
