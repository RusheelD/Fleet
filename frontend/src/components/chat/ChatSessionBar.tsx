import { useEffect, useState } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Input,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Text,
    mergeClasses,
} from '@fluentui/react-components'
import {
    AddRegular,
    CheckmarkRegular,
    DeleteRegular,
    DismissRegular,
    EditRegular,
    MoreHorizontalRegular,
} from '@fluentui/react-icons'
import type { ChatSessionData } from '../../models'
import { useIsMobile, usePreferences } from '../../hooks'

const useStyles = makeStyles({
    sessionBar: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '1rem',
        paddingRight: '1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    sessionBarCompact: {
        gap: '0.25rem',
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    controlRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '0.5rem',
    },
    sessionMeta: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground3,
    },
    sessionsScroller: {
        display: 'flex',
        alignItems: 'stretch',
        gap: '0.5rem',
        overflowX: 'auto',
        overflowY: 'hidden',
        scrollbarWidth: 'thin',
        paddingBottom: '0.125rem',
    },
    sessionsScrollerCompact: {
        gap: '0.25rem',
    },
    sessionChip: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        flexShrink: 0,
        minWidth: '0',
        maxWidth: 'min(320px, 84vw)',
    },
    sessionChipCompact: {
        gap: '0.125rem',
        fontSize: '11px',
    },
    sessionChipActive: {
        fontWeight: 600,
    },
    sessionButton: {
        maxWidth: '240px',
        minWidth: '120px',
        justifyContent: 'flex-start',
    },
    sessionButtonMobile: {
        maxWidth: 'min(66vw, 220px)',
    },
    sessionTitle: {
        display: 'block',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        width: '100%',
    },
    renameInput: {
        width: 'clamp(160px, 30vw, 260px)',
    },
    renameInputMobile: {
        width: 'min(70vw, 240px)',
    },
    renameRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
    },
    deleteButton: {
        flexShrink: 0,
    },
    editButton: {
        flexShrink: 0,
    },
    newChatButton: {
        flexShrink: 0,
    },
    emptyState: {
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        color: tokens.colorNeutralForeground3,
    },
})

interface ChatSessionBarProps {
    sessions: ChatSessionData[]
    activeSessionId: string
    onSelectSession: (id: string) => void
    onDeleteSession?: (id: string) => void
    onRenameSession?: (id: string, title: string) => void
    onNewSession?: () => void
    actionsDisabled?: boolean
}

export function ChatSessionBar({
    sessions,
    activeSessionId,
    onSelectSession,
    onDeleteSession,
    onRenameSession,
    onNewSession,
    actionsDisabled = false,
}: ChatSessionBarProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const [editingSessionId, setEditingSessionId] = useState<string | null>(null)
    const [editTitle, setEditTitle] = useState('')

    useEffect(() => {
        if (!editingSessionId) return

        const exists = sessions.some((session) => session.id === editingSessionId)
        if (!exists) {
            setEditingSessionId(null)
            setEditTitle('')
        }
    }, [editingSessionId, sessions])

    const beginRename = (session: ChatSessionData) => {
        setEditingSessionId(session.id)
        setEditTitle(session.title)
    }

    const cancelRename = () => {
        setEditingSessionId(null)
        setEditTitle('')
    }

    const saveRename = (sessionId: string) => {
        const trimmed = editTitle.trim()
        if (!trimmed) {
            cancelRename()
            return
        }

        onRenameSession?.(sessionId, trimmed)
        cancelRename()
    }

    const requestDelete = (session: ChatSessionData) => {
        if (!onDeleteSession) return
        const confirmed = window.confirm(`Delete chat session "${session.title}"?`)
        if (confirmed) {
            onDeleteSession(session.id)
        }
    }

    return (
        <div className={mergeClasses(styles.sessionBar, isCompact && styles.sessionBarCompact)}>
            <div className={styles.controlRow}>
                <Button
                    appearance="subtle"
                    size="small"
                    icon={<AddRegular />}
                    aria-label="Create new chat"
                    className={styles.newChatButton}
                    onClick={onNewSession}
                    disabled={actionsDisabled}
                >
                    New Chat
                </Button>
                <Text className={styles.sessionMeta}>
                    {sessions.length} session{sessions.length === 1 ? '' : 's'}
                </Text>
            </div>

            <div className={mergeClasses(styles.sessionsScroller, isCompact && styles.sessionsScrollerCompact)}>
                {sessions.length === 0 && (
                    <Text className={styles.emptyState}>Create a chat session to get started.</Text>
                )}

                {sessions.map((session) => (
                    <div key={session.id} className={mergeClasses(styles.sessionChip, isCompact && styles.sessionChipCompact)}>
                        {editingSessionId === session.id ? (
                            <div className={styles.renameRow}>
                                <Input
                                    size="small"
                                    value={editTitle}
                                    autoFocus
                                    className={mergeClasses(styles.renameInput, isMobile && styles.renameInputMobile)}
                                    onChange={(_, data) => setEditTitle(data.value)}
                                    onKeyDown={(event) => {
                                        if (event.key === 'Enter') {
                                            event.preventDefault()
                                            saveRename(session.id)
                                        } else if (event.key === 'Escape') {
                                            event.preventDefault()
                                            cancelRename()
                                        }
                                    }}
                                />
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={<CheckmarkRegular />}
                                    className={styles.editButton}
                                    onClick={() => saveRename(session.id)}
                                    aria-label={`Save ${session.title}`}
                                    disabled={actionsDisabled}
                                />
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={<DismissRegular />}
                                    className={styles.deleteButton}
                                    onClick={cancelRename}
                                    aria-label={`Cancel rename for ${session.title}`}
                                    disabled={actionsDisabled}
                                />
                            </div>
                        ) : (
                            <>
                                <Button
                                    appearance={session.id === activeSessionId ? 'primary' : 'outline'}
                                    size="small"
                                    className={mergeClasses(
                                        styles.sessionButton,
                                        isMobile && styles.sessionButtonMobile,
                                        session.id === activeSessionId && styles.sessionChipActive,
                                    )}
                                    onClick={() => onSelectSession(session.id)}
                                    disabled={actionsDisabled}
                                >
                                    <span className={styles.sessionTitle}>{session.title}</span>
                                </Button>

                                {(onRenameSession || onDeleteSession) && (
                                    <Menu>
                                        <MenuTrigger disableButtonEnhancement>
                                            <Button
                                                appearance="subtle"
                                                size="small"
                                                icon={<MoreHorizontalRegular />}
                                                aria-label={`Session actions for ${session.title}`}
                                                disabled={actionsDisabled}
                                            />
                                        </MenuTrigger>
                                        <MenuPopover>
                                            <MenuList>
                                                {onRenameSession && (
                                                    <MenuItem
                                                        icon={<EditRegular />}
                                                        onClick={() => beginRename(session)}
                                                    >
                                                        Rename
                                                    </MenuItem>
                                                )}
                                                {onDeleteSession && (
                                                    <MenuItem
                                                        icon={<DeleteRegular />}
                                                        onClick={() => requestDelete(session)}
                                                    >
                                                        Delete
                                                    </MenuItem>
                                                )}
                                            </MenuList>
                                        </MenuPopover>
                                    </Menu>
                                )}
                            </>
                        )}
                    </div>
                ))}
            </div>
        </div>
    )
}
