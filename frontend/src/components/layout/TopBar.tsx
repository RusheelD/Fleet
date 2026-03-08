import {
    makeStyles,
    mergeClasses,
    tokens,
    Text,
    Button,
    Tooltip,
    Badge,
} from '@fluentui/react-components'
import {
    SearchRegular,
    GridRegular,
    ChatRegular,
    AlertRegular,
    NavigationRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'
import { UserMenu } from './'
import { useMarkAllNotificationsAsRead, useNotifications } from '../../proxies'
import { useAuth, usePreferences } from '../../hooks'

const useStyles = makeStyles({
    topBar: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 1.25rem',
        minHeight: '52px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
        backdropFilter: 'blur(2px)',
    },
    topBarCompact: {
        paddingTop: 0,
        paddingBottom: 0,
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
        minHeight: '42px',
    },
    topBarMobile: {
        paddingTop: 0,
        paddingBottom: 0,
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        minHeight: '44px',
    },
    topBarLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.625rem',
        minWidth: 0,
    },
    topBarLeftMobile: {
        gap: '0.375rem',
        flex: 1,
    },
    topBarRight: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.375rem',
        flexShrink: 0,
    },
    topBarRightCompact: {
        gap: '0.125rem',
    },
    topBarRightMobile: {
        gap: 0,
    },
    tierBadge: {
        textTransform: 'uppercase',
        fontWeight: 600,
        letterSpacing: '0.04em',
        fontSize: '11px',
        lineHeight: '16px',
        minHeight: '22px',
        paddingTop: '2px',
        paddingBottom: '2px',
        paddingLeft: '8px',
        paddingRight: '8px',
    },
    tierBadgeCompact: {
        minHeight: '20px',
        paddingTop: '1px',
        paddingBottom: '1px',
        paddingLeft: '7px',
        paddingRight: '7px',
    },
    notificationWrapper: {
        position: 'relative',
        display: 'inline-flex',
    },
    notificationBadge: {
        position: 'absolute',
        top: '-4px',
        right: '-4px',
        pointerEvents: 'none',
    },
    breadcrumb: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
        minWidth: 0,
        overflow: 'hidden',
    },
    breadcrumbCompact: {
        fontSize: '12px',
        gap: '0.125rem',
    },
    breadcrumbMobile: {
        fontSize: '12px',
        maxWidth: '100%',
    },
    breadcrumbItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        minWidth: 0,
    },
    breadcrumbSep: {
        color: tokens.colorNeutralForeground4,
    },
    breadcrumbLink: {
        minWidth: 0,
        maxWidth: '260px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    breadcrumbCurrent: {
        color: tokens.colorNeutralForeground1,
        fontWeight: 600,
        minWidth: 0,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    sidebarToggleButton: {
        flexShrink: 0,
    },
})

interface BreadcrumbEntry {
    label: string
    path?: string
}

interface TopBarProps {
    breadcrumbs: BreadcrumbEntry[]
    chatOpen?: boolean
    onToggleChat?: () => void
    isMobile?: boolean
    onToggleSidebar?: () => void
}

export function TopBar({ breadcrumbs, chatOpen, onToggleChat, isMobile = false, onToggleSidebar }: TopBarProps) {
    const styles = useStyles()
    const navigate = useNavigate()
    const { user } = useAuth()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const { data: notifications } = useNotifications(true)
    const markAllRead = useMarkAllNotificationsAsRead()
    const unreadCount = notifications?.length ?? 0
    const tier = (user?.role ?? 'free').toString().toUpperCase()
    const visibleBreadcrumbs = isMobile ? breadcrumbs.slice(-1) : breadcrumbs

    return (
        <div className={mergeClasses(styles.topBar, isCompact && styles.topBarCompact, isMobile && styles.topBarMobile)}>
            <div className={mergeClasses(styles.topBarLeft, isMobile && styles.topBarLeftMobile)}>
                {isMobile && onToggleSidebar && (
                    <Tooltip content="Open navigation" relationship="label">
                        <Button
                            appearance="subtle"
                            icon={<NavigationRegular />}
                            size="small"
                            onClick={onToggleSidebar}
                            className={styles.sidebarToggleButton}
                        />
                    </Tooltip>
                )}
                <div className={mergeClasses(styles.breadcrumb, isCompact && styles.breadcrumbCompact, isMobile && styles.breadcrumbMobile)}>
                    {visibleBreadcrumbs.map((crumb, i) => (
                        <span key={i} className={styles.breadcrumbItem}>
                            {i > 0 && <Text className={styles.breadcrumbSep}>/</Text>}
                            {i === visibleBreadcrumbs.length - 1 ? (
                                <Text className={styles.breadcrumbCurrent}>{crumb.label}</Text>
                            ) : (
                                <Button
                                    appearance="transparent"
                                    size="small"
                                    className={styles.breadcrumbLink}
                                    onClick={() => crumb.path && navigate(crumb.path)}
                                >
                                    {crumb.label}
                                </Button>
                            )}
                        </span>
                    ))}
                </div>
            </div>
            <div className={mergeClasses(styles.topBarRight, isCompact && styles.topBarRightCompact, isMobile && styles.topBarRightMobile)}>
                {!isMobile && (
                    <Badge
                        appearance="outline"
                        color="brand"
                        size="small"
                        className={mergeClasses(styles.tierBadge, isCompact && styles.tierBadgeCompact)}
                    >
                        {tier}
                    </Badge>
                )}
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
                <Tooltip content={chatOpen ? 'Close AI Chat' : 'Open AI Chat'} relationship="label">
                    <Button
                        appearance={chatOpen ? 'primary' : 'subtle'}
                        icon={<ChatRegular />}
                        size="small"
                        onClick={onToggleChat}
                    />
                </Tooltip>
                <Tooltip
                    content={unreadCount > 0 ? `${unreadCount} unread notifications (click to mark all read)` : 'No unread notifications'}
                    relationship="label"
                >
                    <span className={styles.notificationWrapper}>
                        <Button
                            appearance={unreadCount > 0 ? 'primary' : 'subtle'}
                            icon={<AlertRegular />}
                            size="small"
                            onClick={() => {
                                if (unreadCount > 0 && !markAllRead.isPending) {
                                    markAllRead.mutate()
                                }
                            }}
                        />
                        {unreadCount > 0 && (
                            <Badge appearance="filled" color="danger" size="tiny" className={styles.notificationBadge}>
                                {unreadCount > 99 ? '99+' : unreadCount}
                            </Badge>
                        )}
                    </span>
                </Tooltip>
                <UserMenu />
            </div>
        </div>
    )
}
