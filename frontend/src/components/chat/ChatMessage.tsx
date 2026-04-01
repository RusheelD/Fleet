import {
    makeStyles,
    mergeClasses,
    Avatar,
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
import { appTokens } from '../../styles/appTokens'
import { InfoBadge } from '../shared/InfoBadge'

const useStyles = makeStyles({
    messageRow: {
        display: 'flex',
        gap: appTokens.space.sm,
        maxWidth: '100%',
        width: '100%',
        minWidth: 0,
        alignItems: 'flex-start',
    },
    messageRowCompact: {
        gap: appTokens.space.xs,
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
        paddingTop: appTokens.space.md,
        paddingBottom: appTokens.space.md,
        paddingLeft: appTokens.space.lg,
        paddingRight: appTokens.space.lg,
        borderRadius: appTokens.radius.lg,
        lineHeight: appTokens.lineHeight.relaxed,
        fontSize: appTokens.fontSize.md,
        maxWidth: '100%',
        width: '100%',
        minWidth: 0,
        boxSizing: 'border-box',
        boxShadow: appTokens.shadow.card,
    },
    messageBubbleCompact: {
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        fontSize: appTokens.fontSize.sm,
        lineHeight: appTokens.lineHeight.base,
    },
    messageBubbleUser: {
        backgroundColor: appTokens.color.surfaceBrandSolid,
        color: appTokens.color.textOnBrand,
    },
    messageBubbleAssistant: {
        backgroundColor: appTokens.color.pageBackground,
        color: appTokens.color.textPrimary,
        border: appTokens.border.subtle,
    },
    messageTime: {
        color: appTokens.color.textMuted,
        fontSize: appTokens.fontSize.xs,
        marginTop: appTokens.space.xxs,
    },
    messageTimeCompact: {
        fontSize: appTokens.fontSize.xxs,
        marginTop: appTokens.space.xxxs,
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
        gap: appTokens.space.xs,
        marginTop: appTokens.space.xs,
        maxWidth: '100%',
    },
    attachmentListCompact: {
        gap: appTokens.space.xxs,
        marginTop: appTokens.space.xxs,
    },
    attachmentChip: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: appTokens.space.xs,
        paddingTop: appTokens.space.xxs,
        paddingBottom: appTokens.space.xxs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        borderRadius: appTokens.radius.md,
        backgroundColor: appTokens.color.surface,
        border: appTokens.border.subtle,
        maxWidth: '100%',
        minWidth: 0,
        boxSizing: 'border-box',
    },
    attachmentChipUser: {
        backgroundColor: appTokens.color.onBrandSurfaceMuted,
        borderTopColor: appTokens.color.onBrandBorderSoft,
        borderRightColor: appTokens.color.onBrandBorderSoft,
        borderBottomColor: appTokens.color.onBrandBorderSoft,
        borderLeftColor: appTokens.color.onBrandBorderSoft,
    },
    attachmentChipCompact: {
        gap: appTokens.space.xxs,
        paddingTop: appTokens.space.xxxs,
        paddingBottom: appTokens.space.xxxs,
        paddingLeft: appTokens.space.xs,
        paddingRight: appTokens.space.xs,
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
        fontSize: appTokens.fontSize.xs,
        lineHeight: appTokens.lineHeight.tight,
    },
    assistantAvatar: {
        backgroundColor: appTokens.color.brand,
        color: appTokens.color.textOnBrand,
    },
    // Markdown content styles
    markdown: {
        minWidth: 0,
        maxWidth: '100%',
        overflowWrap: 'anywhere',
        '& p': {
            marginTop: 0,
            marginBottom: appTokens.space.sm,
            overflowWrap: 'anywhere',
        },
        '& p:last-child': {
            marginBottom: 0,
        },
        '& ul, & ol': {
            marginTop: appTokens.space.xxs,
            marginBottom: appTokens.space.sm,
            paddingLeft: '1.25rem',
        },
        '& li': {
            marginBottom: '0.15rem',
            overflowWrap: 'anywhere',
        },
        '& code': {
            fontFamily: appTokens.fontFamily.monospace,
            fontSize: appTokens.fontSize.sm,
            padding: '0.1rem 0.35rem',
            borderRadius: appTokens.radius.md,
            backgroundColor: appTokens.color.surface,
            overflowWrap: 'anywhere',
        },
        '& pre': {
            marginTop: appTokens.space.xxs,
            marginBottom: appTokens.space.sm,
            padding: appTokens.space.md,
            borderRadius: appTokens.radius.md,
            backgroundColor: appTokens.color.surface,
            boxSizing: 'border-box',
            maxWidth: '100%',
            display: 'block',
            overflowX: 'auto',
            overflowY: 'hidden',
            whiteSpace: 'pre',
            fontSize: appTokens.fontSize.sm,
        },
        '& pre code': {
            padding: 0,
            backgroundColor: 'transparent',
            whiteSpace: 'pre',
            overflowWrap: 'normal',
        },
        '& h1, & h2, & h3, & h4': {
            marginTop: appTokens.space.sm,
            marginBottom: appTokens.space.xxs,
            fontWeight: appTokens.fontWeight.semibold,
        },
        '& h1': { fontSize: '16px' },
        '& h2': { fontSize: '15px' },
        '& h3': { fontSize: '14px' },
        '& blockquote': {
            marginTop: appTokens.space.xxs,
            marginBottom: appTokens.space.sm,
            paddingLeft: appTokens.space.md,
            borderLeft: `3px solid ${appTokens.color.border}`,
            color: appTokens.color.textTertiary,
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
            borderTopColor: appTokens.color.border,
            borderRightColor: appTokens.color.border,
            borderBottomColor: appTokens.color.border,
            borderLeftColor: appTokens.color.border,
            padding: `${appTokens.space.xxs} ${appTokens.space.sm}`,
            overflowWrap: 'anywhere',
        },
        '& th': {
            fontWeight: appTokens.fontWeight.semibold,
            backgroundColor: appTokens.color.surface,
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
            borderTopColor: appTokens.color.border,
            marginTop: appTokens.space.sm,
            marginBottom: appTokens.space.sm,
        },
    },
    // User bubble code blocks need inverted colors
    markdownUser: {
        '& code': {
            backgroundColor: appTokens.color.onBrandSurfaceStrong,
        },
        '& pre': {
            backgroundColor: appTokens.color.onBrandSurfaceSoft,
        },
        '& th': {
            backgroundColor: appTokens.color.onBrandSurfaceSoft,
        },
        '& th, & td': {
            borderTopColor: appTokens.color.onBrandBorderSoft,
            borderRightColor: appTokens.color.onBrandBorderSoft,
            borderBottomColor: appTokens.color.onBrandBorderSoft,
            borderLeftColor: appTokens.color.onBrandBorderSoft,
        },
        '& blockquote': {
            borderLeftColor: appTokens.color.onBrandBorderStrong,
            color: appTokens.color.onBrandTextMuted,
        },
        '& hr': {
            borderTopColor: appTokens.color.onBrandBorderSoft,
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
                    color="neutral"
                    className={message.role === 'assistant' ? styles.assistantAvatar : undefined}
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
                                <InfoBadge appearance="filled" size={isCompact ? 'tiny' : 'small'}>
                                    {formatSize(attachment.contentLength)}
                                </InfoBadge>
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
