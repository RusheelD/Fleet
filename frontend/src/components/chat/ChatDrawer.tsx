import { useState, useEffect, useRef, useMemo } from 'react'
import { makeStyles, mergeClasses, Spinner } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatInput, AttachedFiles } from './'
import { ToolEventMessage } from './ToolEventMessage'

import {
    useChatData, useChatMessages, useCreateChatSession,
    useAttachments, useUploadAttachment, useDeleteAttachment, useDeleteSession, useRenameSession,
    cancelChatSessionRequests, sendChatMessage,
} from '../../proxies'
import { useChatGenerating, useAuth, usePreferences, useIsMobile } from '../../hooks'
import { appTokens } from '../../styles/appTokens'
import type { ChatAttachment, ChatMessageData, SendMessageResponse } from '../../models'
import { resolveChatUserIdentity } from './initials'
import { filterPendingOptimisticMessages, reconcileDisplayMessages } from './chatMessageReconciliation'

const useStyles = makeStyles({
    drawer: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: appTokens.color.surfaceAlt,
        flexShrink: 0,
        height: '100%',
        overflow: 'hidden',
        width: '100%',
    },
    drawerCompact: {
        fontSize: '12px',
    },
    drawerMobile: {
        paddingBottom: 'env(safe-area-inset-bottom)',
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
        backgroundColor: appTokens.color.surfaceAlt,
    },
    messagesContainerCompact: {
        paddingTop: '0.5rem',
        paddingBottom: '0.5rem',
        paddingLeft: '0.5rem',
        paddingRight: '0.5rem',
        gap: '0.5rem',
    },
    messagesContainerMobile: {
        paddingTop: '0.625rem',
        paddingBottom: '0.75rem',
        paddingLeft: '0.625rem',
        paddingRight: '0.625rem',
    },
    thinkingRow: {
        display: 'flex',
        alignItems: 'center',
        gap: appTokens.space.sm,
        padding: '0.5rem 0.25rem',
    },
    thinkingRowCompact: {
        paddingTop: '0.25rem',
        paddingBottom: '0.25rem',
        paddingLeft: '0.125rem',
        paddingRight: '0.125rem',
    },
    thinkingRowMobile: {
        paddingLeft: '0.25rem',
        paddingRight: '0.25rem',
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

function buildAttachmentsQueryKey(projectId: string | undefined, sessionId: string) {
    return ['chat-attachments', JSON.stringify([sessionId, projectId])]
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
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const hasConstrainedPaneWidth = !isMobile && typeof chatWidth === 'number' && chatWidth <= 520
    const [lastSendResponse, setLastSendResponse] = useState<SendMessageResponse | null>(null)
    const messagesEndRef = useRef<HTMLDivElement>(null)
    const messagesContainerRef = useRef<HTMLDivElement>(null)
    const deletingSessionIdsRef = useRef<Set<string>>(new Set())

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
    const attachmentsForNextMessage = useMemo(
        () => attachments ?? [],
        [attachments]
    )
    const hasUploadingAttachments = pendingAttachments.length > 0 || uploadMutation.isPending

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

    const displayMessages = useMemo(
        () => reconcileDisplayMessages(serverMessages, optimisticMessages),
        [serverMessages, optimisticMessages],
    )
    const currentUserIdentity = resolveChatUserIdentity(user?.displayName, user?.email)
    const allowGenerateWorkItems = Boolean(projectId)

    useEffect(() => {
        setOptimisticMessages((current) => {
            const next = filterPendingOptimisticMessages(current, serverMessages)
            return next.length === current.length ? current : next
        })
    }, [serverMessages])

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

    const doSend = (
        sessionId: string,
        userContent: string,
        generateWorkItems: boolean,
        messageAttachments: ChatAttachment[],
    ) => {
        setMessage('')

        // If generating with no user input, use the default generate message
        const contentToSend = generateWorkItems && !userContent
            ? DEFAULT_GENERATE_MESSAGE
            : userContent

        const attachmentsQueryKey = buildAttachmentsQueryKey(projectId, sessionId)
        const previousAttachments = queryClient.getQueryData<ChatAttachment[]>(attachmentsQueryKey) ?? []

        // Optimistic: show user message immediately
        const displayContent = contentToSend
        setOptimisticMessages([{
            id: `optimistic-${Date.now()}`,
            role: 'user',
            content: displayContent,
            timestamp: new Date().toISOString(),
            attachments: messageAttachments,
        }])
        setIsThinking(true)
        setIsGenerating(generateWorkItems)
        setLastSendResponse(null)

        if (messageAttachments.length > 0) {
            queryClient.setQueryData<ChatAttachment[]>(
                attachmentsQueryKey,
                (current) => (current ?? []).filter(
                    (attachment) => !messageAttachments.some((sentAttachment) => sentAttachment.id === attachment.id),
                ),
            )
        }

        // Call API directly with the explicit sessionId to avoid stale closures
        // when a session was just auto-created
        sendChatMessage(projectId, sessionId, contentToSend, generateWorkItems)
            .then(async (response: SendMessageResponse) => {
                setLastSendResponse(response)
                await queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-data'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
                if (!response.isDeferred) {
                    setIsThinking(false)
                    setIsGenerating(false)
                }
                if (generateWorkItems && !response.isDeferred) {
                    void queryClient.invalidateQueries({ queryKey: ['work-items'] })
                }
            })
            .catch(() => {
                if (!deletingSessionIdsRef.current.has(sessionId)) {
                    queryClient.setQueryData<ChatAttachment[]>(attachmentsQueryKey, previousAttachments)
                    void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
                }
                setIsThinking(false)
                setIsGenerating(false)
                setOptimisticMessages([])
            })
    }

    const handleSend = (generateWorkItems = false) => {
        const userContent = message.trim()
        if (isThinking || createSessionMutation.isPending) return
        if (hasUploadingAttachments) return
        if (generateWorkItems && !allowGenerateWorkItems) return
        if (!userContent && !generateWorkItems) return

        const messageAttachments = attachmentsForNextMessage

        // Auto-create a session if none exists, then send
        if (!activeSession) {
            createSessionMutation.mutate('New Chat', {
                onSuccess: (session) => {
                    setActiveSession(session.id)
                    doSend(session.id, userContent, generateWorkItems, messageAttachments)
                },
            })
            return
        }

        doSend(activeSession, userContent, generateWorkItems, messageAttachments)
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
        deletingSessionIdsRef.current.add(sessionId)
        cancelChatSessionRequests(projectId, sessionId)

        if (activeSession === sessionId) {
            setIsThinking(false)
            setIsGenerating(false)
            setLastSendResponse(null)
            setOptimisticMessages([])
        }

        deleteSessionMutation.mutate(sessionId, {
            onSuccess: () => {
                deletingSessionIdsRef.current.delete(sessionId)
                // If deleted session was active, switch to first remaining session or clear
                if (activeSession === sessionId) {
                    const remainingSessions = sessions.filter(s => s.id !== sessionId)
                    setActiveSession(remainingSessions.length > 0 ? remainingSessions[0].id : undefined)
                }
                setOptimisticMessages([])
            },
            onError: () => {
                deletingSessionIdsRef.current.delete(sessionId)
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
        <div className={mergeClasses(styles.drawer, isCompact && styles.drawerCompact, isMobile && styles.drawerMobile)}>
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
                    className={mergeClasses(
                        styles.messagesContainer,
                        isCompact && styles.messagesContainerCompact,
                        isMobile && styles.messagesContainerMobile,
                    )}
                >
                    <Spinner label="Loading chat..." />
                </div>
            ) : (
                <div
                    ref={messagesContainerRef}
                    className={mergeClasses(
                        styles.messagesContainer,
                        isCompact && styles.messagesContainerCompact,
                        isMobile && styles.messagesContainerMobile,
                    )}
                >
                    {displayMessages.map((msg) => (
                        <ChatMessage key={msg.id} message={msg} currentUserIdentity={currentUserIdentity} />
                    ))}
                    {toolEvents.length > 0 && toolEvents.map((evt, i) => (
                        <ToolEventMessage key={`tool-${i}`} event={evt} />
                    ))}
                    {isThinking && (
                        <div
                            className={mergeClasses(
                                styles.thinkingRow,
                                isCompact && styles.thinkingRowCompact,
                                isMobile && styles.thinkingRowMobile,
                            )}
                        >
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
                forceStackedLayout={hasConstrainedPaneWidth}
                disabled={isThinking || createSessionMutation.isPending || hasUploadingAttachments}
                uploading={uploadMutation.isPending}
            />
        </div>
    )
}

