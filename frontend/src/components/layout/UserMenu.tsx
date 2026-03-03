import {
    makeStyles,
    Text,
    Button,
    Avatar,
    Divider,
    Popover,
    PopoverTrigger,
    PopoverSurface,
    MenuList,
    MenuItem,
} from '@fluentui/react-components'
import {
    PersonRegular,
    CreditCardPersonRegular,
    SignOutRegular,
} from '@fluentui/react-icons'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks'

const useStyles = makeStyles({
    userMenuHeader: {
        padding: '0.75rem',
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
})

export function UserMenu() {
    const styles = useStyles()
    const navigate = useNavigate()
    const { logout, user } = useAuth()
    const displayName = user?.displayName || 'User'
    const email = user?.email || ''

    const handleSignOut = () => {
        logout() // MSAL handles the redirect
    }

    return (
        <Popover>
            <PopoverTrigger disableButtonEnhancement>
                <Button appearance="subtle" size="small">
                    <Avatar name={displayName} size={24} />
                </Button>
            </PopoverTrigger>
            <PopoverSurface>
                <div className={styles.userMenuHeader}>
                    <Avatar name={displayName} size={40} />
                    <div>
                        <Text weight="semibold" block>{displayName}</Text>
                        {email && <Text size={200}>{email}</Text>}
                    </div>
                </div>
                <Divider />
                <MenuList>
                    <MenuItem icon={<PersonRegular />} onClick={() => navigate('/settings')}>
                        Profile & Settings
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
