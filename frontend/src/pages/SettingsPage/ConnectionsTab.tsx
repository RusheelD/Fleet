import { useCallback, useEffect, useMemo, useState } from 'react'
import {
    makeStyles,
    Title3,
    Card,
    Divider,
    Caption1,
    Text,
    Button,
    tokens,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogContent,
    DialogActions,
    DialogTrigger,
    mergeClasses,
} from '@fluentui/react-components'
import { PlugConnectedRegular, StarRegular } from '@fluentui/react-icons'
import { AccountRow } from './'
import { getApiErrorMessage, getGitHubOAuthState, getGitHubOAuthClientId, useSetPrimaryGitHubAccount, useUnlinkGitHub } from '../../proxies'
import { useIsMobile } from '../../hooks'
import type { LinkedAccount } from '../../models'

const PLACEHOLDER_GITHUB_CLIENT_ID = 'YOUR_GITHUB_OAUTH_CLIENT_ID'

const useStyles = makeStyles({
    section: {
        padding: '1.25rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
    sectionMobile: {
        paddingTop: '0.875rem',
        paddingBottom: '0.875rem',
        paddingLeft: '0.75rem',
        paddingRight: '0.75rem',
    },
    sectionHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '0.5rem',
        flexWrap: 'wrap',
    },
    infoCard: {
        padding: '1rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    infoCardMobile: {
        alignItems: 'flex-start',
        paddingTop: '0.75rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
    },
    infoIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    connectError: {
        color: tokens.colorPaletteRedForeground1,
    },
    actionButtonMobile: {
        width: '100%',
    },
})

function normalizeGitHubClientId(value?: string | null): string | undefined {
    const normalized = (value ?? '').trim()
    if (!normalized || normalized === PLACEHOLDER_GITHUB_CLIENT_ID) {
        return undefined
    }

    return normalized
}

const buildTimeClientId = normalizeGitHubClientId(import.meta.env.VITE_GITHUB_CLIENT_ID as string | undefined)

function getNormalizedRedirectUri(): string {
    const origin = window.location.origin
    if (origin.includes('localhost')) {
        return 'http://localhost:5250'
    }
    return origin
}

function buildGitHubAuthUrl(clientId: string, state: string): string {
    const baseUri = getNormalizedRedirectUri()
    const redirectUri = `${baseUri}/auth/github/callback`
    const params = new URLSearchParams({
        client_id: clientId,
        redirect_uri: redirectUri,
        scope: 'read:user user:email repo',
        state,
    })
    return `https://github.com/login/oauth/authorize?${params.toString()}`
}

interface ConnectionsTabProps {
    connections: LinkedAccount[]
}

export function ConnectionsTab({ connections }: ConnectionsTabProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const unlinkGitHub = useUnlinkGitHub()
    const setPrimaryGitHub = useSetPrimaryGitHubAccount()
    const [disconnectTarget, setDisconnectTarget] = useState<LinkedAccount | null>(null)
    const [isConnecting, setIsConnecting] = useState(false)
    const [isResolvingClientId, setIsResolvingClientId] = useState(!buildTimeClientId)
    const [connectError, setConnectError] = useState<string | null>(null)
    const [resolvedGitHubClientId, setResolvedGitHubClientId] = useState<string | undefined>(buildTimeClientId)

    const gitHubConnections = useMemo(
        () => connections
            .filter(c => c.provider === 'GitHub' && c.connectedAs)
            .sort((a, b) => {
                if (!!a.isPrimary !== !!b.isPrimary) {
                    return a.isPrimary ? -1 : 1
                }
                return (b.connectedAt ?? '').localeCompare(a.connectedAt ?? '')
            }),
        [connections],
    )
    const hasGitHubConnections = gitHubConnections.length > 0

    useEffect(() => {
        let active = true

        const loadClientId = async () => {
            try {
                setIsResolvingClientId(true)
                const { clientId } = await getGitHubOAuthClientId()
                const normalized = normalizeGitHubClientId(clientId)
                if (!active) {
                    return
                }

                if (normalized) {
                    setResolvedGitHubClientId(normalized)
                    setConnectError(null)
                } else if (!resolvedGitHubClientId) {
                    setConnectError('GitHub OAuth client ID is missing. Configure GitHub:ClientId on the server.')
                }
            } catch {
                if (!active || resolvedGitHubClientId) {
                    return
                }
                setConnectError('GitHub OAuth client ID is not configured on the server.')
            } finally {
                if (active) {
                    setIsResolvingClientId(false)
                }
            }
        }

        void loadClientId()

        return () => {
            active = false
        }
        // Run once on mount.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])

    const handleConnectGitHub = useCallback(async () => {
        try {
            setConnectError(null)
            setIsConnecting(true)

            let clientId = resolvedGitHubClientId
            if (!clientId) {
                const response = await getGitHubOAuthClientId()
                clientId = normalizeGitHubClientId(response.clientId)
                if (clientId) {
                    setResolvedGitHubClientId(clientId)
                }
            }

            if (!clientId) {
                throw new Error('GitHub OAuth client ID is not configured on the server.')
            }

            const { state } = await getGitHubOAuthState()
            window.location.href = buildGitHubAuthUrl(clientId, state)
        } catch (error) {
            const message = getApiErrorMessage(error, 'Unable to start GitHub OAuth flow.')
            setConnectError(message)
        } finally {
            setIsConnecting(false)
        }
    }, [resolvedGitHubClientId])

    const handleDisconnectGitHub = useCallback(() => {
        if (!disconnectTarget) {
            return
        }
        unlinkGitHub.mutate(disconnectTarget.id, {
            onSuccess: () => setDisconnectTarget(null),
        })
    }, [disconnectTarget, unlinkGitHub])

    const handleSetPrimaryGitHub = useCallback((accountId: number) => {
        setPrimaryGitHub.mutate(accountId)
    }, [setPrimaryGitHub])

    return (
        <Card className={mergeClasses(styles.section, isMobile && styles.sectionMobile)}>
            <div className={styles.sectionHeader}>
                <Title3>Linked Accounts</Title3>
            </div>
            <Divider />

            <div className={mergeClasses(styles.infoCard, isMobile && styles.infoCardMobile)}>
                <PlugConnectedRegular className={styles.infoIcon} />
                <div>
                    <Text weight="semibold" block>External Connections</Text>
                    <Caption1>
                        Link your GitHub account to enable repository access, pull requests,
                        and agent-driven development workflows.
                    </Caption1>
                </div>
            </div>

            <AccountRow
                name="GitHub"
                connectedAs={hasGitHubConnections ? `${gitHubConnections.length} linked account${gitHubConnections.length === 1 ? '' : 's'}` : undefined}
                actions={
                    <Button
                        appearance="primary"
                        size="small"
                        onClick={handleConnectGitHub}
                        disabled={isConnecting || isResolvingClientId}
                        className={mergeClasses(isMobile && styles.actionButtonMobile)}
                    >
                        {isResolvingClientId ? 'Loading...' : (isConnecting ? 'Connecting...' : 'Connect GitHub')}
                    </Button>
                }
            />

            {gitHubConnections.map((account) => (
                <AccountRow
                    key={`github-${account.id}`}
                    name="GitHub Account"
                    connectedAs={account.connectedAs}
                    actions={
                        <>
                            <Button
                                appearance={account.isPrimary ? 'primary' : 'subtle'}
                                size="small"
                                icon={!account.isPrimary ? <StarRegular /> : undefined}
                                onClick={() => handleSetPrimaryGitHub(account.id)}
                                disabled={!!account.isPrimary || setPrimaryGitHub.isPending}
                            >
                                {account.isPrimary ? 'Primary' : 'Set Primary'}
                            </Button>
                            <Button
                                appearance="subtle"
                                size="small"
                                onClick={() => setDisconnectTarget(account)}
                            >
                                Disconnect
                            </Button>
                        </>
                    }
                />
            ))}

            <Dialog open={!!disconnectTarget} onOpenChange={(_e, data) => { if (!data.open) setDisconnectTarget(null) }}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Disconnect GitHub</DialogTitle>
                        <DialogContent>
                            Are you sure you want to disconnect this GitHub account
                            {disconnectTarget?.connectedAs ? <> (<b>{disconnectTarget.connectedAs}</b>)</> : null}? Fleet will no longer
                            be able to access repositories through it.
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">Cancel</Button>
                            </DialogTrigger>
                            <Button
                                appearance="primary"
                                onClick={handleDisconnectGitHub}
                                disabled={unlinkGitHub.isPending}
                            >
                                {unlinkGitHub.isPending ? 'Disconnecting...' : 'Disconnect'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {connectError && (
                <Caption1 className={styles.connectError}>{connectError}</Caption1>
            )}

            {connections
                .filter(c => c.provider !== 'GitHub')
                .map((account) => (
                    <AccountRow
                        key={`${account.provider}-${account.id}`}
                        name={account.provider}
                        connectedAs={account.connectedAs}
                    />
                ))}
        </Card>
    )
}
