import { useCallback, useState } from 'react'
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
} from '@fluentui/react-components'
import { PlugConnectedRegular } from '@fluentui/react-icons'
import { AccountRow } from './'
import { getGitHubOAuthState, useUnlinkGitHub } from '../../proxies'
import type { LinkedAccount } from '../../models'

const GITHUB_CLIENT_ID = import.meta.env.VITE_GITHUB_CLIENT_ID as string | undefined

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
    infoCard: {
        padding: '1rem',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusMedium,
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    infoIcon: {
        fontSize: '24px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
})

function getNormalizedRedirectUri(): string {
    const origin = window.location.origin
    if (origin.includes('localhost')) {
        return 'http://localhost:5250'
    }
    return origin
}

function buildGitHubAuthUrl(state: string): string {
    const baseUri = getNormalizedRedirectUri()
    const redirectUri = `${baseUri}/auth/github/callback`
    const params = new URLSearchParams({
        client_id: GITHUB_CLIENT_ID ?? '',
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
    const unlinkGitHub = useUnlinkGitHub()
    const [disconnectOpen, setDisconnectOpen] = useState(false)
    const [isConnecting, setIsConnecting] = useState(false)

    const gitHubConnection = connections.find(c => c.provider === 'GitHub')
    const isGitHubConnected = !!gitHubConnection?.connectedAs

    const handleConnectGitHub = useCallback(async () => {
        try {
            setIsConnecting(true)
            const { state } = await getGitHubOAuthState()
            window.location.href = buildGitHubAuthUrl(state)
        } finally {
            setIsConnecting(false)
        }
    }, [])

    const handleDisconnectGitHub = useCallback(() => {
        unlinkGitHub.mutate(undefined, {
            onSuccess: () => setDisconnectOpen(false),
        })
    }, [unlinkGitHub])

    return (
        <Card className={styles.section}>
            <div className={styles.sectionHeader}>
                <Title3>Linked Accounts</Title3>
            </div>
            <Divider />

            <div className={styles.infoCard}>
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
                connectedAs={gitHubConnection?.connectedAs}
                actions={
                    isGitHubConnected ? (
                        <Dialog open={disconnectOpen} onOpenChange={(_e, data) => setDisconnectOpen(data.open)}>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="subtle" size="small">Disconnect</Button>
                            </DialogTrigger>
                            <DialogSurface>
                                <DialogBody>
                                    <DialogTitle>Disconnect GitHub</DialogTitle>
                                    <DialogContent>
                                        Are you sure you want to disconnect your GitHub account
                                        (<b>{gitHubConnection?.connectedAs}</b>)? Fleet will no longer
                                        be able to access your repositories.
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
                    ) : (
                        <Button
                            appearance="primary"
                            size="small"
                            onClick={handleConnectGitHub}
                            disabled={!GITHUB_CLIENT_ID || isConnecting}
                        >
                            {isConnecting ? 'Connecting...' : 'Connect GitHub'}
                        </Button>
                    )
                }
            />

            {/* Show other connections as read-only */}
            {connections
                .filter(c => c.provider !== 'GitHub')
                .map((account) => (
                    <AccountRow
                        key={account.provider}
                        name={account.provider}
                        connectedAs={account.connectedAs}
                    />
                ))}
        </Card>
    )
}
