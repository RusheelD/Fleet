import {
    makeStyles,
    tokens,
    Button,
    Divider,
    mergeClasses,
} from '@fluentui/react-components'
import { AddRegular } from '@fluentui/react-icons'
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
        fontSize: '12px',
        whiteSpace: 'nowrap',
        flexShrink: 0,
    },
    sessionChipActive: {
        fontWeight: 600,
    },
})

interface ChatSessionBarProps {
    sessions: ChatSessionData[]
    activeSessionId: string
    onSelectSession: (id: string) => void
}

export function ChatSessionBar({ sessions, activeSessionId, onSelectSession }: ChatSessionBarProps) {
    const styles = useStyles()

    return (
        <div className={styles.sessionBar}>
            <Button appearance="subtle" size="small" icon={<AddRegular />} aria-label="New chat" />
            <Divider vertical />
            {sessions.map((session) => (
                <Button
                    key={session.id}
                    appearance={session.id === activeSessionId ? 'primary' : 'outline'}
                    size="small"
                    className={mergeClasses(styles.sessionChip, session.id === activeSessionId ? styles.sessionChipActive : undefined)}
                    onClick={() => onSelectSession(session.id)}
                >
                    {session.title}
                </Button>
            ))}
        </div>
    )
}
