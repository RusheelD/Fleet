import {
    makeStyles,
    Text,
    Button,
    Avatar,
    Badge,
    Caption1,
    Divider,
    Popover,
    PopoverTrigger,
    PopoverSurface,
    MenuList,
    MenuItem,
} from '@fluentui/react-components'
import {
    PersonRegular,
    AlertRegular,
    CreditCardPersonRegular,
    SignOutRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuthHook'

const useStyles = makeStyles({
    userMenuHeader: {
        padding: '0.75rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    userMenuSurface: {
        minWidth: '260px',
        padding: 0,
        borderRadius: '16px',
        overflow: 'hidden',
    },
    userMenuTrigger: {
        minWidth: 'unset',
    },
    userMenuIdentity: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem',
        minWidth: 0,
    },
    userMenuEmail: {
        color: 'var(--app-text-secondary)',
        wordBreak: 'break-word',
    },
    userMenuBadgeRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.375rem',
        flexWrap: 'wrap',
    },
    tierBadge: {
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
    },
})

export function UserMenu() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { logout, user } = useAuth()
    const displayName = user?.displayName || 'User'
    const email = user?.email || ''
    const tier = (user?.role ?? 'free').toString().toUpperCase()

    const handleSignOut = () => {
        logout() // MSAL handles the redirect
    }

    return (
        <Popover>
            <PopoverTrigger disableButtonEnhancement>
                <Button appearance="subtle" size="small" className={styles.userMenuTrigger}>
                    <Avatar name={displayName} size={24} />
                </Button>
            </PopoverTrigger>
            <PopoverSurface className={styles.userMenuSurface}>
                <div className={styles.userMenuHeader}>
                    <Avatar name={displayName} size={40} />
                    <div className={styles.userMenuIdentity}>
                        <Text weight="semibold" block>{displayName}</Text>
                        {email && <Caption1 className={styles.userMenuEmail}>{email}</Caption1>}
                        <div className={styles.userMenuBadgeRow}>
                            <Badge appearance="outline" color="brand" className={styles.tierBadge}>
                                {tier}
                            </Badge>
                            <Caption1>Workspace account</Caption1>
                        </div>
                    </div>
                </div>
                <Divider />
                <MenuList>
                    <MenuItem icon={<PersonRegular />} onClick={() => navigate('/settings')}>
                        Account Settings
                    </MenuItem>
                    <MenuItem icon={<AlertRegular />} onClick={() => navigate('/notifications')}>
                        Notifications
                    </MenuItem>
                    <MenuItem icon={<CreditCardPersonRegular />} onClick={() => navigate('/subscription')}>
                        Subscription
                    </MenuItem>
                    <MenuItem icon={<SignOutRegular />} onClick={handleSignOut}>
                        Sign Out
                    </MenuItem>
                </MenuList>
            </PopoverSurface>
        </Popover>
    )
}
