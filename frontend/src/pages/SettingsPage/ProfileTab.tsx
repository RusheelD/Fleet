import {
    makeStyles,
    Title3,
    Card,
    Button,
    Input,
    Divider,
    Avatar,
    Field,
} from '@fluentui/react-components'
import { SaveRegular } from '@fluentui/react-icons'

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
    profileRow: {
        display: 'flex',
        gap: '1.5rem',
        alignItems: 'flex-start',
    },
    avatarColumn: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '0.5rem',
    },
    profileForm: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
    },
    formRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '0.75rem',
    },
})

export function ProfileTab() {
    const styles = useStyles()

    return (
        <Card className={styles.section}>
            <div className={styles.sectionHeader}>
                <Title3>Profile Information</Title3>
                <Button appearance="primary" icon={<SaveRegular />}>Save Changes</Button>
            </div>
            <Divider />
            <div className={styles.profileRow}>
                <div className={styles.avatarColumn}>
                    <Avatar name="Fleet User" size={72} />
                    <Button appearance="outline" size="small">Change</Button>
                </div>
                <div className={styles.profileForm}>
                    <div className={styles.formRow}>
                        <Field label="Display Name">
                            <Input defaultValue="Fleet User" />
                        </Field>
                        <Field label="Email">
                            <Input defaultValue="user@fleet.dev" type="email" />
                        </Field>
                    </div>
                    <Field label="Bio">
                        <Input defaultValue="Building the future with AI agents" />
                    </Field>
                    <Field label="Location">
                        <Input defaultValue="San Francisco, CA" />
                    </Field>
                </div>
            </div>
        </Card>
    )
}
