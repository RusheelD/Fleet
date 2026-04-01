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
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    page: {
        paddingTop: appTokens.space.xl,
        paddingBottom: appTokens.space.xl,
        paddingLeft: appTokens.space.pageX,
        paddingRight: appTokens.space.pageX,
        maxWidth: appTokens.width.pageNarrow,
        margin: '0 auto',
        width: '100%',
        minWidth: 0,
    },
    pageMobile: {
        paddingTop: appTokens.space.pageYMobile,
        paddingBottom: appTokens.space.pageYMobile,
        paddingLeft: appTokens.space.pageXMobile,
        paddingRight: appTokens.space.pageXMobile,
    },
    tabListSpacing: {
        marginBottom: appTokens.space.xl,
    },
    tabListSpacingMobile: {
        marginBottom: appTokens.space.pageYMobile,
        overflowX: 'auto',
        paddingBottom: appTokens.space.xxs,
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
