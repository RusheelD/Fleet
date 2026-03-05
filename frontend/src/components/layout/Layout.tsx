import { useState, useCallback, useRef, useEffect } from 'react'
import { useLocation, Outlet, useParams } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Text,
} from '@fluentui/react-components'
import {
    FolderRegular,
    SearchRegular,
    SettingsRegular,
    CreditCardPersonRegular,
    BoardRegular,
    BotRegular,
    HomeFilled,
} from '@fluentui/react-icons'

import { SidebarHeader, ProjectSelector, SidebarNavItem, TopBar } from './'
import { SplitView } from '../shared'
import { ChatDrawer } from '../chat'
import { useCurrentProject, usePreferences, ChatGeneratingProvider } from '../../hooks'

import type { NavItemConfig } from '../../models'

const SIDEBAR_WIDTH_EXPANDED = '260px'
const SIDEBAR_WIDTH_COLLAPSED = '48px'

const useStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground2,
    },

    /* ───── Sidebar pane (SplitView first) ───── */
    sidebarPane: {
        flexShrink: 0,
    },

    /* ───── Sidebar shell ───── */
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
    },
    sidebarExpanded: {
        width: SIDEBAR_WIDTH_EXPANDED,
    },
    sidebarCollapsed: {
        width: SIDEBAR_WIDTH_COLLAPSED,
    },

    /* ───── Nav section / group ───── */
    navSection: {
        display: 'flex',
        flexDirection: 'column',
        padding: '0.375rem',
        gap: '1px',
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

    /* ───── Footer ───── */
    sidebarFooter: {
        marginTop: 'auto',
        padding: '0.375rem',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },

    /* ───── Main content area ───── */
    content: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    mainContent: {
        flex: 1,
        overflow: 'auto',
    },
    contentWithChat: {
        display: 'flex',
        flexDirection: 'row',
        flex: 1,
        overflow: 'hidden',
    },
    chatPane: {
        flexShrink: 0,
        height: '100%',
        display: 'flex',
        flexDirection: 'row',
    },
    resizeHandle: {
        width: '4px',
        cursor: 'col-resize',
        backgroundColor: 'transparent',
        flexShrink: 0,
        transition: 'background-color 0.15s',
        ':hover': {
            backgroundColor: tokens.colorBrandBackground,
        },
    },
    resizeHandleActive: {
        backgroundColor: tokens.colorBrandBackground,
    },
})

export function Layout() {
    const styles = useStyles()
    const location = useLocation()
    const { slug } = useParams()
    const { preferences } = usePreferences()
    const [sidebarExpanded, setSidebarExpanded] = useState(!preferences?.sidebarCollapsed)
    const [chatOpen, setChatOpen] = useState(false)

    const { projectId, projectTitle } = useCurrentProject()

    // Open chat pane when navigated with { state: { openChat: true } }
    useEffect(() => {
        if (location.state?.openChat && projectId) {
            setChatOpen(true)
            // Clear the state so it doesn't re-trigger on back/forward navigation
            window.history.replaceState({}, '')
        }
    }, [location.state, projectId])

    /* ── Chat pane resize state ── */
    const MIN_CHAT_WIDTH = 340
    const MAX_CHAT_WIDTH = 800
    const DEFAULT_CHAT_WIDTH = 480
    const [chatWidth, setChatWidth] = useState(DEFAULT_CHAT_WIDTH)
    const isResizing = useRef(false)

    const handleResizeStart = useCallback(() => {
        isResizing.current = true
        document.body.style.cursor = 'col-resize'
        document.body.style.userSelect = 'none'
    }, [])

    useEffect(() => {
        const handleMouseMove = (e: MouseEvent) => {
            if (!isResizing.current) return
            const newWidth = window.innerWidth - e.clientX
            setChatWidth(Math.min(MAX_CHAT_WIDTH, Math.max(MIN_CHAT_WIDTH, newWidth)))
        }
        const handleMouseUp = () => {
            if (isResizing.current) {
                isResizing.current = false
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
    }, [])

    const isActive = useCallback(
        (path: string, exact?: boolean) => {
            if (exact) return location.pathname === path
            if (path === '/projects' && location.pathname === '/projects') return true
            if (path !== '/projects' && location.pathname.startsWith(path)) return true
            return false
        },
        [location.pathname],
    )

    /* ── Navigation sections ── */
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

    /* ── Breadcrumbs ── */
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

    return (
        <SplitView
            containerClassName={styles.root}
            firstPaneClassName={styles.sidebarPane}
            first={
                <nav
                    className={mergeClasses(
                        styles.sidebar,
                        sidebarExpanded ? styles.sidebarExpanded : styles.sidebarCollapsed,
                    )}
                >
                    <SidebarHeader
                        expanded={sidebarExpanded}
                        onToggle={() => setSidebarExpanded((prev) => !prev)}
                    />

                    {/* Project selector (when viewing a project) */}
                    {slug && (
                        <ProjectSelector projectName={projectTitle ?? slug} expanded={sidebarExpanded} />
                    )}

                    {/* Global nav */}
                    <div className={styles.navSection}>
                        {sidebarExpanded && <Text className={styles.navSectionLabel}>Navigate</Text>}
                        {globalNav.map((item) => (
                            <SidebarNavItem
                                key={item.path}
                                item={item}
                                active={isActive(item.path, item.exact)}
                                expanded={sidebarExpanded}
                            />
                        ))}
                    </div>

                    {/* Project-scoped nav */}
                    {slug && (
                        <div className={styles.navSection}>
                            {sidebarExpanded && <Text className={styles.navSectionLabel}>Project</Text>}
                            {projectNav.map((item) => (
                                <SidebarNavItem
                                    key={item.path}
                                    item={item}
                                    active={isActive(item.path, item.exact)}
                                    expanded={sidebarExpanded}
                                />
                            ))}
                        </div>
                    )}

                    {/* Footer / utility nav */}
                    <div className={styles.sidebarFooter}>
                        {bottomNav.map((item) => (
                            <SidebarNavItem
                                key={item.path}
                                item={item}
                                active={isActive(item.path, item.exact)}
                                expanded={sidebarExpanded}
                            />
                        ))}
                    </div>
                </nav>
            }
            second={
                <ChatGeneratingProvider>
                    <div className={styles.content}>
                        <TopBar
                            breadcrumbs={breadcrumbs}
                            sidebarExpanded={sidebarExpanded}
                            onExpandSidebar={() => setSidebarExpanded(true)}
                            chatOpen={chatOpen}
                            onToggleChat={() => setChatOpen((prev) => !prev)}
                        />

                        <div className={chatOpen && projectId ? styles.contentWithChat : styles.mainContent}>
                            <div className={styles.mainContent}>
                                <Outlet />
                            </div>
                            {chatOpen && projectId && (
                                <div className={styles.chatPane} style={{ width: `${chatWidth}px` }}>
                                    <div
                                        className={mergeClasses(
                                            styles.resizeHandle,
                                            isResizing.current ? styles.resizeHandleActive : undefined,
                                        )}
                                        onMouseDown={handleResizeStart}
                                        role="separator"
                                        aria-orientation="vertical"
                                        aria-label="Resize chat pane"
                                    />
                                    <ChatDrawer
                                        projectId={projectId}
                                        onClose={() => setChatOpen(false)}
                                    />
                                </div>
                            )}
                        </div>
                    </div>
                </ChatGeneratingProvider>
            }
        />
    )
}
