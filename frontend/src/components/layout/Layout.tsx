import { useState, useCallback } from 'react'
import { useLocation, Outlet, useParams } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
    tokens,
    Text,
    Divider,
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

import { SidebarHeader } from './SidebarHeader'
import { ProjectSelector } from './ProjectSelector'
import { SidebarNavItem } from './SidebarNavItem'
import { TopBar } from './TopBar'

import type { NavItemConfig } from '../../models'

const SIDEBAR_WIDTH_EXPANDED = '260px'
const SIDEBAR_WIDTH_COLLAPSED = '48px'

const useStyles = makeStyles({
    root: {
        display: 'flex',
        minHeight: '100vh',
        backgroundColor: tokens.colorNeutralBackground2,
    },

    /* ───── Sidebar shell ───── */
    sidebar: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
        transition: 'width 0.2s ease',
        overflow: 'hidden',
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
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'auto',
    },
    mainContent: {
        flex: 1,
        overflow: 'auto',
    },
})

export function Layout() {
    const styles = useStyles()
    const location = useLocation()
    const { projectId } = useParams()
    const [sidebarExpanded, setSidebarExpanded] = useState(true)

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

    const projectNav: NavItemConfig[] = projectId
        ? [
            { icon: <HomeFilled />, label: 'Overview', path: `/projects/${projectId}`, exact: true },
            { icon: <BoardRegular />, label: 'Work Items', path: `/projects/${projectId}/work-items` },
            { icon: <BotRegular />, label: 'Agents', path: `/projects/${projectId}/agents` },
        ]
        : []

    const bottomNav: NavItemConfig[] = [
        { icon: <SettingsRegular />, label: 'Settings', path: '/settings' },
        { icon: <CreditCardPersonRegular />, label: 'Subscription', path: '/subscription' },
    ]

    /* ── Breadcrumbs ── */
    const getBreadcrumbs = () => {
        const parts: Array<{ label: string; path?: string }> = []
        parts.push({ label: 'Fleet', path: '/projects' })

        if (projectId) {
            parts.push({ label: `Project ${projectId}`, path: `/projects/${projectId}` })

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
        <div className={styles.root}>
            {/* ═══════ Sidebar ═══════ */}
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
                {projectId && (
                    <ProjectSelector projectName="Fleet Platform" expanded={sidebarExpanded} />
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
                {projectId && (
                    <>
                        <Divider />
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
                    </>
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

            {/* ═══════ Main content area ═══════ */}
            <div className={styles.content}>
                <TopBar
                    breadcrumbs={breadcrumbs}
                    sidebarExpanded={sidebarExpanded}
                    onExpandSidebar={() => setSidebarExpanded(true)}
                />

                <div className={styles.mainContent}>
                    <Outlet />
                </div>
            </div>
        </div>
    )
}
