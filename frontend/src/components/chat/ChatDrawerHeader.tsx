import {
    makeStyles,
    mergeClasses,
    tokens,
    Caption1,
    Text,
    Button,
} from '@fluentui/react-components'
import {
    BotRegular,
    DismissRegular,
} from '@fluentui/react-icons'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    drawerHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    drawerHeaderCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    drawerHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
    },
    drawerHeaderLeftCompact: {
        gap: '0.5rem',
    },
    drawerHeaderIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
    },
    drawerHeaderIconCompact: {
        fontSize: '14px',
    },
    drawerHeaderInfo: {
        display: 'flex',
        flexDirection: 'column',
    },
    drawerHeaderCaption: {
        display: 'block',
    },
    drawerHeaderCaptionCompact: {
        fontSize: '10px',
        lineHeight: '12px',
    },
    drawerHeaderRight: {
        display: 'flex',
        gap: '0.5rem',
        alignItems: 'center',
    },
})

interface ChatDrawerHeaderProps {
    onClose: () => void
}

export function ChatDrawerHeader({ onClose }: ChatDrawerHeaderProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false

    return (
        <div className={mergeClasses(styles.drawerHeader, isCompact && styles.drawerHeaderCompact)}>
            <div className={mergeClasses(styles.drawerHeaderLeft, isCompact && styles.drawerHeaderLeftCompact)}>
                <BotRegular className={mergeClasses(styles.drawerHeaderIcon, isCompact && styles.drawerHeaderIconCompact)} />
                <div className={styles.drawerHeaderInfo}>
                    <Text weight="semibold" size={isCompact ? 200 : 300}>AI Chat</Text>
                    <Caption1 className={mergeClasses(styles.drawerHeaderCaption, isCompact && styles.drawerHeaderCaptionCompact)}>Fleet AI Assistant</Caption1>
                </div>
            </div>
            <div className={styles.drawerHeaderRight}>
                <Button appearance="subtle" size="small" icon={<DismissRegular />} onClick={onClose} aria-label="Close chat" />
            </div>
        </div>
    )
}
