import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
    Button,
    Card,
    Caption1,
    makeStyles,
    mergeClasses,
    Text,
    Tab,
    TabList,
    Spinner,
} from '@fluentui/react-components'
import {
    PersonRegular,
    LinkRegular,
    BookmarkRegular,
    BotRegular,
    PaintBrushRegular,
    ShieldKeyholeRegular,
    AlertRegular,
} from '@fluentui/react-icons'
import { PageShell } from '../../components/shared'
import { ProfileTab, AppearanceTab, NotificationsTab, SecurityTab } from './'
import { useUserSettings } from '../../proxies'
import { useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const SETTINGS_TABS = ['profile', 'appearance', 'notifications', 'security'] as const
type SettingsTabKey = typeof SETTINGS_TABS[number]

const MOVED_TABS: Record<string, { label: string; path: string }> = {
    connections: { label: 'Integrations', path: '/integrations' },
    memory: { label: 'Memory', path: '/memory' },
    playbooks: { label: 'Playbooks', path: '/playbooks' },
}

const useStyles = makeStyles({
    tabListSpacing: {
        marginTop: appTokens.space.xxs,
    },
    tabListSpacingMobile: {
        overflowX: 'auto',
        paddingBottom: appTokens.space.xxs,
        whiteSpace: 'nowrap',
    },
    workspaceCard: {
        padding: appTokens.space.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.lg,
    },
    workspaceCardMobile: {
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.md,
    },
    workspaceHeader: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
    },
    shortcutGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: appTokens.space.md,
    },
    shortcutCard: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        backgroundColor: appTokens.color.pageBackground,
    },
    shortcutCardHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
    },
    shortcutIcon: {
        color: appTokens.color.brand,
        fontSize: appTokens.fontSize.iconMd,
    },
    relocationCard: {
        padding: appTokens.space.md,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
        flexWrap: 'wrap',
        backgroundColor: appTokens.color.surfaceBrand,
    },
    relocationCardMobile: {
        alignItems: 'flex-start',
    },
    relocationCopy: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
    },
})

function isSettingsTab(value: string): value is SettingsTabKey {
    return (SETTINGS_TABS as readonly string[]).includes(value)
}

export function SettingsPage() {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const navigate = useNavigate()
    const [searchParams] = useSearchParams()
    const requestedTab = searchParams.get('tab') ?? 'profile'
    const initialTab: SettingsTabKey = isSettingsTab(requestedTab) ? requestedTab : 'profile'
    const [tab, setTab] = useState<SettingsTabKey>(initialTab)
    const { data: settings, isLoading } = useUserSettings()
    const movedTabDestination = MOVED_TABS[requestedTab]

    useEffect(() => {
        setTab(isSettingsTab(requestedTab) ? requestedTab : 'profile')
    }, [requestedTab])

    const workspaceTools = [
        {
            key: 'notifications',
            title: 'Notifications',
            description: 'Use the inbox view for execution updates and project events instead of burying them in preferences.',
            path: '/notifications',
            icon: <AlertRegular className={styles.shortcutIcon} />,
        },
        {
            key: 'integrations',
            title: 'Integrations',
            description: 'Manage linked accounts, GitHub access, and MCP servers from one dedicated page.',
            path: '/integrations',
            icon: <LinkRegular className={styles.shortcutIcon} />,
        },
        {
            key: 'memory',
            title: 'Memory',
            description: 'Keep durable context and references somewhere easy to reach while you are actively working.',
            path: '/memory',
            icon: <BookmarkRegular className={styles.shortcutIcon} />,
        },
        {
            key: 'playbooks',
            title: 'Playbooks',
            description: 'Promote reusable workflows into a first-class workspace tool instead of a hidden settings tab.',
            path: '/playbooks',
            icon: <BotRegular className={styles.shortcutIcon} />,
        },
    ]

    if (isLoading || !settings) {
        return (
            <PageShell
                title="Settings"
                subtitle="Manage your account, app preferences, and how Fleet shows up for you."
                maxWidth="medium"
            >
                <Spinner label="Loading settings..." />
            </PageShell>
        )
    }

    return (
        <PageShell
                title="Settings"
                subtitle="Manage your account, app preferences, and how Fleet shows up for you."
                maxWidth="medium"
            >
            {movedTabDestination && (
                <Card className={mergeClasses(styles.relocationCard, isMobile && styles.relocationCardMobile)}>
                    <div className={styles.relocationCopy}>
                        <Text weight="semibold">{movedTabDestination.label} moved out of Settings</Text>
                        <Caption1>
                            It now lives in the main navigation so it stays visible during day-to-day work instead of getting buried here.
                        </Caption1>
                    </div>
                    <Button appearance="primary" onClick={() => navigate(movedTabDestination.path)}>
                        Open {movedTabDestination.label}
                    </Button>
                </Card>
            )}

            <Card className={mergeClasses(styles.workspaceCard, isMobile && styles.workspaceCardMobile)}>
                <div className={styles.workspaceHeader}>
                    <Text weight="semibold">Workspace tools</Text>
                    <Caption1>
                        The pieces you use during actual execution now live alongside the rest of the workspace, not behind settings.
                    </Caption1>
                </div>
                <div className={styles.shortcutGrid}>
                    {workspaceTools.map((tool) => (
                        <Card key={tool.key} className={styles.shortcutCard}>
                            <div className={styles.shortcutCardHeader}>
                                {tool.icon}
                                <Text weight="semibold">{tool.title}</Text>
                            </div>
                            <Caption1>{tool.description}</Caption1>
                            <Button appearance="secondary" onClick={() => navigate(tool.path)}>
                                Open {tool.title}
                            </Button>
                        </Card>
                    ))}
                </div>
            </Card>

            <TabList
                selectedValue={tab}
                onTabSelect={(_e, data) => setTab(data.value as SettingsTabKey)}
                className={mergeClasses(styles.tabListSpacing, isMobile && styles.tabListSpacingMobile)}
                size={isMobile ? 'small' : 'medium'}
            >
                <Tab value="profile" icon={<PersonRegular />}>Profile</Tab>
                <Tab value="appearance" icon={<PaintBrushRegular />}>Appearance</Tab>
                <Tab value="notifications" icon={<AlertRegular />}>Notification Preferences</Tab>
                <Tab value="security" icon={<ShieldKeyholeRegular />}>Security</Tab>
            </TabList>

            {tab === 'profile' && <ProfileTab profile={settings.profile} />}
            {tab === 'appearance' && <AppearanceTab />}
            {tab === 'notifications' && <NotificationsTab />}
            {tab === 'security' && <SecurityTab />}
        </PageShell>
    )
}
