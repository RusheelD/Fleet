import {
    makeStyles,
    tokens,
    Caption1,
    Text,
    Button,
    Badge,
} from '@fluentui/react-components'
import {
    BotRegular,
    CheckmarkCircleRegular,
    DismissRegular,
} from '@fluentui/react-icons'

const useStyles = makeStyles({
    drawerHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    drawerHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    drawerHeaderIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
    },
    drawerHeaderInfo: {
        display: 'flex',
        flexDirection: 'column',
    },
    drawerHeaderCaption: {
        display: 'block',
    },
    drawerHeaderRight: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'center',
    },
    badgeIcon: {
        marginRight: '0.25rem',
    },
})

interface ChatDrawerHeaderProps {
    onClose: () => void
}

export function ChatDrawerHeader({ onClose }: ChatDrawerHeaderProps) {
    const styles = useStyles()

    return (
        <div className={styles.drawerHeader}>
            <div className={styles.drawerHeaderLeft}>
                <BotRegular className={styles.drawerHeaderIcon} />
                <div className={styles.drawerHeaderInfo}>
                    <Text weight="semibold">AI Chat</Text>
                    <Caption1 className={styles.drawerHeaderCaption}>Fleet AI Assistant</Caption1>
                </div>
            </div>
            <div className={styles.drawerHeaderRight}>
                <Badge appearance="filled" color="success" size="small">
                    <CheckmarkCircleRegular className={styles.badgeIcon} />
                    12 items generated
                </Badge>
                <Button appearance="subtle" size="small" icon={<DismissRegular />} onClick={onClose} aria-label="Close chat" />
            </div>
        </div>
    )
}
