import { useState } from 'react'
import {
    makeStyles,
    mergeClasses,
    Title3,
    Caption1,
    Text,
    Card,
    Divider,
    Button,
    Badge,
    Spinner,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    DialogTrigger,
} from '@fluentui/react-components'
import { ShieldKeyholeRegular, LockClosedRegular, PersonRegular, LinkRegular, DismissCircleRegular } from '@fluentui/react-icons'
import { useAuth, useIsMobile } from '../../hooks'
import { getApiErrorMessage, useDeleteLoginIdentity, useLoginIdentities } from '../../proxies'
import type { AuthLoginProvider } from '../../auth'
import type { LoginIdentity } from '../../models'
import { APP_MOBILE_MEDIA_QUERY, appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../../components/shared/InfoBadge'

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
            gap: appTokens.space.md,
        },
    },
    settingRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        gap: appTokens.space.md,
    },
    settingRowMobile: {
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: appTokens.space.sm,
    },
    settingInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
    },
    infoCard: {
        padding: appTokens.space.lg,
        backgroundColor: appTokens.color.pageBackground,
        borderRadius: appTokens.radius.md,
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.md,
    },
    infoCardMobile: {
        alignItems: 'flex-start',
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    infoIcon: {
        fontSize: appTokens.fontSize.iconLg,
        color: appTokens.color.brand,
        flexShrink: 0,
    },
    managedBadge: {
        flexShrink: 0,
    },
    methodList: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
    },
    methodRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: appTokens.space.md,
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.pageBackground,
    },
    methodRowMobile: {
        alignItems: 'stretch',
        flexDirection: 'column',
    },
    methodMeta: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.xxxs,
        minWidth: 0,
    },
    methodActions: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-end',
        gap: appTokens.space.sm,
        flexWrap: 'wrap',
    },
    methodActionsMobile: {
        justifyContent: 'stretch',
        '> .fui-Button': {
            flex: 1,
        },
    },
    securityError: {
        color: appTokens.color.danger,
    },
})

const LOGIN_PROVIDERS: Array<{ key: AuthLoginProvider; label: string; provider: string }> = [
    { key: 'email', label: 'Email', provider: 'Email' },
    { key: 'microsoft', label: 'Microsoft', provider: 'Microsoft' },
    { key: 'google', label: 'Google', provider: 'Google' },
]

export function SecurityTab() {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const { linkLoginProvider, logout } = useAuth()
    const { data: loginIdentities, isLoading: isLoadingIdentities } = useLoginIdentities()
    const deleteLoginIdentity = useDeleteLoginIdentity()
    const [linkError, setLinkError] = useState<string | null>(null)
    const [unlinkTarget, setUnlinkTarget] = useState<LoginIdentity | null>(null)
    const identities = loginIdentities ?? []
    const linkedCount = identities.length

    const handleLinkProvider = (provider: AuthLoginProvider) => {
        setLinkError(null)
        linkLoginProvider(provider).catch((error: unknown) => {
            setLinkError(getApiErrorMessage(error, 'Unable to start sign-in method linking.'))
        })
    }

    const handleUnlink = () => {
        if (!unlinkTarget) {
            return
        }

        const shouldLogout = Boolean(unlinkTarget.isCurrent)
        deleteLoginIdentity.mutate(unlinkTarget.id, {
            onSuccess: () => {
                setUnlinkTarget(null)
                if (shouldLogout) {
                    logout()
                }
            },
            onError: (error: unknown) => {
                setLinkError(getApiErrorMessage(error, 'Unable to delink sign-in method.'))
            },
        })
    }

    return (
        <Card className={styles.section}>
            <Title3>Security</Title3>
            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <ShieldKeyholeRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Secured by Microsoft Entra ID</Text>
                    <Caption1>Your account is protected by your organization&apos;s identity provider. Multi-factor authentication and session management are handled by Entra ID.</Caption1>
                </div>
            </div>

            <div className={mergeClasses(styles.settingRow, isMobile && styles.settingRowMobile)}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Two-Factor Authentication</Text>
                    <Caption1>Managed by your Entra ID administrator</Caption1>
                </div>
                <InfoBadge appearance="tint" className={styles.managedBadge}>Managed by Entra ID</InfoBadge>
            </div>

            <div className={mergeClasses(styles.settingRow, isMobile && styles.settingRowMobile)}>
                <div className={styles.settingInfo}>
                    <Text weight="semibold">Active Sessions</Text>
                    <Caption1>View and manage sessions via your Microsoft account</Caption1>
                </div>
                <InfoBadge appearance="tint" className={styles.managedBadge}>Managed by Entra ID</InfoBadge>
            </div>

            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <PersonRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Sign-in Methods</Text>
                    <Caption1>Link providers here to sign in to this same Fleet account with email, Google, or Microsoft.</Caption1>
                </div>
            </div>

            <div className={styles.methodList}>
                {isLoadingIdentities ? (
                    <Spinner label="Loading sign-in methods..." />
                ) : LOGIN_PROVIDERS.map((provider) => {
                    const identity = identities.find(item => item.provider === provider.provider)
                    const isLinked = Boolean(identity)
                    const canUnlink = isLinked && linkedCount > 1

                    return (
                        <div key={provider.key} className={mergeClasses(styles.methodRow, isMobile && styles.methodRowMobile)}>
                            <div className={styles.methodMeta}>
                                <Text weight="semibold">{provider.label}</Text>
                                <Caption1>
                                    {identity?.email || identity?.displayName || (isLinked ? 'Linked' : 'Not linked')}
                                </Caption1>
                            </div>
                            <div className={mergeClasses(styles.methodActions, isMobile && styles.methodActionsMobile)}>
                                {identity?.isCurrent && <Badge appearance="filled" color="brand">Current</Badge>}
                                {isLinked ? (
                                    <Button
                                        appearance="subtle"
                                        size="small"
                                        icon={<DismissCircleRegular />}
                                        disabled={!canUnlink || deleteLoginIdentity.isPending}
                                        onClick={() => setUnlinkTarget(identity ?? null)}
                                    >
                                        Delink
                                    </Button>
                                ) : (
                                    <Button
                                        appearance="outline"
                                        size="small"
                                        icon={<LinkRegular />}
                                        onClick={() => handleLinkProvider(provider.key)}
                                    >
                                        Link
                                    </Button>
                                )}
                            </div>
                        </div>
                    )
                })}
            </div>

            {linkError && <Caption1 className={styles.securityError}>{linkError}</Caption1>}

            <Dialog open={!!unlinkTarget} onOpenChange={(_e, data) => { if (!data.open) setUnlinkTarget(null) }}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Delink {unlinkTarget?.provider}</DialogTitle>
                        <DialogContent>
                            Delink this sign-in method from your Fleet account?
                            {unlinkTarget?.isCurrent ? ' You will be signed out after it is removed.' : null}
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">Cancel</Button>
                            </DialogTrigger>
                            <Button
                                appearance="primary"
                                onClick={handleUnlink}
                                disabled={deleteLoginIdentity.isPending}
                            >
                                {deleteLoginIdentity.isPending ? 'Delinking...' : 'Delink'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <LockClosedRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Data &amp; Privacy</Text>
                    <Caption1>Contact your workspace administrator for account deletion or data export requests.</Caption1>
                </div>
            </div>

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <PersonRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>Social Sign-In</Text>
                    <Caption1>Microsoft and Google sign-in are available when those identity providers are enabled in your Entra tenant.</Caption1>
                </div>
            </div>
        </Card>
    )
}
