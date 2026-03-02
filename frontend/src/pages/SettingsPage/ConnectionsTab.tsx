import {
    makeStyles,
    Title3,
    Card,
    Button,
    Divider,
} from '@fluentui/react-components'
import { AddRegular } from '@fluentui/react-icons'
import { AccountRow } from './AccountRow'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    sectionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
})

export function ConnectionsTab() {
    const styles = useStyles()

    return (
        <Card className={styles.section}>
            <div className={styles.sectionHeader}>
                <Title3>Linked Accounts</Title3>
                <Button appearance="outline" size="small" icon={<AddRegular />}>Link Account</Button>
            </div>
            <Divider />
            <AccountRow name="GitHub" connectedAs="RusheelD" />
            <AccountRow name="Google" />
            <AccountRow name="Microsoft" />
        </Card>
    )
}
