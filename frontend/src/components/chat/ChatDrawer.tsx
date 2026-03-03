import { useState } from 'react'
import { makeStyles, tokens, Spinner } from '@fluentui/react-components'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatSuggestions, ChatInput } from './'

import { useChatData, useChatMessages, useCreateChatSession, useSendMessage } from '../../proxies'

const useStyles = makeStyles({
    drawer: {
        width: '420px',
        borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        flexShrink: 0,
        height: '100%',
        overflow: 'hidden',
    },
    messagesContainer: {
        flex: 1,
        overflow: 'auto',
        padding: '1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
    },
})

interface ChatDrawerProps {
    projectId: string
    onClose: () => void
}

export function ChatDrawer({ projectId, onClose }: ChatDrawerProps) {
    const styles = useStyles()
    const [message, setMessage] = useState('')
    const [activeSession, setActiveSession] = useState<string | undefined>(undefined)
    const { data: chatData, isLoading: loadingChat } = useChatData(projectId)
    const { data: messages } = useChatMessages(projectId, activeSession)
    const createSessionMutation = useCreateChatSession(projectId)
    const sendMutation = useSendMessage(projectId, activeSession ?? '')

    const sessions = chatData?.sessions ?? []
    const displayMessages = messages ?? chatData?.messages ?? []
    const suggestions = chatData?.suggestions ?? []

    // Auto-select first session when chat data loads
    if (!activeSession && sessions.length > 0) {
        setActiveSession(sessions[0].id)
    }

    const handleSend = () => {
        if (!message.trim() || !activeSession) return
        sendMutation.mutate(message.trim(), {
            onSuccess: () => setMessage(''),
        })
    }

    const handleNewSession = () => {
        createSessionMutation.mutate('New Chat', {
            onSuccess: (session) => {
                setActiveSession(session.id)
            },
        })
    }

    return (
        <div className={styles.drawer}>
            <ChatDrawerHeader onClose={onClose} />

            <ChatSessionBar
                sessions={sessions}
                activeSessionId={activeSession ?? ''}
                onSelectSession={setActiveSession}
                onNewSession={handleNewSession}
            />

            {loadingChat ? (
                <div className={styles.messagesContainer}>
                    <Spinner label="Loading chat..." />
                </div>
            ) : (
                <div className={styles.messagesContainer}>
                    {displayMessages.map((msg) => (
                        <ChatMessage key={msg.id} message={msg} />
                    ))}
                </div>
            )}

            <ChatSuggestions suggestions={suggestions} onSelect={setMessage} />

            <ChatInput value={message} onChange={setMessage} onSend={handleSend} />
        </div>
    )
}
