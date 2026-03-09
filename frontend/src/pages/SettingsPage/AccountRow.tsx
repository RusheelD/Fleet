import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Text,
    Badge,
    Button,
    Avatar,
} from '@fluentui/react-components'
import { OpenRegular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import { useIsMobile } from '../../hooks'

const useStyles = makeStyles({
    accountRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.75rem',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground3,
        gap: '0.75rem',
    },
    accountRowMobile: {
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: '0.625rem',
    },
    accountInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: 0,
    },
    accountInfoMobile: {
        width: '100%',
    },
    captionBlock: {
        display: 'block' as const,
    },
    connectionActions: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'center',
        flexWrap: 'wrap',
    },
    connectionActionsMobile: {
        width: '100%',
    },
    actionsSlot: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-end',
        gap: '0.5rem',
        flexWrap: 'wrap',
    },
    actionsSlotMobile: {
        width: '100%',
        justifyContent: 'stretch',
        display: 'grid',
        gap: '0.5rem',
        '> .fui-Button': {
            width: '100%',
        },
    },
})

interface AccountRowProps {
    name: string
    /** Username displayed when connected (e.g., "RusheelD") */
    connectedAs?: string
    /** Override the default action area */
    actions?: ReactNode
    /** Called when View or Connect is clicked */
    onAction?: (action: 'view' | 'connect', name: string) => void
}

export function AccountRow({ name, connectedAs, actions, onAction }: AccountRowProps) {
    const styles = useStyles()
    const isMobile = useIsMobile()
    const isConnected = !!connectedAs

    return (
        <div className={mergeClasses(styles.accountRow, isMobile && styles.accountRowMobile)}>
            <div className={mergeClasses(styles.accountInfo, isMobile && styles.accountInfoMobile)}>
                <Avatar name={name} size={36} />
                <div>
                    <Text weight="semibold">{name}</Text>
                    <Caption1 className={styles.captionBlock}>
                        {isConnected ? <>Connected as <b>{connectedAs}</b></> : 'Not connected'}
                    </Caption1>
                </div>
            </div>
            <div className={mergeClasses(styles.actionsSlot, isMobile && styles.actionsSlotMobile)}>
                {actions ?? (
                    isConnected ? (
                        <div className={mergeClasses(styles.connectionActions, isMobile && styles.connectionActionsMobile)}>
                            <Badge appearance="filled" color="success" size="small">Connected</Badge>
                            <Button appearance="subtle" size="small" icon={<OpenRegular />} onClick={() => onAction?.('view', name)}>View</Button>
                        </div>
                    ) : (
                        <Button appearance="outline" size="small" onClick={() => onAction?.('connect', name)}>Connect</Button>
                    )
                )}
            </div>
        </div>
    )
}
