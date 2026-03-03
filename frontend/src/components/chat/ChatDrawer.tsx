import { useState, useEffect, useRef, useMemo } from 'react'
import { makeStyles, tokens, Spinner } from '@fluentui/react-components'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatSuggestions, ChatInput, AttachedFiles } from './'
import { ToolEventMessage } from './ToolEventMessage'

import {
    useChatData, useChatMessages, useCreateChatSession, useSendMessage,
    useAttachments, useUploadAttachment, useDeleteAttachment,
} from '../../proxies'
import type { ChatMessageData } from '../../models'

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
    thinkingRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.5rem 0',
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
    const [optimisticMessages, setOptimisticMessages] = useState<ChatMessageData[]>([])
    const [isThinking, setIsThinking] = useState(false)
    const messagesEndRef = useRef<HTMLDivElement>(null)

    const { data: chatData, isLoading: loadingChat } = useChatData(projectId)
    const { data: messages } = useChatMessages(projectId, activeSession)
    const createSessionMutation = useCreateChatSession(projectId)
    const sendMutation = useSendMessage(projectId, activeSession ?? '')
    const { data: attachments } = useAttachments(projectId, activeSession)
    const uploadMutation = useUploadAttachment(projectId, activeSession)
    const deleteMutation = useDeleteAttachment(projectId, activeSession)

    const sessions = useMemo(() => chatData?.sessions ?? [], [chatData?.sessions])
    const serverMessages = messages ?? chatData?.messages ?? []
    const displayMessages = [...serverMessages, ...optimisticMessages]
    const suggestions = chatData?.suggestions ?? []

    // Auto-select first session when chat data loads
    useEffect(() => {
        if (!activeSession && sessions.length > 0) {
            setActiveSession(sessions[0].id)
        }
    }, [activeSession, sessions])

    // Scroll to bottom when messages change
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }, [displayMessages.length, isThinking])

    // Clear optimistic messages when server messages update
    useEffect(() => {
        if (optimisticMessages.length > 0 && !isThinking) {
            setOptimisticMessages([])
        }
    }, [serverMessages.length, isThinking, optimisticMessages.length])

    const handleSend = () => {
        if (!message.trim() || !activeSession || sendMutation.isPending) return

        const userContent = message.trim()
        setMessage('')

        // Optimistic: show user message immediately
        setOptimisticMessages([{
            id: `optimistic-${Date.now()}`,
            role: 'user',
            content: userContent,
            timestamp: new Date().toISOString(),
        }])
        setIsThinking(true)

        sendMutation.mutate(userContent, {
            onSuccess: () => {
                setIsThinking(false)
                setOptimisticMessages([])
            },
            onError: () => {
                setIsThinking(false)
                setOptimisticMessages([])
            },
        })
    }

    const handleNewSession = () => {
        createSessionMutation.mutate('New Chat', {
            onSuccess: (session) => {
                setActiveSession(session.id)
                setOptimisticMessages([])
            },
        })
    }

    // Show tool events from last response
    const lastResponse = sendMutation.data
    const toolEvents = lastResponse?.toolEvents ?? []

    const handleFileSelect = (file: File) => {
        if (!activeSession) return
        uploadMutation.mutate(file)
    }

    return (
        <div className={styles.drawer}>
            <ChatDrawerHeader onClose={onClose} />

            <ChatSessionBar
                sessions={sessions}
                activeSessionId={activeSession ?? ''}
                onSelectSession={(id) => { setActiveSession(id); setOptimisticMessages([]) }}
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
                    {toolEvents.length > 0 && toolEvents.map((evt, i) => (
                        <ToolEventMessage key={`tool-${i}`} event={evt} />
                    ))}
                    {isThinking && (
                        <div className={styles.thinkingRow}>
                            <Spinner size="tiny" label="Fleet AI is thinking..." labelPosition="after" />
                        </div>
                    )}
                    <div ref={messagesEndRef} />
                </div>
            )}

            <ChatSuggestions suggestions={suggestions} onSelect={setMessage} />

            <AttachedFiles
                attachments={attachments ?? []}
                onDelete={(id) => deleteMutation.mutate(id)}
                deleting={deleteMutation.isPending}
            />

            <ChatInput
                value={message}
                onChange={setMessage}
                onSend={handleSend}
                onFileSelect={handleFileSelect}
                disabled={sendMutation.isPending}
                uploading={uploadMutation.isPending}
            />
        </div>
    )
}
