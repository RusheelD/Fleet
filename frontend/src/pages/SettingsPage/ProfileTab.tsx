import { useState } from 'react'
import {
    makeStyles,
    Title3,
    Card,
    Button,
    Input,
    Divider,
    Avatar,
    Field,
    Toast,
    ToastTitle,
    useToastController,
    useId,
    Toaster,
} from '@fluentui/react-components'
import { SaveRegular, CheckmarkRegular } from '@fluentui/react-icons'
import { useUpdateProfile } from '../../proxies'
import { useAuth } from '../../hooks'
import type { UserProfile } from '../../models'

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

interface ProfileTabProps {
    profile: UserProfile
}

export function ProfileTab({ profile }: ProfileTabProps) {
    const styles = useStyles()
    const { updateUser } = useAuth()
    const updateMutation = useUpdateProfile()
    const toasterId = useId('profile-toaster')
    const { dispatchToast } = useToastController(toasterId)
    const [displayName, setDisplayName] = useState(profile.displayName)
    const [email, setEmail] = useState(profile.email)
    const [bio, setBio] = useState(profile.bio)
    const [location, setLocation] = useState(profile.location)

    const hasChanges =
        displayName !== profile.displayName ||
        email !== profile.email ||
        bio !== profile.bio ||
        location !== profile.location

    const handleSave = () => {
        updateMutation.mutate({ displayName, email, bio, location }, {
            onSuccess: (savedProfile) => {
                // Update the auth context so the user's name/email updates globally
                updateUser(savedProfile)
                dispatchToast(
                    <Toast><ToastTitle>Profile saved successfully</ToastTitle></Toast>,
                    { intent: 'success' },
                )
            },
            onError: () => {
                dispatchToast(
                    <Toast><ToastTitle>Failed to save profile</ToastTitle></Toast>,
                    { intent: 'error' },
                )
            },
        })
    }

    return (
        <Card className={styles.section}>
            <Toaster toasterId={toasterId} />
            <div className={styles.sectionHeader}>
                <Title3>Profile Information</Title3>
                <Button
                    appearance="primary"
                    icon={hasChanges ? <SaveRegular /> : <CheckmarkRegular />}
                    onClick={handleSave}
                    disabled={updateMutation.isPending || !hasChanges}
                >
                    {updateMutation.isPending ? 'Saving...' : hasChanges ? 'Save Changes' : 'Saved'}
                </Button>
            </div>
            <Divider />
            <div className={styles.profileRow}>
                <div className={styles.avatarColumn}>
                    <Avatar name={displayName} size={72} />
                </div>
                <div className={styles.profileForm}>
                    <div className={styles.formRow}>
                        <Field label="Display Name">
                            <Input value={displayName} onChange={(_e, data) => setDisplayName(data.value)} />
                        </Field>
                        <Field label="Email">
                            <Input value={email} onChange={(_e, data) => setEmail(data.value)} type="email" />
                        </Field>
                    </div>
                    <Field label="Bio">
                        <Input value={bio} onChange={(_e, data) => setBio(data.value)} />
                    </Field>
                    <Field label="Location">
                        <Input value={location} onChange={(_e, data) => setLocation(data.value)} />
                    </Field>
                    <Field label="Subscription Tier">
                        <Input value={(profile.role ?? 'free').toUpperCase()} readOnly />
                    </Field>
                </div>
            </div>
        </Card>
    )
}
