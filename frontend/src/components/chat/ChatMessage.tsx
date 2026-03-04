import {
    makeStyles,
    tokens,
    Avatar,
    Text,
    mergeClasses,
    Link,
} from '@fluentui/react-components'
import { BotRegular, PersonRegular } from '@fluentui/react-icons'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
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
    // Markdown content styles
    markdown: {
        '& p': {
            marginTop: 0,
            marginBottom: '0.5rem',
        },
        '& p:last-child': {
            marginBottom: 0,
        },
        '& ul, & ol': {
            marginTop: '0.25rem',
            marginBottom: '0.5rem',
            paddingLeft: '1.25rem',
        },
        '& li': {
            marginBottom: '0.15rem',
        },
        '& code': {
            fontFamily: tokens.fontFamilyMonospace,
            fontSize: '12px',
            padding: '0.1rem 0.35rem',
            borderRadius: tokens.borderRadiusMedium,
            backgroundColor: tokens.colorNeutralBackground1,
        },
        '& pre': {
            marginTop: '0.25rem',
            marginBottom: '0.5rem',
            padding: '0.75rem',
            borderRadius: tokens.borderRadiusMedium,
            backgroundColor: tokens.colorNeutralBackground1,
            overflowX: 'auto',
            fontSize: '12px',
        },
        '& pre code': {
            padding: 0,
            backgroundColor: 'transparent',
        },
        '& h1, & h2, & h3, & h4': {
            marginTop: '0.5rem',
            marginBottom: '0.25rem',
            fontWeight: tokens.fontWeightSemibold,
        },
        '& h1': { fontSize: '16px' },
        '& h2': { fontSize: '15px' },
        '& h3': { fontSize: '14px' },
        '& blockquote': {
            marginTop: '0.25rem',
            marginBottom: '0.5rem',
            paddingLeft: '0.75rem',
            borderLeft: `3px solid ${tokens.colorNeutralStroke2}`,
            color: tokens.colorNeutralForeground3,
        },
        '& table': {
            borderCollapse: 'collapse',
            marginTop: '0.25rem',
            marginBottom: '0.5rem',
            fontSize: '12px',
        },
        '& th, & td': {
            border: `1px solid ${tokens.colorNeutralStroke2}`,
            padding: '0.25rem 0.5rem',
        },
        '& th': {
            fontWeight: tokens.fontWeightSemibold,
            backgroundColor: tokens.colorNeutralBackground1,
        },
        '& hr': {
            border: 'none',
            borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
            marginTop: '0.5rem',
            marginBottom: '0.5rem',
        },
    },
    // User bubble code blocks need inverted colors
    markdownUser: {
        '& code': {
            backgroundColor: 'rgba(255,255,255,0.15)',
        },
        '& pre': {
            backgroundColor: 'rgba(255,255,255,0.1)',
        },
        '& th': {
            backgroundColor: 'rgba(255,255,255,0.1)',
        },
        '& th, & td': {
            borderColor: 'rgba(255,255,255,0.25)',
        },
        '& blockquote': {
            borderLeftColor: 'rgba(255,255,255,0.4)',
            color: 'rgba(255,255,255,0.8)',
        },
        '& hr': {
            borderTopColor: 'rgba(255,255,255,0.25)',
        },
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
                    {message.role === 'user' ? (
                        <div className={mergeClasses(styles.markdown, styles.markdownUser)}>
                            <Markdown remarkPlugins={[remarkGfm]}
                                components={{
                                    a: ({ children, href }) => (
                                        <Link href={href ?? '#'} target="_blank" rel="noopener noreferrer" inline>{children}</Link>
                                    ),
                                }}
                            >
                                {message.content}
                            </Markdown>
                        </div>
                    ) : (
                        <div className={styles.markdown}>
                            <Markdown remarkPlugins={[remarkGfm]}
                                components={{
                                    a: ({ children, href }) => (
                                        <Link href={href ?? '#'} target="_blank" rel="noopener noreferrer" inline>{children}</Link>
                                    ),
                                }}
                            >
                                {message.content}
                            </Markdown>
                        </div>
                    )}
                </div>
                <Text className={styles.messageTime}>{message.timestamp}</Text>
            </div>
        </div>
    )
}
