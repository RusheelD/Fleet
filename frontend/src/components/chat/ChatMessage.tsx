import {
    makeStyles,
    tokens,
    Avatar,
    Text,
    mergeClasses,
} from '@fluentui/react-components'
import { BotRegular, PersonRegular } from '@fluentui/react-icons'
import type { ChatMessageData } from '../../models'

const useStyles = makeStyles({
    messageRow: {
        display: 'flex',
        gap: '0.5rem',
        maxWidth: '100%',
    },
    messageRowUser: {
        alignSelf: 'flex-end',
        flexDirection: 'row-reverse',
    },
    messageRowAssistant: {
        alignSelf: 'flex-start',
    },
    messageBubble: {
        padding: '0.75rem 1rem',
        borderRadius: tokens.borderRadiusLarge,
        lineHeight: '1.5',
        fontSize: '13px',
    },
    messageBubbleUser: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundOnBrand,
    },
    messageBubbleAssistant: {
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground1,
    },
    messageTime: {
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
        marginTop: '0.25rem',
        textAlign: 'right',
    },
})

interface ChatMessageProps {
    message: ChatMessageData
}

export function ChatMessage({ message }: ChatMessageProps) {
    const styles = useStyles()

    return (
        <div
            className={mergeClasses(
                styles.messageRow,
                message.role === 'user' ? styles.messageRowUser : styles.messageRowAssistant,
            )}
        >
            <Avatar
                name={message.role === 'user' ? 'You' : 'Fleet AI'}
                icon={message.role === 'user' ? <PersonRegular /> : <BotRegular />}
                color={message.role === 'user' ? 'neutral' : 'brand'}
                size={28}
            />
            <div>
                <div
                    className={mergeClasses(
                        styles.messageBubble,
                        message.role === 'user' ? styles.messageBubbleUser : styles.messageBubbleAssistant,
                    )}
                >
                    {message.content.split('\n').map((line, i) => (
                        <span key={i}>
                            {line}
                            {i < message.content.split('\n').length - 1 && <br />}
                        </span>
                    ))}
                </div>
                <Text className={styles.messageTime}>{message.timestamp}</Text>
            </div>
        </div>
    )
}
