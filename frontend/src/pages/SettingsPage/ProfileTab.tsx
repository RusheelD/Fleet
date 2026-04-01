import { useState } from 'react'
import {
    makeStyles,
    mergeClasses,
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
import { useAuth, useIsMobile } from '../../hooks'
import type { UserProfile } from '../../models'
import { APP_MOBILE_MEDIA_QUERY, appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    section: {
        padding: `calc(${appTokens.space.lg} + ${appTokens.space.xxs})`,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.lg,
        [APP_MOBILE_MEDIA_QUERY]: {
            paddingTop: appTokens.space.pageYMobile,
            paddingBottom: appTokens.space.pageYMobile,
            paddingLeft: appTokens.space.pageXMobile,
            paddingRight: appTokens.space.pageXMobile,
        },
    },
    sectionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    profileRow: {
        display: 'flex',
        gap: appTokens.space.xl,
        alignItems: 'stretch',
        flexWrap: 'wrap',
        [APP_MOBILE_MEDIA_QUERY]: {
            gap: appTokens.space.pageYMobile,
            flexDirection: 'column',
        },
    },
    avatarColumn: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: appTokens.space.sm,
        [APP_MOBILE_MEDIA_QUERY]: {
            alignItems: 'flex-start',
        },
    },
    profileForm: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.md,
        minWidth: 0,
        width: '100%',
    },
    formRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.md,
        minWidth: 0,
        [APP_MOBILE_MEDIA_QUERY]: {
            gridTemplateColumns: '1fr',
        },
    },
    fieldControl: {
        width: '100%',
        minWidth: 0,
    },
    saveButtonMobile: {
        width: '100%',
    },
})

interface ProfileTabProps {
    profile: UserProfile
}

export function ProfileTab({ profile }: ProfileTabProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
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
                    className={mergeClasses(isMobile && styles.saveButtonMobile)}
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
                            <Input className={styles.fieldControl} value={displayName} onChange={(_e, data) => setDisplayName(data.value)} />
                        </Field>
                        <Field label="Email">
                            <Input className={styles.fieldControl} value={email} onChange={(_e, data) => setEmail(data.value)} type="email" />
                        </Field>
                    </div>
                    <Field label="Bio">
                        <Input className={styles.fieldControl} value={bio} onChange={(_e, data) => setBio(data.value)} />
                    </Field>
                    <Field label="Location">
                        <Input className={styles.fieldControl} value={location} onChange={(_e, data) => setLocation(data.value)} />
                    </Field>
                    <Field label="Subscription Tier">
                        <Input className={styles.fieldControl} value={(profile.role ?? 'free').toUpperCase()} readOnly />
                    </Field>
                </div>
            </div>
        </Card>
    )
}
