import { useState, useEffect, useRef, useMemo } from 'react'
import { makeStyles, tokens, Spinner } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatSuggestions, ChatInput, AttachedFiles } from './'
import { ToolEventMessage } from './ToolEventMessage'

import {
    useChatData, useChatMessages, useCreateChatSession,
    useAttachments, useUploadAttachment, useDeleteAttachment, useDeleteSession,
    sendChatMessage,
} from '../../proxies'
import { useChatGenerating } from '../../hooks'
import type { ChatMessageData, SendMessageResponse } from '../../models'

const useStyles = makeStyles({
    drawer: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground1,
        flexShrink: 0,
        height: '100%',
        overflow: 'hidden',
        width: '100%',
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
    const queryClient = useQueryClient()
    const [message, setMessage] = useState('')
    const [activeSession, setActiveSession] = useState<string | undefined>(undefined)
    const [optimisticMessages, setOptimisticMessages] = useState<ChatMessageData[]>([])
    const [isThinking, setIsThinking] = useState(false)
    const { isGenerating, setIsGenerating } = useChatGenerating()
    const [lastSendResponse, setLastSendResponse] = useState<SendMessageResponse | null>(null)
    const messagesEndRef = useRef<HTMLDivElement>(null)

    const { data: chatData, isLoading: loadingChat } = useChatData(projectId)
    const { data: messages } = useChatMessages(projectId, activeSession, {
        pollingInterval: isThinking ? 3_000 : false,
    })
    const createSessionMutation = useCreateChatSession(projectId)
    const deletSessionMutation = useDeleteSession(projectId)
    const { data: attachments } = useAttachments(projectId, activeSession)
    const uploadMutation = useUploadAttachment(projectId, activeSession)
    const deleteMutation = useDeleteAttachment(projectId, activeSession)

    const sessions = useMemo(() => chatData?.sessions ?? [], [chatData?.sessions])
    const serverMessages = useMemo(() => messages ?? chatData?.messages ?? [], [messages, chatData?.messages])

    // Detect if the active session has a pending generate on the server (e.g. after page refresh)
    const activeSessionData = useMemo(
        () => sessions.find(s => s.id === activeSession),
        [sessions, activeSession]
    )

    // Restore thinking/generating state from the server flag on mount or session switch
    useEffect(() => {
        if (activeSessionData?.isGenerating && !isThinking) {
            setIsThinking(true)
            setIsGenerating(true)
        }
    }, [activeSessionData?.isGenerating, activeSession]) // eslint-disable-line react-hooks/exhaustive-deps

    // Track the message count when thinking started so we can detect *new* assistant replies
    const messageCountAtThinkingStart = useRef(serverMessages.length)
    useEffect(() => {
        if (isThinking) {
            messageCountAtThinkingStart.current = serverMessages.length
        }
    }, [isThinking]) // eslint-disable-line react-hooks/exhaustive-deps

    // When polling picks up a NEW assistant response (added after thinking started), clear thinking
    useEffect(() => {
        if (!isThinking) return
        // Only consider messages that arrived after thinking started
        if (serverMessages.length <= messageCountAtThinkingStart.current) return
        const lastMsg = serverMessages[serverMessages.length - 1]
        if (lastMsg && lastMsg.role === 'assistant') {
            setIsThinking(false)
            setIsGenerating(false)
            // Don't clear optimistic here — the displayMessages dedup handles the
            // transition seamlessly. Optimistic messages are cleaned up in doSend's
            // .then() after queries are refreshed.
            void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
            if (isGenerating) {
                void queryClient.invalidateQueries({ queryKey: ['work-items'] })
            }
        }
    }, [serverMessages, isThinking]) // eslint-disable-line react-hooks/exhaustive-deps

    // Only show optimistic messages that haven't yet appeared in server data
    const displayMessages = useMemo(() => {
        const serverIds = new Set(serverMessages.map(m => m.id))
        const pending = optimisticMessages.filter(m => !serverIds.has(m.id))
        // Also deduplicate by content — if the server already has the user message, skip
        const serverUserContents = new Set(
            serverMessages.filter(m => m.role === 'user').map(m => m.content)
        )
        const unique = pending.filter(m => m.role !== 'user' || !serverUserContents.has(m.content))
        return [...serverMessages, ...unique]
    }, [serverMessages, optimisticMessages])
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

    const doSend = (sessionId: string, userContent: string, generateWorkItems: boolean) => {
        setMessage('')

        const defaultGenerateMessage = 'Generate work items based on the context you have been given.'

        // If generating with no user input, use the default generate message
        const contentToSend = generateWorkItems && !userContent
            ? defaultGenerateMessage
            : userContent

        // Optimistic: show user message immediately
        const displayContent = generateWorkItems && !userContent
            ? '📋 Generate work items'
            : userContent
        setOptimisticMessages([{
            id: `optimistic-${Date.now()}`,
            role: 'user',
            content: displayContent,
            timestamp: new Date().toISOString(),
        }])
        setIsThinking(true)
        setIsGenerating(generateWorkItems)

        // Call API directly with the explicit sessionId to avoid stale closures
        // when a session was just auto-created
        sendChatMessage(projectId, sessionId, contentToSend, generateWorkItems)
            .then(async (response: SendMessageResponse) => {
                setLastSendResponse(response)
                // Wait for server data to be fresh BEFORE clearing optimistic state
                // so the user message never flashes away
                await queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-data'] })
                setIsThinking(false)
                setIsGenerating(false)
                setOptimisticMessages([])
                if (generateWorkItems) {
                    void queryClient.invalidateQueries({ queryKey: ['work-items'] })
                }
            })
            .catch(() => {
                setIsThinking(false)
                setIsGenerating(false)
                setOptimisticMessages([])
            })
    }

    const handleSend = (generateWorkItems = false) => {
        const userContent = message.trim()
        if (isThinking || createSessionMutation.isPending) return
        if (!userContent && !generateWorkItems) return

        // Auto-create a session if none exists, then send
        if (!activeSession) {
            createSessionMutation.mutate('New Chat', {
                onSuccess: (session) => {
                    setActiveSession(session.id)
                    doSend(session.id, userContent, generateWorkItems)
                },
            })
            return
        }

        doSend(activeSession, userContent, generateWorkItems)
    }

    const handleNewSession = () => {
        createSessionMutation.mutate('New Chat', {
            onSuccess: (session) => {
                setActiveSession(session.id)
                setOptimisticMessages([])
            },
        })
    }

    const handleDeleteSession = (sessionId: string) => {
        deletSessionMutation.mutate(sessionId, {
            onSuccess: () => {
                // If deleted session was active, switch to first remaining session or clear
                if (activeSession === sessionId) {
                    const remainingSessions = sessions.filter(s => s.id !== sessionId)
                    setActiveSession(remainingSessions.length > 0 ? remainingSessions[0].id : undefined)
                }
                setOptimisticMessages([])
            },
        })
    }

    // Show tool events from last response
    const toolEvents = lastSendResponse?.toolEvents ?? []

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
                onDeleteSession={handleDeleteSession}
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
                            <Spinner
                                size="tiny"
                                label={isGenerating
                                    ? 'Fleet AI is generating work items — this may take a while…'
                                    : 'Fleet AI is thinking…'}
                                labelPosition="after"
                            />
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
                onSend={() => handleSend(false)}
                onGenerate={() => handleSend(true)}
                onFileSelect={handleFileSelect}
                disabled={isThinking || createSessionMutation.isPending}
                uploading={uploadMutation.isPending}
            />
        </div>
    )
}
