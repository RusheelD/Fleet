import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
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
    relocationCard: {
        padding: appTokens.space.md,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxs,
        backgroundColor: appTokens.color.surfaceAlt,
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
    const [searchParams] = useSearchParams()
    const requestedTab = searchParams.get('tab') ?? 'profile'
    const initialTab: SettingsTabKey = isSettingsTab(requestedTab) ? requestedTab : 'profile'
    const [tab, setTab] = useState<SettingsTabKey>(initialTab)
    const { data: settings, isLoading } = useUserSettings()
    const movedTabDestination = MOVED_TABS[requestedTab]

    useEffect(() => {
        setTab(isSettingsTab(requestedTab) ? requestedTab : 'profile')
    }, [requestedTab])

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
            {movedTabDestination ? (
                <Card className={styles.relocationCard}>
                    <div className={styles.relocationCopy}>
                        <Text weight="semibold">{movedTabDestination.label}</Text>
                        <Caption1>
                            Open it from the left navigation.
                        </Caption1>
                    </div>
                </Card>
            ) : null}

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
