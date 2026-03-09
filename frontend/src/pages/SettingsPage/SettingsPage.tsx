import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
    makeStyles,
    mergeClasses,
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
import { useIsMobile } from '../../hooks'

const useStyles = makeStyles({
    page: {
        padding: '1.5rem 2rem',
        maxWidth: '900px',
        margin: '0 auto',
        width: '100%',
    },
    pageMobile: {
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
    },
    tabListSpacing: {
        marginBottom: '1.5rem',
    },
    tabListSpacingMobile: {
        marginBottom: '0.875rem',
        overflowX: 'auto',
        paddingBottom: '0.25rem',
        whiteSpace: 'nowrap',
    },
})

export function SettingsPage() {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const [searchParams] = useSearchParams()
    const initialTab = searchParams.get('tab') ?? 'profile'
    const [tab, setTab] = useState<string>(initialTab)
    const { data: settings, isLoading } = useUserSettings()

    if (isLoading || !settings) {
        return (
            <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
                <Spinner label="Loading settings..." />
            </div>
        )
    }

    return (
        <div className={mergeClasses(styles.page, isMobile && styles.pageMobile)}>
            <PageHeader
                title="Settings"
                subtitle="Manage your account, linked services, and preferences"
            />

            <TabList
                selectedValue={tab}
                onTabSelect={(_e, data) => setTab(data.value as string)}
                className={mergeClasses(styles.tabListSpacing, isMobile && styles.tabListSpacingMobile)}
                size={isMobile ? 'small' : 'medium'}
            >
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
