import {
    makeStyles,
    mergeClasses,
    Caption1,
    Text,
    Button,
} from '@fluentui/react-components'
import {
    BotRegular,
    DismissRegular,
    StopRegular,
} from '@fluentui/react-icons'
import { usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'

const useStyles = makeStyles({
    drawerHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '0.75rem 1rem',
        borderBottom: appTokens.border.subtle,
    },
    drawerHeaderCompact: {
        paddingTop: '0.375rem',
        paddingBottom: '0.375rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    drawerHeaderMobile: {
        paddingTop: 'max(0.5rem, env(safe-area-inset-top))',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
    },
    drawerHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.75rem',
        minWidth: 0,
    },
    drawerHeaderLeftCompact: {
        gap: '0.5rem',
    },
    drawerHeaderIcon: {
        fontSize: '20px',
        color: appTokens.color.brand,
    },
    drawerHeaderIconCompact: {
        fontSize: '14px',
    },
    drawerHeaderInfo: {
        display: 'flex',
        flexDirection: 'column',
        minWidth: 0,
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
    isGenerating?: boolean
    isCanceling?: boolean
    onCancelGeneration?: () => void
}

export function ChatDrawerHeader({
    onClose,
    isGenerating = false,
    isCanceling = false,
    onCancelGeneration,
}: ChatDrawerHeaderProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false

    return (
        <div className={mergeClasses(styles.drawerHeader, isCompact && styles.drawerHeaderCompact, isMobile && styles.drawerHeaderMobile)}>
            <div className={mergeClasses(styles.drawerHeaderLeft, isCompact && styles.drawerHeaderLeftCompact)}>
                <BotRegular className={mergeClasses(styles.drawerHeaderIcon, isCompact && styles.drawerHeaderIconCompact)} />
                <div className={styles.drawerHeaderInfo}>
                    <Text weight="semibold" size={isCompact ? 200 : 300}>AI Chat</Text>
                    <Caption1 className={mergeClasses(styles.drawerHeaderCaption, isCompact && styles.drawerHeaderCaptionCompact)}>Fleet AI Assistant</Caption1>
                </div>
            </div>
            <div className={styles.drawerHeaderRight}>
                {isGenerating && onCancelGeneration && (
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<StopRegular />}
                        onClick={onCancelGeneration}
                        disabled={isCanceling}
                    >
                        {isCanceling ? 'Canceling...' : 'Cancel'}
                    </Button>
                )}
                <Button appearance="subtle" size="small" icon={<DismissRegular />} onClick={onClose} aria-label="Close chat" />
            </div>
        </div>
    )
}
