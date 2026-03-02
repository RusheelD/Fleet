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

    return (
        <Popover>
            <PopoverTrigger disableButtonEnhancement>
                <Button appearance="subtle" size="small">
                    <Avatar name="Fleet User" size={24} />
                </Button>
            </PopoverTrigger>
            <PopoverSurface>
                <div className={styles.userMenuHeader}>
                    <Avatar name="Fleet User" size={40} />
                    <div>
                        <Text weight="semibold" block>Fleet User</Text>
                        <Text size={200}>user@fleet.dev</Text>
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
                    <MenuItem icon={<SignOutRegular />}>
                        Sign Out
                    </MenuItem>
                </MenuList>
            </PopoverSurface>
        </Popover>
    )
}
