import { useState, useCallback, useRef, useEffect } from 'react'
import { useLocation, Outlet, useParams } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
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
import { appTokens } from '../../styles/appTokens'

import type { NavItemConfig } from '../../models'

const SIDEBAR_WIDTH_EXPANDED = appTokens.size.sidebarExpanded
const SIDEBAR_WIDTH_COLLAPSED = appTokens.size.sidebarCollapsed
const SIDEBAR_WIDTH_EXPANDED_COMPACT = appTokens.size.sidebarExpandedCompact
const SIDEBAR_WIDTH_COLLAPSED_COMPACT = appTokens.size.sidebarCollapsedCompact
const MIN_CHAT_WIDTH = Number.parseInt(appTokens.size.chatMin, 10)
const MAX_CHAT_WIDTH = Number.parseInt(appTokens.size.chatMax, 10)
const DEFAULT_CHAT_WIDTH = Number.parseInt(appTokens.size.chatDefault, 10)

const useStyles = makeStyles({
    root: {
        backgroundColor: appTokens.color.surfaceAlt,
        height: '100dvh',
        minHeight: '100dvh',
        minWidth: 0,
    },
    rootMobile: {
        position: 'relative',
        overflow: 'hidden',
        width: '100%',
    },

    sidebarPane: {
        flexShrink: 0,
        display: 'flex',
        borderRight: appTokens.border.subtle,
        backgroundColor: appTokens.color.surface,
    },

    sidebar: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        backgroundColor: appTokens.color.surface,
        transition: `width ${appTokens.motion.normal} ease`,
        overflowX: 'hidden',
        overflowY: 'auto',
        flexShrink: 0,
    },
    sidebarMobile: {
        height: '100dvh',
        boxShadow: appTokens.shadow.overlay,
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
        backgroundColor: appTokens.color.overlayBackdrop,
        zIndex: appTokens.zIndex.sidebarBackdrop,
    },
    mobileSidebarDrawer: {
        position: 'fixed',
        top: 0,
        left: 0,
        bottom: 0,
        width: appTokens.size.mobileSidebarWidth,
        zIndex: appTokens.zIndex.sidebarDrawer,
        transform: 'translateX(-100%)',
        transition: `transform ${appTokens.motion.normal} ease`,
        pointerEvents: 'none',
    },
    mobileSidebarDrawerOpen: {
        transform: 'translateX(0)',
        pointerEvents: 'auto',
    },
    mobileSidebarCloseRow: {
        display: 'flex',
        justifyContent: 'flex-end',
        paddingTop: `max(${appTokens.space.xxs}, env(safe-area-inset-top))`,
        paddingBottom: 0,
        paddingLeft: appTokens.space.xxs,
        paddingRight: appTokens.space.xxs,
        borderBottom: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
    },

    navSection: {
        display: 'flex',
        flexDirection: 'column',
        paddingTop: appTokens.space.sm,
        paddingRight: appTokens.space.xs,
        paddingBottom: 0,
        paddingLeft: appTokens.space.xs,
        gap: '2px',
    },
    navSectionCompact: {
        paddingTop: appTokens.space.xxs,
        paddingLeft: appTokens.space.xxs,
        paddingRight: appTokens.space.xxs,
    },
    navSectionLabel: {
        paddingTop: appTokens.space.xs,
        paddingRight: '0.625rem',
        paddingBottom: appTokens.space.xxs,
        paddingLeft: '0.625rem',
        fontSize: '11px',
        fontWeight: 600,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        color: appTokens.color.textMuted,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        userSelect: 'none',
    },
    navSectionLabelCompact: {
        paddingTop: appTokens.space.xxs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        fontSize: '10px',
    },

    sidebarFooter: {
        marginTop: 'auto',
        padding: appTokens.space.xs,
        borderTop: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
    },
    sidebarFooterCompact: {
        padding: appTokens.space.xxs,
    },
    collapsedTopSlot: {
        marginTop: appTokens.space.xs,
        marginLeft: appTokens.space.xs,
        marginRight: appTokens.space.xs,
        display: 'flex',
        justifyContent: 'center',
    },
    collapsedTopSlotCompact: {
        marginTop: appTokens.space.xxs,
        marginLeft: appTokens.space.xxs,
        marginRight: appTokens.space.xxs,
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
        minHeight: 0,
        minWidth: 0,
        overflow: 'hidden',
        backgroundColor: appTokens.color.pageBackground,
    },
    mainContent: {
        flex: 1,
        overflow: 'auto',
        minWidth: 0,
        minHeight: 0,
    },
    contentWithChat: {
        display: 'flex',
        flexDirection: 'row',
        flex: 1,
        overflow: 'hidden',
        minWidth: 0,
        minHeight: 0,
    },
    chatPane: {
        flexShrink: 0,
        height: '100%',
        display: 'flex',
        flexDirection: 'row',
        backgroundColor: appTokens.color.surface,
        borderLeft: appTokens.border.subtle,
        transition: `width ${appTokens.motion.fast} ease`,
    },
    chatPaneResizing: {
        transition: 'none',
    },
    chatOverlayMobile: {
        position: 'fixed',
        inset: 0,
        zIndex: appTokens.zIndex.chatOverlay,
        backgroundColor: appTokens.color.surface,
        paddingBottom: 'env(safe-area-inset-bottom)',
    },
    resizeHandle: {
        width: '6px',
        cursor: 'col-resize',
        backgroundColor: 'transparent',
        flexShrink: 0,
        transition: 'background-color 0.15s',
        ':hover': {
            backgroundColor: appTokens.color.surfaceBrand,
        },
    },
    resizeHandleActive: {
        backgroundColor: appTokens.color.surfaceBrand,
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
