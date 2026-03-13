import { useState, useEffect, useRef, useMemo } from 'react'
import { makeStyles, mergeClasses, tokens, Spinner } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatInput, AttachedFiles } from './'
import { ToolEventMessage } from './ToolEventMessage'

import {
    useChatData, useChatMessages, useCreateChatSession,
    useAttachments, useUploadAttachment, useDeleteAttachment, useDeleteSession, useRenameSession,
    sendChatMessage,
} from '../../proxies'
import { useChatGenerating, useAuth, usePreferences } from '../../hooks'
import type { ChatAttachment, ChatMessageData, SendMessageResponse } from '../../models'
import { resolveChatUserIdentity } from './initials'

const useStyles = makeStyles({
    drawer: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground2,
        flexShrink: 0,
        height: '100%',
        overflow: 'hidden',
        width: '100%',
    },
    drawerCompact: {
        fontSize: '12px',
    },
    messagesContainer: {
        flex: 1,
        overflow: 'auto',
        paddingTop: '1rem',
        paddingBottom: '1rem',
        paddingLeft: '0.875rem',
        paddingRight: '0.875rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    messagesContainerCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
    },
    thinkingRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        padding: '0.5rem 0.25rem',
    },
    thinkingRowCompact: {
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.125rem',
        paddingRight: '0.125rem',
    },
})

interface ChatDrawerProps {
    projectId?: string
    onClose: () => void
    chatWidth?: number
    maxChatWidth?: number
    onRequestChatWidth?: (nextWidth: number) => void
}

const DEFAULT_GENERATE_MESSAGE =
    'Generate work-items based on provided context. If context is limited, make reasonable assumptions and produce a best-effort initial backlog draft.'

type PendingAttachment = ChatAttachment & {
    isUploading: true
}

function normalizeMessageContent(value: string): string {
    return value.replace(/\s+/g, ' ').trim().toLowerCase()
}

export function ChatDrawer({
    projectId,
    onClose,
    chatWidth,
    maxChatWidth,
    onRequestChatWidth,
}: ChatDrawerProps) {
    const styles = useStyles()
    const queryClient = useQueryClient()
    const [message, setMessage] = useState('')
    const [activeSession, setActiveSession] = useState<string | undefined>(undefined)
    const [optimisticMessages, setOptimisticMessages] = useState<ChatMessageData[]>([])
    const [isThinking, setIsThinking] = useState(false)
    const { isGenerating, setIsGenerating } = useChatGenerating()
    const { user } = useAuth()
    const { preferences } = usePreferences()
    const isCompact = preferences?.compactMode ?? false
    const [lastSendResponse, setLastSendResponse] = useState<SendMessageResponse | null>(null)
    const messagesEndRef = useRef<HTMLDivElement>(null)
    const messagesContainerRef = useRef<HTMLDivElement>(null)

    const { data: chatData, isLoading: loadingChat } = useChatData(projectId)
    const { data: messages } = useChatMessages(projectId, activeSession)
    const createSessionMutation = useCreateChatSession(projectId)
    const deleteSessionMutation = useDeleteSession(projectId)
    const renameSessionMutation = useRenameSession(projectId)
    const { data: attachments } = useAttachments(projectId, activeSession)
    const uploadMutation = useUploadAttachment(projectId)
    const deleteMutation = useDeleteAttachment(projectId, activeSession)
    const [pendingAttachments, setPendingAttachments] = useState<PendingAttachment[]>([])

    const sessions = useMemo(() => chatData?.sessions ?? [], [chatData?.sessions])
    const serverMessages = useMemo(() => messages ?? chatData?.messages ?? [], [messages, chatData?.messages])
    const displayAttachments = useMemo(
        () => [...pendingAttachments, ...(attachments ?? [])],
        [pendingAttachments, attachments]
    )

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
            // Don't clear optimistic here - the displayMessages dedup handles the
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
        // Also deduplicate by content - if the server already has the user message, skip
        const serverUserContents = new Set(
            serverMessages
                .filter(m => m.role === 'user')
                .map(m => normalizeMessageContent(m.content))
        )
        const unique = pending.filter(
            m => m.role !== 'user' || !serverUserContents.has(normalizeMessageContent(m.content)),
        )
        return [...serverMessages, ...unique]
    }, [serverMessages, optimisticMessages])
    const currentUserIdentity = resolveChatUserIdentity(user?.displayName, user?.email)
    const allowGenerateWorkItems = Boolean(projectId)

    // Auto-select/reset active session when scope/session list changes.
    useEffect(() => {
        if (sessions.length === 0) {
            setActiveSession(undefined)
            setPendingAttachments([])
            return
        }

        if (!activeSession || !sessions.some(s => s.id === activeSession)) {
            setActiveSession(sessions[0].id)
        }
    }, [activeSession, sessions])

    // Scroll to bottom when messages change
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }, [displayMessages.length, isThinking])

    useEffect(() => {
        if (!onRequestChatWidth || !chatWidth || !messagesContainerRef.current) {
            return
        }

        const frame = window.requestAnimationFrame(() => {
            const container = messagesContainerRef.current
            if (!container) {
                return
            }

            const overflowCandidates = Array.from(
                container.querySelectorAll<HTMLElement>('[data-chat-role="assistant"] pre, [data-chat-role="assistant"] table')
            )

            let requiredExtraWidth = 0
            for (const element of overflowCandidates) {
                const overflowWidth = element.scrollWidth - element.clientWidth
                if (overflowWidth > requiredExtraWidth) {
                    requiredExtraWidth = overflowWidth
                }
            }

            if (requiredExtraWidth <= 16) {
                return
            }

            const requestedWidth = Math.min(
                maxChatWidth ?? chatWidth,
                chatWidth + requiredExtraWidth + 48,
            )

            if (requestedWidth > chatWidth + 16) {
                onRequestChatWidth(requestedWidth)
            }
        })

        return () => window.cancelAnimationFrame(frame)
    }, [displayMessages, chatWidth, maxChatWidth, onRequestChatWidth])

    const doSend = (sessionId: string, userContent: string, generateWorkItems: boolean) => {
        setMessage('')

        // If generating with no user input, use the default generate message
        const contentToSend = generateWorkItems && !userContent
            ? DEFAULT_GENERATE_MESSAGE
            : userContent

        // Optimistic: show user message immediately
        const displayContent = contentToSend
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
        if (generateWorkItems && !allowGenerateWorkItems) return
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
        deleteSessionMutation.mutate(sessionId, {
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

    const handleRenameSession = (sessionId: string, title: string) => {
        renameSessionMutation.mutate({ sessionId, title })
    }

    // Show tool events from last response
    const toolEvents = lastSendResponse?.toolEvents ?? []

    const handleFileSelect = (file: File) => {
        const resolvedSessionId = activeSession && sessions.some((session) => session.id === activeSession)
            ? activeSession
            : undefined

        const uploadToSession = (sessionId: string) => {
            const optimisticId = `pending-${Date.now()}-${Math.random().toString(36).slice(2)}`
            setPendingAttachments((current) => [
                ...current,
                {
                    id: optimisticId,
                    fileName: file.name,
                    contentLength: file.size,
                    uploadedAt: new Date().toISOString(),
                    isUploading: true,
                },
            ])

            uploadMutation.mutate(
                { sessionId, file },
                {
                    onSuccess: () => {
                        setPendingAttachments((current) => current.filter((attachment) => attachment.id !== optimisticId))
                    },
                    onError: () => {
                        setPendingAttachments((current) => current.filter((attachment) => attachment.id !== optimisticId))
                    },
                },
            )
        }

        if (resolvedSessionId) {
            uploadToSession(resolvedSessionId)
            return
        }

        createSessionMutation.mutate('New Chat', {
            onSuccess: (session) => {
                setActiveSession(session.id)
                uploadToSession(session.id)
            },
        })
    }

    return (
        <div className={mergeClasses(styles.drawer, isCompact && styles.drawerCompact)}>
            <ChatDrawerHeader onClose={onClose} />

            <ChatSessionBar
                sessions={sessions}
                activeSessionId={activeSession ?? ''}
                onSelectSession={(id) => { setActiveSession(id); setOptimisticMessages([]) }}
                onDeleteSession={handleDeleteSession}
                onRenameSession={handleRenameSession}
                onNewSession={handleNewSession}
                actionsDisabled={
                    createSessionMutation.isPending
                    || deleteSessionMutation.isPending
                    || renameSessionMutation.isPending
                }
            />

            {loadingChat ? (
                <div
                    ref={messagesContainerRef}
                    className={mergeClasses(styles.messagesContainer, isCompact && styles.messagesContainerCompact)}
                >
                    <Spinner label="Loading chat..." />
                </div>
            ) : (
                <div
                    ref={messagesContainerRef}
                    className={mergeClasses(styles.messagesContainer, isCompact && styles.messagesContainerCompact)}
                >
                    {displayMessages.map((msg) => (
                        <ChatMessage key={msg.id} message={msg} currentUserIdentity={currentUserIdentity} />
                    ))}
                    {toolEvents.length > 0 && toolEvents.map((evt, i) => (
                        <ToolEventMessage key={`tool-${i}`} event={evt} />
                    ))}
                    {isThinking && (
                        <div className={mergeClasses(styles.thinkingRow, isCompact && styles.thinkingRowCompact)}>
                            <Spinner
                                size="tiny"
                                label={isGenerating
                                    ? 'Fleet AI is generating work items - this may take a while...'
                                    : 'Fleet AI is thinking...'}
                                labelPosition="after"
                            />
                        </div>
                    )}
                    <div ref={messagesEndRef} />
                </div>
            )}
            <AttachedFiles
                attachments={displayAttachments}
                onDelete={(id) => deleteMutation.mutate(id)}
                deleting={deleteMutation.isPending || uploadMutation.isPending}
            />

            <ChatInput
                value={message}
                onChange={setMessage}
                onSend={() => handleSend(false)}
                onGenerate={allowGenerateWorkItems ? () => handleSend(true) : undefined}
                onFileSelect={handleFileSelect}
                allowGenerate={allowGenerateWorkItems}
                disabled={isThinking || createSessionMutation.isPending}
                uploading={uploadMutation.isPending}
            />
        </div>
    )
}

