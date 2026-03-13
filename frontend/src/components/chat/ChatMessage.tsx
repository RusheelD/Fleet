import {
    makeStyles,
    mergeClasses,
    tokens,
    Avatar,
    Badge,
    Text,
    Link,
    Tooltip,
} from '@fluentui/react-components'
import { BotRegular, DocumentRegular, PersonRegular } from '@fluentui/react-icons'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { ChatMessageData } from '../../models'
import { formatInitials } from './initials'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    messageRow: {
        display: 'flex',
        gap: '0.5rem',
        maxWidth: '100%',
        width: '100%',
        minWidth: 0,
        alignItems: 'flex-start',
    },
    messageRowCompact: {
        gap: '0.375rem',
    },
    messageRowUser: {
        flexDirection: 'row-reverse',
        justifyContent: 'flex-start',
    },
    messageRowAssistant: {
        justifyContent: 'flex-start',
    },
    messageContent: {
        display: 'flex',
        flexDirection: 'column',
        minWidth: 0,
        flex: '1 1 auto',
    },
    messageContentUser: {
        maxWidth: 'min(78%, 42rem)',
    },
    messageContentAssistant: {
        maxWidth: 'min(100%, 52rem)',
    },
    messageBubble: {
        padding: '0.75rem 1rem',
        borderRadius: tokens.borderRadiusLarge,
        lineHeight: '1.5',
        fontSize: '13px',
        maxWidth: '100%',
        width: '100%',
        minWidth: 0,
        boxSizing: 'border-box',
        boxShadow: tokens.shadow4,
    },
    messageBubbleCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
        fontSize: '12px',
        lineHeight: '1.35',
    },
    messageBubbleUser: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundOnBrand,
    },
    messageBubbleAssistant: {
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground1,
        borderTopWidth: '1px',
        borderRightWidth: '1px',
        borderBottomWidth: '1px',
        borderLeftWidth: '1px',
        borderTopStyle: 'solid',
        borderRightStyle: 'solid',
        borderBottomStyle: 'solid',
        borderLeftStyle: 'solid',
        borderTopColor: tokens.colorNeutralStroke2,
        borderRightColor: tokens.colorNeutralStroke2,
        borderBottomColor: tokens.colorNeutralStroke2,
        borderLeftColor: tokens.colorNeutralStroke2,
    },
    messageTime: {
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
        marginTop: '0.25rem',
    },
    messageTimeCompact: {
        fontSize: '10px',
        marginTop: '0.125rem',
    },
    messageTimeUser: {
        textAlign: 'right',
    },
    messageTimeAssistant: {
        textAlign: 'left',
    },
    attachmentList: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '0.375rem',
        marginTop: '0.375rem',
        maxWidth: '100%',
    },
    attachmentListCompact: {
        gap: '0.25rem',
        marginTop: '0.25rem',
    },
    attachmentChip: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '0.375rem',
        padding: '0.25rem 0.5rem',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        maxWidth: '100%',
        minWidth: 0,
        boxSizing: 'border-box',
    },
    attachmentChipUser: {
        backgroundColor: 'rgba(255,255,255,0.12)',
        borderTopColor: 'rgba(255,255,255,0.25)',
        borderRightColor: 'rgba(255,255,255,0.25)',
        borderBottomColor: 'rgba(255,255,255,0.25)',
        borderLeftColor: 'rgba(255,255,255,0.25)',
    },
    attachmentChipCompact: {
        gap: '0.25rem',
        paddingTop: '0.125rem',
        paddingBottom: '0.125rem',
        paddingLeft: '0.375rem',
        paddingRight: '0.375rem',
    },
    attachmentName: {
        minWidth: 0,
        maxWidth: '12rem',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    attachmentNameCompact: {
        maxWidth: '9rem',
        fontSize: '11px',
        lineHeight: '14px',
    },
    // Markdown content styles
    markdown: {
        minWidth: 0,
        maxWidth: '100%',
        overflowWrap: 'anywhere',
        '& p': {
            marginTop: 0,
            marginBottom: '0.5rem',
            overflowWrap: 'anywhere',
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
            overflowWrap: 'anywhere',
        },
        '& code': {
            fontFamily: tokens.fontFamilyMonospace,
            fontSize: '12px',
            padding: '0.1rem 0.35rem',
            borderRadius: tokens.borderRadiusMedium,
            backgroundColor: tokens.colorNeutralBackground1,
            overflowWrap: 'anywhere',
        },
        '& pre': {
            marginTop: '0.25rem',
            marginBottom: '0.5rem',
            padding: '0.75rem',
            borderRadius: tokens.borderRadiusMedium,
            backgroundColor: tokens.colorNeutralBackground1,
            boxSizing: 'border-box',
            maxWidth: '100%',
            display: 'block',
            overflowX: 'auto',
            overflowY: 'hidden',
            whiteSpace: 'pre',
            fontSize: '12px',
        },
        '& pre code': {
            padding: 0,
            backgroundColor: 'transparent',
            whiteSpace: 'pre',
            overflowWrap: 'normal',
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
            display: 'block',
            width: 'max-content',
            maxWidth: '100%',
            overflowX: 'auto',
        },
        '& th, & td': {
            borderTopWidth: '1px',
            borderRightWidth: '1px',
            borderBottomWidth: '1px',
            borderLeftWidth: '1px',
            borderTopStyle: 'solid',
            borderRightStyle: 'solid',
            borderBottomStyle: 'solid',
            borderLeftStyle: 'solid',
            borderTopColor: tokens.colorNeutralStroke2,
            borderRightColor: tokens.colorNeutralStroke2,
            borderBottomColor: tokens.colorNeutralStroke2,
            borderLeftColor: tokens.colorNeutralStroke2,
            padding: '0.25rem 0.5rem',
            overflowWrap: 'anywhere',
        },
        '& th': {
            fontWeight: tokens.fontWeightSemibold,
            backgroundColor: tokens.colorNeutralBackground1,
        },
        '& a': {
            overflowWrap: 'anywhere',
        },
        '& img': {
            maxWidth: '100%',
            height: 'auto',
        },
        '& hr': {
            borderRightStyle: 'none',
            borderBottomStyle: 'none',
            borderLeftStyle: 'none',
            borderTopWidth: '1px',
            borderTopStyle: 'solid',
            borderTopColor: tokens.colorNeutralStroke2,
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
            borderTopColor: 'rgba(255,255,255,0.25)',
            borderRightColor: 'rgba(255,255,255,0.25)',
            borderBottomColor: 'rgba(255,255,255,0.25)',
            borderLeftColor: 'rgba(255,255,255,0.25)',
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
    currentUserIdentity?: string
}

export function ChatMessage({ message, currentUserIdentity }: ChatMessageProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const userIdentity = currentUserIdentity?.trim() || 'Me'

    return (
        <div
            data-chat-role={message.role}
            className={mergeClasses(
                styles.messageRow,
                isCompact && styles.messageRowCompact,
                message.role === 'user' ? styles.messageRowUser : styles.messageRowAssistant,
            )}
        >
            <div data-chat-avatar>
                <Avatar
                    name={message.role === 'user' ? userIdentity : 'Fleet AI'}
                    initials={message.role === 'user' ? formatInitials(userIdentity, 'Me') : 'FA'}
                    icon={message.role === 'user' ? <PersonRegular /> : <BotRegular />}
                    color={message.role === 'user' ? 'neutral' : 'brand'}
                    size={isCompact ? 24 : 28}
                />
            </div>
            <div
                className={mergeClasses(
                    styles.messageContent,
                    message.role === 'user' ? styles.messageContentUser : styles.messageContentAssistant,
                )}
            >
                <div
                    data-chat-bubble={message.role}
                    className={mergeClasses(
                        styles.messageBubble,
                        isCompact && styles.messageBubbleCompact,
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
                {message.attachments && message.attachments.length > 0 && (
                    <div className={mergeClasses(styles.attachmentList, isCompact && styles.attachmentListCompact)}>
                        {message.attachments.map((attachment) => (
                            <div
                                key={attachment.id}
                                className={mergeClasses(
                                    styles.attachmentChip,
                                    message.role === 'user' && styles.attachmentChipUser,
                                    isCompact && styles.attachmentChipCompact,
                                )}
                            >
                                <DocumentRegular fontSize={isCompact ? 12 : 14} />
                                <Tooltip
                                    content={`${attachment.fileName} (${formatSize(attachment.contentLength)})`}
                                    relationship="description"
                                >
                                    <Text
                                        size={200}
                                        className={mergeClasses(
                                            styles.attachmentName,
                                            isCompact && styles.attachmentNameCompact,
                                        )}
                                    >
                                        {attachment.fileName}
                                    </Text>
                                </Tooltip>
                                <Badge appearance="filled" size={isCompact ? 'tiny' : 'small'} color="informative">
                                    {formatSize(attachment.contentLength)}
                                </Badge>
                            </div>
                        ))}
                    </div>
                )}
                <Text
                    className={mergeClasses(
                        styles.messageTime,
                        isCompact && styles.messageTimeCompact,
                        message.role === 'user' ? styles.messageTimeUser : styles.messageTimeAssistant,
                    )}
                >
                    {message.timestamp}
                </Text>
            </div>
        </div>
    )
}

function formatSize(chars: number): string {
    if (chars < 1024) return `${chars} chars`
    return `${(chars / 1024).toFixed(1)} KB`
}
