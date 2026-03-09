import { useEffect, useState } from 'react'
import {
    makeStyles,
    tokens,
    Button,
    Divider,
    Input,
    mergeClasses,
} from '@fluentui/react-components'
import { AddRegular, DismissRegular, EditRegular, CheckmarkRegular } from '@fluentui/react-icons'
import type { ChatSessionData } from '../../models'
import { usePreferences } from '../../hooks'

const useStyles = makeStyles({
    sessionBar: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.5rem 1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'auto',
    },
    sessionBarCompact: {
        gap: '0.25rem',
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
    },
    sessionChip: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        fontSize: '12px',
        whiteSpace: 'nowrap',
        flexShrink: 0,
    },
    sessionChipCompact: {
        gap: '0.125rem',
        fontSize: '11px',
    },
    sessionChipActive: {
        fontWeight: 600,
    },
    sessionButton: {
        flex: 1,
    },
    renameInput: {
        minWidth: '160px',
        maxWidth: '240px',
    },
    deleteButton: {
        flexShrink: 0,
    },
    editButton: {
        flexShrink: 0,
    },
})

interface ChatSessionBarProps {
    sessions: ChatSessionData[]
    activeSessionId: string
    onSelectSession: (id: string) => void
    onDeleteSession?: (id: string) => void
    onRenameSession?: (id: string, title: string) => void
    onNewSession?: () => void
}

export function ChatSessionBar({
    sessions,
    activeSessionId,
    onSelectSession,
    onDeleteSession,
    onRenameSession,
    onNewSession,
}: ChatSessionBarProps) {
    const styles = useStyles()
    const { preferences } = usePreferences()
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

    return (
        <div className={mergeClasses(styles.sessionBar, isCompact && styles.sessionBarCompact)}>
            <Button appearance="subtle" size="small" icon={<AddRegular />} aria-label="New chat" onClick={onNewSession} />
            <Divider vertical />
            {sessions.map((session) => (
                <div key={session.id} className={mergeClasses(styles.sessionChip, isCompact && styles.sessionChipCompact)}>
                    {editingSessionId === session.id ? (
                        <Input
                            size="small"
                            value={editTitle}
                            className={styles.renameInput}
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
                    ) : (
                        <Button
                            appearance={session.id === activeSessionId ? 'primary' : 'outline'}
                            size="small"
                            className={mergeClasses(styles.sessionButton, session.id === activeSessionId ? styles.sessionChipActive : undefined)}
                            onClick={() => onSelectSession(session.id)}
                        >
                            {session.title}
                        </Button>
                    )}
                    {editingSessionId === session.id ? (
                        <>
                            <Button
                                appearance="subtle"
                                size="small"
                                icon={<CheckmarkRegular />}
                                className={styles.editButton}
                                onClick={() => saveRename(session.id)}
                                aria-label={`Save ${session.title}`}
                            />
                            <Button
                                appearance="subtle"
                                size="small"
                                icon={<DismissRegular />}
                                className={styles.deleteButton}
                                onClick={cancelRename}
                                aria-label={`Cancel rename for ${session.title}`}
                            />
                        </>
                    ) : (
                        <>
                            {onRenameSession && (
                                <Button
                                    appearance="subtle"
                                    size="small"
                                    icon={<EditRegular />}
                                    className={styles.editButton}
                                    onClick={() => beginRename(session)}
                                    aria-label={`Rename ${session.title}`}
                                />
                            )}
                    {onDeleteSession && (
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<DismissRegular />}
                            className={styles.deleteButton}
                            onClick={() => onDeleteSession(session.id)}
                            aria-label={`Delete ${session.title}`}
                        />
                    )}
                        </>
                    )}
                </div>
            ))}
        </div>
    )
}
