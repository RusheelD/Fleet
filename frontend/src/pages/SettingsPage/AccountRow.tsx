import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Badge,
    Button,
    Avatar,
} from '@fluentui/react-components'
import { OpenRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'

const useStyles = makeStyles({
    accountRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
    },
    accountInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    captionBlock: {
        display: 'block' as const,
    },
    connectionActions: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'center',
    },
})

interface AccountRowProps {
    name: string
    /** Username displayed when connected (e.g., "RusheelD") */
    connectedAs?: string
    /** Override the default action area */
    actions?: ReactNode
}

export function AccountRow({ name, connectedAs, actions }: AccountRowProps) {
    const styles = useStyles()
    const isConnected = !!connectedAs

    return (
        <div className={styles.accountRow}>
            <div className={styles.accountInfo}>
                <Avatar name={name} size={36} />
                <div>
                    <Text weight="semibold">{name}</Text>
                    <Caption1 className={styles.captionBlock}>
                        {isConnected ? <>Connected as <b>{connectedAs}</b></> : 'Not connected'}
                    </Caption1>
                </div>
            </div>
            {actions ?? (
                isConnected ? (
                    <div className={styles.connectionActions}>
                        <Badge appearance="filled" color="success" size="small">Connected</Badge>
                        <Button appearance="subtle" size="small" icon={<OpenRegular />}>View</Button>
                    </div>
                ) : (
                    <Button appearance="outline" size="small">Connect</Button>
                )
            )}
        </div>
    )
}
