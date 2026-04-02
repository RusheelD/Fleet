import { useState, useEffect, useRef, useMemo } from 'react'
import { makeStyles, mergeClasses, Spinner } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatInput, AttachedFiles } from './'
import { ToolEventMessage } from './ToolEventMessage'

import {
    useChatData, useChatMessages, useCreateChatSession,
    useAttachments, useUploadAttachment, useDeleteAttachment, useDeleteSession, useRenameSession, useCancelChatGeneration,
    cancelChatSessionRequests, sendChatMessage, getApiErrorMessage,
} from '../../proxies'
import { useAuth, usePreferences, useIsMobile } from '../../hooks'
import { CHAT_TOOL_EVENT_WINDOW_EVENT, type ChatToolEventPayload } from '../../hooks/useServerEvents'
import { appTokens } from '../../styles/appTokens'
import type { ChatAttachment, ChatMessageData, SendMessageResponse, ToolEvent } from '../../models'
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

function addSessionId(current: string[], sessionId: string): string[] {
    return current.includes(sessionId) ? current : [...current, sessionId]
}

function removeSessionId(current: string[], sessionId: string): string[] {
    return current.filter((candidate) => candidate !== sessionId)
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
    const [pendingMessageSessionIds, setPendingMessageSessionIds] = useState<string[]>([])
    const [optimisticGeneratingSessionIds, setOptimisticGeneratingSessionIds] = useState<string[]>([])
    const { user } = useAuth()
    const { preferences } = usePreferences()
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const hasConstrainedPaneWidth = !isMobile && typeof chatWidth === 'number' && chatWidth <= 520
    const [lastSendResponse, setLastSendResponse] = useState<SendMessageResponse | null>(null)
    const [liveToolEvents, setLiveToolEvents] = useState<ToolEvent[]>([])
    const messagesEndRef = useRef<HTMLDivElement>(null)
    const messagesContainerRef = useRef<HTMLDivElement>(null)
    const deletingSessionIdsRef = useRef<Set<string>>(new Set())

    const { data: chatData, isLoading: loadingChat } = useChatData(projectId)
    const { data: messages } = useChatMessages(projectId, activeSession)
    const createSessionMutation = useCreateChatSession(projectId)
    const deleteSessionMutation = useDeleteSession(projectId)
    const renameSessionMutation = useRenameSession(projectId)
    const cancelGenerationMutation = useCancelChatGeneration(projectId)
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

    const activeSessionData = useMemo(
        () => sessions.find(s => s.id === activeSession),
        [sessions, activeSession]
    )
    const activeSessionIsSending = Boolean(activeSession && pendingMessageSessionIds.includes(activeSession))
    const activeSessionIsGenerating = Boolean(
        activeSession
        && (
            activeSessionData?.isGenerating
            || optimisticGeneratingSessionIds.includes(activeSession)
        )
    )
    const activeSessionIsBusy = activeSessionIsSending || activeSessionIsGenerating
    const isCancelingActiveSession = Boolean(
        activeSession
        && cancelGenerationMutation.isPending
        && cancelGenerationMutation.variables === activeSession
    )

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

    useEffect(() => {
        const sessionIds = new Set(sessions.map((session) => session.id))
        setPendingMessageSessionIds((current) => {
            const next = current.filter((sessionId) => sessionIds.has(sessionId))
            return next.length === current.length ? current : next
        })
        setOptimisticGeneratingSessionIds((current) => {
            const next = current.filter((sessionId) => sessionIds.has(sessionId))
            return next.length === current.length ? current : next
        })
    }, [sessions])

    useEffect(() => {
        const handleToolEvent = (event: Event) => {
            const detail = (event as CustomEvent<ChatToolEventPayload>).detail
            if (!detail || !activeSession || detail.sessionId !== activeSession) {
                return
            }

            const detailProjectId = detail.projectId ?? undefined
            const normalizedProjectId = projectId ?? undefined
            if (detailProjectId !== normalizedProjectId) {
                return
            }

            setLiveToolEvents((current) => [
                ...current,
                {
                    toolName: detail.toolName,
                    argumentsJson: detail.argumentsJson,
                    result: detail.result,
                },
            ])
        }

        window.addEventListener(CHAT_TOOL_EVENT_WINDOW_EVENT, handleToolEvent as EventListener)
        return () => {
            window.removeEventListener(CHAT_TOOL_EVENT_WINDOW_EVENT, handleToolEvent as EventListener)
        }
    }, [activeSession, projectId])

    // Auto-select/reset active session when scope/session list changes.
    useEffect(() => {
        if (sessions.length === 0) {
            setActiveSession(undefined)
            setPendingAttachments([])
            setLiveToolEvents([])
            return
        }

        if (!activeSession || !sessions.some(s => s.id === activeSession)) {
            setActiveSession(sessions[0].id)
        }
    }, [activeSession, sessions])

    // Scroll to bottom when messages change
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }, [displayMessages.length, activeSessionIsBusy])

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
        setPendingMessageSessionIds((current) => addSessionId(current, sessionId))
        if (generateWorkItems) {
            setOptimisticGeneratingSessionIds((current) => addSessionId(current, sessionId))
        }
        setLastSendResponse(null)
        setLiveToolEvents([])

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
                setPendingMessageSessionIds((current) => removeSessionId(current, sessionId))
                setOptimisticGeneratingSessionIds((current) => removeSessionId(current, sessionId))
                if (!response.isDeferred) {
                    setLiveToolEvents([])
                }
                if (generateWorkItems && !response.isDeferred) {
                    void queryClient.invalidateQueries({ queryKey: ['work-items'] })
                }
            })
            .catch(async (error: unknown) => {
                if (!deletingSessionIdsRef.current.has(sessionId)) {
                    queryClient.setQueryData<ChatAttachment[]>(attachmentsQueryKey, previousAttachments)
                    void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
                }

                const errorMessage = getApiErrorMessage(
                    error,
                    generateWorkItems ? 'Failed to generate work items.' : 'Failed to send message.',
                )

                setPendingMessageSessionIds((current) => removeSessionId(current, sessionId))
                setOptimisticGeneratingSessionIds((current) => removeSessionId(current, sessionId))
                setLastSendResponse(null)
                setOptimisticMessages((current) => [
                    ...current.filter((entry) => entry.role === 'user'),
                    {
                        id: `chat-error-${Date.now()}`,
                        role: 'assistant',
                        content: errorMessage,
                        timestamp: new Date().toISOString(),
                    },
                ])

                await queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-data'] })
            })
    }

    const handleSend = (generateWorkItems = false) => {
        const userContent = message.trim()
        if (activeSessionIsBusy || createSessionMutation.isPending || isCancelingActiveSession) return
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
                setLiveToolEvents([])
            },
        })
    }

    const handleDeleteSession = (sessionId: string) => {
        deletingSessionIdsRef.current.add(sessionId)
        cancelChatSessionRequests(projectId, sessionId)

        if (activeSession === sessionId) {
            setLastSendResponse(null)
            setOptimisticMessages([])
            setLiveToolEvents([])
        }

        setPendingMessageSessionIds((current) => removeSessionId(current, sessionId))
        setOptimisticGeneratingSessionIds((current) => removeSessionId(current, sessionId))

        deleteSessionMutation.mutate(sessionId, {
            onSuccess: () => {
                deletingSessionIdsRef.current.delete(sessionId)
                // If deleted session was active, switch to first remaining session or clear
                if (activeSession === sessionId) {
                    const remainingSessions = sessions.filter(s => s.id !== sessionId)
                    setActiveSession(remainingSessions.length > 0 ? remainingSessions[0].id : undefined)
                }
                setOptimisticMessages([])
                setLiveToolEvents([])
            },
            onError: () => {
                deletingSessionIdsRef.current.delete(sessionId)
            },
        })
    }

    const handleRenameSession = (sessionId: string, title: string) => {
        renameSessionMutation.mutate({ sessionId, title })
    }

    const handleCancelGeneration = (sessionId?: string) => {
        const resolvedSessionId = sessionId ?? activeSession
        if (!resolvedSessionId) {
            return
        }

        cancelGenerationMutation.mutate(resolvedSessionId, {
            onSuccess: () => {
                setLastSendResponse(null)
                setLiveToolEvents([])
                setPendingMessageSessionIds((current) => removeSessionId(current, resolvedSessionId))
                setOptimisticGeneratingSessionIds((current) => removeSessionId(current, resolvedSessionId))
            },
        })
    }

    // Show tool events from last response
    const toolEvents = liveToolEvents.length > 0
        ? liveToolEvents
        : (lastSendResponse?.toolEvents ?? [])

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
            <ChatDrawerHeader
                onClose={onClose}
                isGenerating={activeSessionIsGenerating}
                isCanceling={isCancelingActiveSession}
                onCancelGeneration={activeSessionIsGenerating ? () => handleCancelGeneration(activeSession) : undefined}
            />

            <ChatSessionBar
                sessions={sessions}
                activeSessionId={activeSession ?? ''}
                onSelectSession={(id) => { setActiveSession(id); setOptimisticMessages([]); setLiveToolEvents([]) }}
                onDeleteSession={handleDeleteSession}
                onRenameSession={handleRenameSession}
                onCancelGeneration={handleCancelGeneration}
                isCancelingSession={(sessionId) =>
                    cancelGenerationMutation.isPending && cancelGenerationMutation.variables === sessionId
                }
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
                    {activeSessionIsBusy && (
                        <div
                            className={mergeClasses(
                                styles.thinkingRow,
                                isCompact && styles.thinkingRowCompact,
                                isMobile && styles.thinkingRowMobile,
                            )}
                        >
                            <Spinner
                                size="tiny"
                                label={activeSessionIsGenerating
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
                disabled={activeSessionIsBusy || createSessionMutation.isPending || hasUploadingAttachments || isCancelingActiveSession}
                uploading={uploadMutation.isPending}
            />
        </div>
    )
}

