import {
    makeStyles,
    tokens,
    Button,
    Divider,
    mergeClasses,
} from '@fluentui/react-components'
import { AddRegular, DismissRegular } from '@fluentui/react-icons'
import type { ChatSessionData } from '../../models'

const useStyles = makeStyles({
    sessionBar: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.5rem 1rem',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'auto',
    },
    sessionChip: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.25rem',
        fontSize: '12px',
        whiteSpace: 'nowrap',
        flexShrink: 0,
    },
    sessionChipActive: {
        fontWeight: 600,
    },
    sessionButton: {
        flex: 1,
    },
    deleteButton: {
        flexShrink: 0,
    },
})

interface ChatSessionBarProps {
    sessions: ChatSessionData[]
    activeSessionId: string
    onSelectSession: (id: string) => void
    onDeleteSession?: (id: string) => void
    onNewSession?: () => void
}

export function ChatSessionBar({ sessions, activeSessionId, onSelectSession, onDeleteSession, onNewSession }: ChatSessionBarProps) {
    const styles = useStyles()

    return (
        <div className={styles.sessionBar}>
            <Button appearance="subtle" size="small" icon={<AddRegular />} aria-label="New chat" onClick={onNewSession} />
            <Divider vertical />
            {sessions.map((session) => (
                <div key={session.id} className={styles.sessionChip}>
                    <Button
                        appearance={session.id === activeSessionId ? 'primary' : 'outline'}
                        size="small"
                        className={mergeClasses(styles.sessionButton, session.id === activeSessionId ? styles.sessionChipActive : undefined)}
                        onClick={() => onSelectSession(session.id)}
                    >
                        {session.title}
                    </Button>
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
                </div>
            ))}
        </div>
    )
}
