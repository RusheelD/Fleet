import { useState } from 'react'
import {
    makeStyles,
    Tab,
    TabList,
} from '@fluentui/react-components'
import {
    PersonRegular,
    LinkRegular,
    PaintBrushRegular,
    ShieldKeyholeRegular,
    AlertRegular,
} from '@fluentui/react-icons'
import { PageHeader } from '../../components/shared'
import { ProfileTab } from './ProfileTab'
import { ConnectionsTab } from './ConnectionsTab'
import { AppearanceTab } from './AppearanceTab'
import { NotificationsTab } from './NotificationsTab'
import { SecurityTab } from './SecurityTab'

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
    const [tab, setTab] = useState<string>('profile')

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

            {tab === 'profile' && <ProfileTab />}
            {tab === 'connections' && <ConnectionsTab />}
            {tab === 'appearance' && <AppearanceTab />}
            {tab === 'notifications' && <NotificationsTab />}
            {tab === 'security' && <SecurityTab />}
        </div>
    )
}
