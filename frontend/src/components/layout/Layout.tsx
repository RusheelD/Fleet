import { useState, useCallback, useRef, useEffect } from 'react'
import { useLocation, Outlet, useParams } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Text,
    Button,
    Tooltip,
} from '@fluentui/react-components'
import {
    FolderRegular,
    SearchRegular,
    SettingsRegular,
    CreditCardPersonRegular,
    BoardRegular,
    BotRegular,
    HomeFilled,
    NavigationRegular,
    DismissRegular,
} from '@fluentui/react-icons'

import { SidebarHeader, ProjectSelector, SidebarNavItem, TopBar } from './'
import { SplitView } from '../shared'
import { ChatDrawer } from '../chat'
import { useCurrentProject, usePreferences, useServerEvents, ChatGeneratingProvider, useIsMobile } from '../../hooks'

import type { NavItemConfig } from '../../models'

const SIDEBAR_WIDTH_EXPANDED = '260px'
const SIDEBAR_WIDTH_COLLAPSED = '48px'
const SIDEBAR_WIDTH_EXPANDED_COMPACT = '220px'
const SIDEBAR_WIDTH_COLLAPSED_COMPACT = '44px'

const useStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground2,
        height: '100%',
    },
    rootMobile: {
        position: 'relative',
        overflow: 'hidden',
    },

    sidebarPane: {
        flexShrink: 0,
    },

    sidebar: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
        transition: 'width 0.2s ease',
        overflowX: 'hidden',
        overflowY: 'auto',
        flexShrink: 0,
        boxShadow: `inset -1px 0 0 ${tokens.colorNeutralStroke2}`,
    },
    sidebarMobile: {
        height: '100vh',
        boxShadow: tokens.shadow64,
    },
    sidebarExpanded: {
        width: SIDEBAR_WIDTH_EXPANDED,
    },
    sidebarExpandedCompact: {
        width: SIDEBAR_WIDTH_EXPANDED_COMPACT,
    },
    sidebarCollapsed: {
        width: SIDEBAR_WIDTH_COLLAPSED,
    },
    sidebarCollapsedCompact: {
        width: SIDEBAR_WIDTH_COLLAPSED_COMPACT,
    },

    mobileSidebarBackdrop: {
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.35)',
        zIndex: 30,
    },
    mobileSidebarDrawer: {
        position: 'fixed',
        top: 0,
        left: 0,
        bottom: 0,
        width: 'min(82vw, 300px)',
        zIndex: 31,
        transform: 'translateX(-100%)',
        transition: 'transform 0.2s ease',
        pointerEvents: 'none',
    },
    mobileSidebarDrawerOpen: {
        transform: 'translateX(0)',
        pointerEvents: 'auto',
    },
    mobileSidebarCloseRow: {
        display: 'flex',
        justifyContent: 'flex-end',
        paddingTop: '0.25rem',
        paddingBottom: 0,
        paddingLeft: '0.25rem',
        paddingRight: '0.25rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
    },

    navSection: {
        display: 'flex',
        flexDirection: 'column',
        padding: '0.5rem 0.375rem 0',
        gap: '2px',
    },
    navSectionCompact: {
        paddingTop: '0.25rem',
        paddingLeft: '0.25rem',
        paddingRight: '0.25rem',
    },
    navSectionLabel: {
        padding: '0.375rem 0.625rem 0.25rem',
        fontSize: '11px',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: tokens.colorNeutralForeground4,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        userSelect: 'none',
    },
    navSectionLabelCompact: {
        paddingTop: '0.25rem',
        paddingBottom: '0.125rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        fontSize: '10px',
    },

    sidebarFooter: {
        marginTop: 'auto',
        padding: '0.375rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
    },
    sidebarFooterCompact: {
        padding: '0.25rem',
    },
    collapsedTopSlot: {
        marginTop: '0.375rem',
        marginLeft: '0.375rem',
        marginRight: '0.375rem',
        display: 'flex',
        justifyContent: 'center',
    },
    collapsedTopSlotCompact: {
        marginTop: '0.25rem',
        marginLeft: '0.25rem',
        marginRight: '0.25rem',
    },
    collapsedExpandButton: {
        width: '100%',
        minHeight: '34px',
    },
    collapsedExpandButtonCompact: {
        minHeight: '30px',
    },

    content: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
        backgroundColor: tokens.colorNeutralBackground3,
    },
    mainContent: {
        flex: 1,
        overflow: 'auto',
        minWidth: 0,
    },
    contentWithChat: {
        display: 'flex',
        flexDirection: 'row',
        flex: 1,
        overflow: 'hidden',
        minWidth: 0,
    },
    chatPane: {
        flexShrink: 0,
        height: '100%',
        display: 'flex',
        flexDirection: 'row',
        backgroundColor: tokens.colorNeutralBackground1,
        borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
        transition: 'width 0.18s ease',
    },
    chatPaneResizing: {
        transition: 'none',
    },
    chatOverlayMobile: {
        position: 'fixed',
        inset: 0,
        zIndex: 40,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    resizeHandle: {
        width: '6px',
        cursor: 'col-resize',
        backgroundColor: 'transparent',
        flexShrink: 0,
        transition: 'background-color 0.15s',
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
        },
    },
    resizeHandleActive: {
        backgroundColor: tokens.colorBrandBackground2,
    },
})

export function Layout() {
    const styles = useStyles()
    const location = useLocation()
    const { slug } = useParams()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const [sidebarExpanded, setSidebarExpanded] = useState(!preferences?.sidebarCollapsed)
    const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)
    const [chatOpen, setChatOpen] = useState(false)

    const { projectId, projectTitle } = useCurrentProject()
    useServerEvents(projectId)

    useEffect(() => {
        if (location.state?.openChat) {
            setChatOpen(true)
            window.history.replaceState({}, '')
        }
    }, [location.state])

    useEffect(() => {
        if (typeof preferences?.sidebarCollapsed === 'boolean') {
            setSidebarExpanded(!preferences.sidebarCollapsed)
        }
    }, [preferences?.sidebarCollapsed])

    useEffect(() => {
        if (!isMobile) {
            setMobileSidebarOpen(false)
        }
    }, [isMobile])

    useEffect(() => {
        if (isMobile) {
            setMobileSidebarOpen(false)
        }
    }, [isMobile, location.pathname])

    const MIN_CHAT_WIDTH = 340
    const MAX_CHAT_WIDTH = 800
    const DEFAULT_CHAT_WIDTH = 480
    const [chatWidth, setChatWidth] = useState(DEFAULT_CHAT_WIDTH)
    const isResizing = useRef(false)
    const [isResizingActive, setIsResizingActive] = useState(false)

    const handleResizeStart = useCallback(() => {
        if (isMobile) {
            return
        }

        isResizing.current = true
        setIsResizingActive(true)
        document.body.style.cursor = 'col-resize'
        document.body.style.userSelect = 'none'
    }, [isMobile])

    const handleAutoExpandChat = useCallback((requestedWidth: number) => {
        if (isMobile || isResizing.current) {
            return
        }

        setChatWidth((currentWidth) => {
            const nextWidth = Math.min(MAX_CHAT_WIDTH, Math.max(currentWidth, requestedWidth))
            return nextWidth
        })
    }, [isMobile])

    useEffect(() => {
        const handleMouseMove = (e: MouseEvent) => {
            if (!isResizing.current || isMobile) return
            const newWidth = window.innerWidth - e.clientX
            setChatWidth(Math.min(MAX_CHAT_WIDTH, Math.max(MIN_CHAT_WIDTH, newWidth)))
        }
        const handleMouseUp = () => {
            if (isResizing.current) {
                isResizing.current = false
                setIsResizingActive(false)
                document.body.style.cursor = ''
                document.body.style.userSelect = ''
            }
        }
        window.addEventListener('mousemove', handleMouseMove)
        window.addEventListener('mouseup', handleMouseUp)
        return () => {
            window.removeEventListener('mousemove', handleMouseMove)
            window.removeEventListener('mouseup', handleMouseUp)
        }
    }, [isMobile])

    const isActive = useCallback(
        (path: string, exact?: boolean) => {
            if (exact) return location.pathname === path
            if (path === '/projects' && location.pathname === '/projects') return true
            if (path !== '/projects' && location.pathname.startsWith(path)) return true
            return false
        },
        [location.pathname],
    )

    const globalNav: NavItemConfig[] = [
        { icon: <FolderRegular />, label: 'All Projects', path: '/projects', exact: true },
        { icon: <SearchRegular />, label: 'Search', path: '/search' },
    ]

    const projectNav: NavItemConfig[] = slug
        ? [
            { icon: <HomeFilled />, label: 'Overview', path: `/projects/${slug}`, exact: true },
            { icon: <BoardRegular />, label: 'Work Items', path: `/projects/${slug}/work-items` },
            { icon: <BotRegular />, label: 'Agents', path: `/projects/${slug}/agents` },
        ]
        : []

    const bottomNav: NavItemConfig[] = [
        { icon: <SettingsRegular />, label: 'Settings', path: '/settings' },
        { icon: <CreditCardPersonRegular />, label: 'Subscription', path: '/subscription' },
    ]

    const getBreadcrumbs = () => {
        const parts: Array<{ label: string; path?: string }> = []

        if (slug) {
            parts.push({ label: 'Projects', path: '/projects' })
            const displayName = slug.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
            parts.push({ label: displayName, path: `/projects/${slug}` })

            if (location.pathname.includes('/work-items')) {
                parts.push({ label: 'Work Items' })
            } else if (location.pathname.includes('/agents')) {
                parts.push({ label: 'Agents' })
            } else {
                parts.push({ label: 'Overview' })
            }
        } else if (location.pathname === '/settings') {
            parts.push({ label: 'Settings' })
        } else if (location.pathname === '/subscription') {
            parts.push({ label: 'Subscription' })
        } else if (location.pathname === '/search') {
            parts.push({ label: 'Search' })
        } else {
            parts.push({ label: 'Projects' })
        }

        return parts
    }

    const breadcrumbs = getBreadcrumbs()
    const isCompact = preferences?.compactMode ?? false
    const sidebarIsExpanded = isMobile ? true : sidebarExpanded

    const sidebarNav = (
        <nav
            className={mergeClasses(
                styles.sidebar,
                isMobile && styles.sidebarMobile,
                sidebarIsExpanded ? styles.sidebarExpanded : styles.sidebarCollapsed,
                isCompact && sidebarIsExpanded && styles.sidebarExpandedCompact,
                isCompact && !sidebarIsExpanded && styles.sidebarCollapsedCompact,
            )}
        >
            {isMobile && (
                <div className={styles.mobileSidebarCloseRow}>
                    <Tooltip content="Close navigation" relationship="label">
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<DismissRegular />}
                            onClick={() => setMobileSidebarOpen(false)}
                            aria-label="Close navigation"
                        />
                    </Tooltip>
                </div>
            )}
            <SidebarHeader
                expanded={sidebarIsExpanded}
                onToggle={() => {
                    if (isMobile) {
                        setMobileSidebarOpen(false)
                    } else {
                        setSidebarExpanded((prev) => !prev)
                    }
                }}
            />

            {sidebarIsExpanded && slug && (
                <ProjectSelector projectName={projectTitle ?? slug} expanded={sidebarIsExpanded} />
            )}

            {!sidebarIsExpanded && !isMobile && (
                <div className={mergeClasses(styles.collapsedTopSlot, isCompact && styles.collapsedTopSlotCompact)}>
                    <Tooltip content="Expand sidebar" relationship="label" positioning="after">
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<NavigationRegular />}
                            onClick={() => setSidebarExpanded(true)}
                            className={mergeClasses(
                                styles.collapsedExpandButton,
                                isCompact && styles.collapsedExpandButtonCompact,
                            )}
                            aria-label="Expand sidebar"
                        />
                    </Tooltip>
                </div>
            )}

            <div className={mergeClasses(styles.navSection, isCompact && styles.navSectionCompact)}>
                {sidebarIsExpanded && (
                    <Text className={mergeClasses(styles.navSectionLabel, isCompact && styles.navSectionLabelCompact)}>
                        Navigate
                    </Text>
                )}
                {globalNav.map((item) => (
                    <SidebarNavItem
                        key={item.path}
                        item={item}
                        active={isActive(item.path, item.exact)}
                        expanded={sidebarIsExpanded}
                    />
                ))}
            </div>

            {slug && (
                <div className={mergeClasses(styles.navSection, isCompact && styles.navSectionCompact)}>
                    {sidebarIsExpanded && (
                        <Text className={mergeClasses(styles.navSectionLabel, isCompact && styles.navSectionLabelCompact)}>
                            Project
                        </Text>
                    )}
                    {projectNav.map((item) => (
                        <SidebarNavItem
                            key={item.path}
                            item={item}
                            active={isActive(item.path, item.exact)}
                            expanded={sidebarIsExpanded}
                        />
                    ))}
                </div>
            )}

            <div className={mergeClasses(styles.sidebarFooter, isCompact && styles.sidebarFooterCompact)}>
                {bottomNav.map((item) => (
                    <SidebarNavItem
                        key={item.path}
                        item={item}
                        active={isActive(item.path, item.exact)}
                        expanded={sidebarIsExpanded}
                    />
                ))}
            </div>
        </nav>
    )

    const content = (
        <div className={styles.content}>
            <TopBar
                breadcrumbs={breadcrumbs}
                chatOpen={chatOpen}
                onToggleChat={() => setChatOpen((prev) => !prev)}
                isMobile={isMobile}
                onToggleSidebar={isMobile ? () => setMobileSidebarOpen(true) : undefined}
            />

            <div className={chatOpen && !isMobile ? styles.contentWithChat : styles.mainContent}>
                <div className={styles.mainContent}>
                    <Outlet />
                </div>

                {!isMobile && chatOpen && (
                    <div
                        className={mergeClasses(
                            styles.chatPane,
                            isResizingActive && styles.chatPaneResizing,
                        )}
                        style={{ width: `${chatWidth}px` }}
                    >
                        <div
                            className={mergeClasses(
                                styles.resizeHandle,
                                isResizingActive ? styles.resizeHandleActive : undefined,
                            )}
                            onMouseDown={handleResizeStart}
                            role="separator"
                            aria-orientation="vertical"
                            aria-label="Resize chat pane"
                        />
                        <ChatDrawer
                            projectId={projectId}
                            onClose={() => setChatOpen(false)}
                            chatWidth={chatWidth}
                            maxChatWidth={MAX_CHAT_WIDTH}
                            onRequestChatWidth={handleAutoExpandChat}
                        />
                    </div>
                )}
            </div>

            {isMobile && chatOpen && (
                <div className={styles.chatOverlayMobile}>
                    <ChatDrawer
                        projectId={projectId}
                        onClose={() => setChatOpen(false)}
                    />
                </div>
            )}
        </div>
    )

    return (
        <ChatGeneratingProvider>
            {isMobile ? (
                <div className={mergeClasses(styles.root, styles.rootMobile)}>
                    {mobileSidebarOpen && (
                        <div className={styles.mobileSidebarBackdrop} onClick={() => setMobileSidebarOpen(false)} />
                    )}
                    <div className={mergeClasses(styles.mobileSidebarDrawer, mobileSidebarOpen && styles.mobileSidebarDrawerOpen)}>
                        {sidebarNav}
                    </div>
                    {content}
                </div>
            ) : (
                <SplitView
                    containerClassName={styles.root}
                    firstPaneClassName={styles.sidebarPane}
                    first={sidebarNav}
                    second={content}
                />
            )}
        </ChatGeneratingProvider>
    )
}
