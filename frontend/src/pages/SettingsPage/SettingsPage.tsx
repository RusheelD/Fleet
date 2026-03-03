import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
    makeStyles,
    Tab,
    TabList,
    Spinner,
} from '@fluentui/react-components'
import {
    PersonRegular,
    LinkRegular,
    PaintBrushRegular,
    ShieldKeyholeRegular,
    AlertRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { ProfileTab, ConnectionsTab, AppearanceTab, NotificationsTab, SecurityTab } from './'
import { useUserSettings } from '../../proxies'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '900px',
        margin: '0 auto',
        width: '100%',
    },
    tabListSpacing: {
        marginBottom: '1.5rem',
    },
})

export function SettingsPage() {
    const styles = useStyles()
    const [searchParams] = useSearchParams()
    const initialTab = searchParams.get('tab') ?? 'profile'
    const [tab, setTab] = useState<string>(initialTab)
    const { data: settings, isLoading } = useUserSettings()

    if (isLoading || !settings) {
        return (
            <div className={styles.page}>
                <Spinner label="Loading settings..." />
            </div>
        )
    }

    return (
        <div className={styles.page}>
            <PageHeader
                title="Settings"
                subtitle="Manage your account, linked services, and preferences"
            />

            <TabList selectedValue={tab} onTabSelect={(_e, data) => setTab(data.value as string)} className={styles.tabListSpacing}>
                <Tab value="profile" icon={<PersonRegular />}>Profile</Tab>
                <Tab value="connections" icon={<LinkRegular />}>Connections</Tab>
                <Tab value="appearance" icon={<PaintBrushRegular />}>Appearance</Tab>
                <Tab value="notifications" icon={<AlertRegular />}>Notifications</Tab>
                <Tab value="security" icon={<ShieldKeyholeRegular />}>Security</Tab>
            </TabList>

            {tab === 'profile' && <ProfileTab profile={settings.profile} />}
            {tab === 'connections' && <ConnectionsTab connections={settings.connections} />}
            {tab === 'appearance' && <AppearanceTab />}
            {tab === 'notifications' && <NotificationsTab />}
            {tab === 'security' && <SecurityTab />}
        </div>
    )
}
