import { useState, useEffect, useRef, useMemo } from 'react'
import {
    Badge,
    Dropdown,
    Field,
    Input,
    Option,
    Switch,
    makeStyles,
    mergeClasses,
    Spinner,
} from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'

import { ChatDrawerHeader, ChatSessionBar, ChatMessage, ChatThinkingGroup, ChatInput, AttachedFiles } from './'

import {
    useChatData, useChatMessages, useCreateChatSession,
    useAttachments, useUploadAttachment, useDeleteAttachment, useDeleteSession, useRenameSession, useCancelChatGeneration,
    useUpdateSessionDynamicIteration,
} from '../../proxies/dataClient'
import { cancelChatSessionRequests, sendChatMessage } from '../../proxies/chatProxy'
import { getApiErrorMessage } from '../../proxies/proxy'
import { useAuth } from '../../hooks/useAuthHook'
import { usePreferences } from '../../hooks/PreferencesContext'
import { useIsMobile } from '../../hooks/useIsMobile'
import { useServerEventConnection } from '../../hooks/useServerEvents'
import { resolveConnectionAwarePollingInterval } from '../../hooks/serverEventConnectionState'
import { appTokens } from '../../styles/appTokens'
import type {
    ChatAttachment,
    ChatDynamicOptions,
    ChatDynamicStrategy,
    ChatGenerationState,
    ChatMessageData,
    ChatSessionData,
} from '../../models'
import { normalizeChatSessionActivities } from '../../models/chat'
import { resolveChatUserIdentity } from './initials'
import { filterPendingOptimisticMessages, reconcileDisplayMessages } from './chatMessageReconciliation'
import { buildChatTimeline } from './chatTimeline'
import { resolveContentToSend, applySessionOptimisticState, canSubmitChatMessage } from './chatDrawerHelpers'

const useStyles = makeStyles({
    drawer: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: appTokens.color.pageBackground,
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
        backgroundColor: appTokens.color.pageBackground,
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
    generationOptionsPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: appTokens.space.sm,
        paddingTop: appTokens.space.sm,
        paddingBottom: appTokens.space.sm,
        paddingLeft: appTokens.space.md,
        paddingRight: appTokens.space.md,
        borderTop: appTokens.border.subtle,
        backgroundColor: appTokens.color.surfaceAlt,
    },
    generationOptionsPanelCompact: {
        paddingTop: appTokens.space.xs,
        paddingBottom: appTokens.space.xs,
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
        gap: appTokens.space.xs,
    },
    generationOptionsPanelMobile: {
        paddingLeft: appTokens.space.sm,
        paddingRight: appTokens.space.sm,
    },
    generationOptionsRow: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: appTokens.space.sm,
    },
    generationOptionsRowStacked: {
        gridTemplateColumns: '1fr',
    },
    policyIndicators: {
        display: 'flex',
        gap: appTokens.space.xs,
        flexWrap: 'wrap',
        alignItems: 'center',
    },
})

interface ChatDrawerProps {
    projectId?: string
    onClose: () => void
    chatWidth?: number
    maxChatWidth?: number
    onRequestChatWidth?: (nextWidth: number) => void
}

const BUSY_CHAT_FALLBACK_POLL_MS = 4000
const IDLE_CHAT_FALLBACK_POLL_MS = 8000
const DYNAMIC_STRATEGIES: Array<{ value: ChatDynamicStrategy; label: string }> = [
    { value: 'balanced', label: 'Balanced' },
    { value: 'parallel', label: 'Parallel' },
    { value: 'sequential', label: 'Sequential' },
]

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
    const { state: serverEventState } = useServerEventConnection(projectId)
    const isMobile = useIsMobile()
    const isCompact = preferences?.compactMode ?? false
    const hasConstrainedPaneWidth = !isMobile && typeof chatWidth === 'number' && chatWidth <= 520
    const messagesEndRef = useRef<HTMLDivElement>(null)
    const messagesContainerRef = useRef<HTMLDivElement>(null)
    const deletingSessionIdsRef = useRef<Set<string>>(new Set())
    const chatFallbackPollingInterval =
        pendingMessageSessionIds.length > 0 || optimisticGeneratingSessionIds.length > 0
            ? BUSY_CHAT_FALLBACK_POLL_MS
            : IDLE_CHAT_FALLBACK_POLL_MS
    const chatPollingInterval = resolveConnectionAwarePollingInterval(serverEventState, chatFallbackPollingInterval, chatFallbackPollingInterval)
    const { data: chatData, isLoading: loadingChat, isError: chatDataIsError, error: chatDataError } = useChatData(projectId, {
        pollingInterval: chatPollingInterval,
    })
    const { data: messages } = useChatMessages(projectId, activeSession, {
        pollingInterval: chatPollingInterval,
    })
    const createSessionMutation = useCreateChatSession(projectId)
    const deleteSessionMutation = useDeleteSession(projectId)
    const renameSessionMutation = useRenameSession(projectId)
    const updateDynamicIterationMutation = useUpdateSessionDynamicIteration(projectId)
    const cancelGenerationMutation = useCancelChatGeneration(projectId)
    const { data: attachments } = useAttachments(projectId, activeSession)
    const uploadMutation = useUploadAttachment(projectId)
    const deleteMutation = useDeleteAttachment(projectId, activeSession)
    const [pendingAttachments, setPendingAttachments] = useState<PendingAttachment[]>([])
    const [dynamicIterationEnabled, setDynamicIterationEnabled] = useState(false)
    const [dynamicBranchName, setDynamicBranchName] = useState('')
    const [dynamicStrategy, setDynamicStrategy] = useState<ChatDynamicStrategy>('balanced')

    const serverSessions = useMemo(() => chatData?.sessions ?? [], [chatData?.sessions])
    const sessions = useMemo(
        () => serverSessions.map((session) => {
            const isCancelingSession = Boolean(
                cancelGenerationMutation.isPending
                && cancelGenerationMutation.variables === session.id,
            )
            const optimisticSession = applySessionOptimisticState(session, {
                optimisticGeneratingSessionIds,
                isCancelingSession,
                isDynamicIterationSession: Boolean(resolveSessionDynamicOptions(session)?.enabled),
            })

            return {
                ...optimisticSession,
                recentActivity: normalizeChatSessionActivities(session.recentActivity),
            }
        }),
        [serverSessions, optimisticGeneratingSessionIds, cancelGenerationMutation.isPending, cancelGenerationMutation.variables],
    )
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
    const activeSessionDynamicOptions = useMemo(
        () => resolveSessionDynamicOptions(activeSessionData),
        [activeSessionData],
    )
    const activeSessionIsSending = Boolean(activeSession && pendingMessageSessionIds.includes(activeSession))
    const activeSessionIsGenerating = Boolean(activeSession && activeSessionData?.isGenerating)
    const activeSessionIsBusy = activeSessionIsSending || activeSessionIsGenerating
    const recentActivityCount = activeSessionData?.recentActivity?.length ?? 0
    const isCancelingActiveSession = Boolean(
        activeSession
        && cancelGenerationMutation.isPending
        && cancelGenerationMutation.variables === activeSession
    )

    useEffect(() => {
        const options = activeSessionDynamicOptions
        setDynamicIterationEnabled(Boolean(options?.enabled))
        setDynamicBranchName(options?.branchName ?? '')
        setDynamicStrategy(options?.strategy ?? 'balanced')
    }, [activeSessionDynamicOptions])

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
        if (serverSessions.length === 0) {
            return
        }

        setOptimisticGeneratingSessionIds((current) => current.filter((sessionId) => {
            const serverSession = serverSessions.find((candidate) => candidate.id === sessionId)
            if (!serverSession) {
                return false
            }

            return !serverSession.isGenerating
                && serverSession.generationState === 'idle'
                && !serverSession.generationUpdatedAtUtc
        }))
    }, [serverSessions])

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
    }, [displayMessages.length, recentActivityCount, activeSessionIsBusy])

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
        nextDynamicOptions?: ChatDynamicOptions,
    ) => {
        const isDynamicIterationSend = Boolean(nextDynamicOptions?.enabled)
        const contentToSend = resolveContentToSend(userContent, generateWorkItems, isDynamicIterationSend)
        setMessage('')

        const attachmentsQueryKey = buildAttachmentsQueryKey(projectId, sessionId)
        const previousAttachments = queryClient.getQueryData<ChatAttachment[]>(attachmentsQueryKey) ?? []

        setOptimisticMessages([createOptimisticUserMessage(contentToSend, messageAttachments)])
        setPendingMessageSessionIds((current) => addSessionId(current, sessionId))
        if (generateWorkItems) {
            setOptimisticGeneratingSessionIds((current) => addSessionId(current, sessionId))
        }

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
        sendChatMessage(projectId, sessionId, {
            content: contentToSend,
            generateWorkItems,
            dynamicOptions: nextDynamicOptions,
        })
            .then(async (response) => {
                await queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-data'] })
                await queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
                setPendingMessageSessionIds((current) => removeSessionId(current, sessionId))
                if (!response.isDeferred) {
                    setOptimisticGeneratingSessionIds((current) => removeSessionId(current, sessionId))
                }
                if (generateWorkItems && !response.isDeferred) {
                    void queryClient.invalidateQueries({ queryKey: ['work-items'] })
                    void queryClient.invalidateQueries({ queryKey: ['executions'] })
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
        const isDynamicIterationSend = generateWorkItems && dynamicIterationEnabled
        if (messageActionsDisabled) return
        if (hasUploadingAttachments) return
        if (generateWorkItems && !allowGenerateWorkItems) return
        if (!canSubmitChatMessage(userContent, generateWorkItems, isDynamicIterationSend)) return

        const messageAttachments = attachmentsForNextMessage
        const nextDynamicOptions = generateWorkItems && allowGenerateWorkItems
            ? dynamicOptions
            : undefined
        const contentToSend = resolveContentToSend(userContent, generateWorkItems, Boolean(nextDynamicOptions?.enabled))

        // Auto-create a session if none exists, then send
        if (!activeSession) {
            setMessage('')
            setOptimisticMessages([createOptimisticUserMessage(contentToSend, messageAttachments)])
            createSessionMutation.mutate('New Chat', {
                onSuccess: (session) => {
                    setActiveSession(session.id)
                    doSend(session.id, userContent, generateWorkItems, messageAttachments, nextDynamicOptions)
                },
                onError: (error: unknown) => {
                    setMessage(userContent)
                    setOptimisticMessages((current) => [
                        ...current.filter((entry) => entry.role === 'user'),
                        {
                            id: `chat-create-error-${Date.now()}`,
                            role: 'assistant',
                            content: getApiErrorMessage(error, 'Failed to create a chat session.'),
                            timestamp: new Date().toISOString(),
                        },
                    ])
                },
            })
            return
        }

        doSend(activeSession, userContent, generateWorkItems, messageAttachments, nextDynamicOptions)
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
            setOptimisticMessages([])
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
            },
            onError: () => {
                deletingSessionIdsRef.current.delete(sessionId)
            },
        })
    }

    const handleRenameSession = (sessionId: string, title: string) => {
        renameSessionMutation.mutate({ sessionId, title })
    }

    const persistDynamicIterationOptions = (
        isEnabled: boolean,
        branchName: string,
        strategy: ChatDynamicStrategy,
    ) => {
        if (!activeSession || !allowGenerateWorkItems) {
            return
        }

        const normalizedBranch = branchName.trim()
        updateDynamicIterationMutation.mutate({
            sessionId: activeSession,
            data: {
                isDynamicIterationEnabled: isEnabled,
                dynamicIterationBranch: isEnabled && normalizedBranch.length > 0 ? normalizedBranch : null,
                dynamicIterationPolicyJson: isEnabled ? JSON.stringify({ executionPolicy: strategy }) : null,
            },
        })
    }

    const handleCancelGeneration = (sessionId?: string) => {
        const resolvedSessionId = sessionId ?? activeSession
        if (!resolvedSessionId) {
            return
        }

        cancelGenerationMutation.mutate(resolvedSessionId, {
            onSuccess: () => {
                setPendingMessageSessionIds((current) => removeSessionId(current, resolvedSessionId))
                setOptimisticGeneratingSessionIds((current) => removeSessionId(current, resolvedSessionId))
            },
        })
    }

    const activeSessionStatusState = activeSessionData?.generationState ?? 'idle'
    const activeSessionStatusMessage = getVisibleSessionStatus(activeSessionData, Boolean(activeSessionDynamicOptions?.enabled))
    const visibleActivity = useMemo(() => {
        return activeSessionData?.recentActivity ?? []
    }, [activeSessionData?.recentActivity])
    const dynamicOptions = useMemo<ChatDynamicOptions>(
        () => ({
            enabled: dynamicIterationEnabled,
            branchName: dynamicBranchName.trim().length > 0 ? dynamicBranchName.trim() : null,
            strategy: dynamicIterationEnabled ? dynamicStrategy : null,
        }),
        [dynamicIterationEnabled, dynamicBranchName, dynamicStrategy],
    )
    const dynamicIterationActive = allowGenerateWorkItems && dynamicIterationEnabled
    const messageActionsDisabled = activeSessionIsBusy || createSessionMutation.isPending || isCancelingActiveSession
    const dynamicSettingsDisabled = messageActionsDisabled || updateDynamicIterationMutation.isPending
    const activeDynamicPolicy = activeSessionData?.dynamicPolicy
        ?? parseDynamicPolicy(activeSessionData?.dynamicIterationPolicyJson)
    const policyBadges = useMemo(() => {
        const autoStartLimit = activeDynamicPolicy?.autoStartLimit
        if (typeof autoStartLimit !== 'number' || autoStartLimit <= 0) {
            return []
        }

        return [`Auto-start limit ${autoStartLimit}`]
    }, [activeDynamicPolicy?.autoStartLimit])
    const timelineItems = useMemo(
        () => buildChatTimeline(displayMessages, visibleActivity, {
            isBusy: activeSessionIsBusy,
            statusMessage: activeSessionStatusMessage
                ?? (activeSessionIsGenerating
                    ? dynamicIterationActive
                        ? 'Iterating on requested changes...'
                        : 'Generating work items...'
                    : 'Fleet AI is thinking...'),
        }),
        [displayMessages, visibleActivity, activeSessionIsBusy, activeSessionStatusMessage, activeSessionIsGenerating, dynamicIterationActive],
    )
    const showLoadingChat = loadingChat && displayMessages.length === 0
    const chatLoadErrorMessage = chatDataIsError
        ? getApiErrorMessage(chatDataError, 'Failed to load chat.')
        : null

    const handleFileSelect = (files: File[]) => {
        if (files.length === 0) {
            return
        }

        const resolvedSessionId = activeSession && sessions.some((session) => session.id === activeSession)
            ? activeSession
            : undefined

        const uploadToSession = (sessionId: string) => {
            files.forEach((file, index) => {
                const optimisticId = `pending-${Date.now()}-${index}-${Math.random().toString(36).slice(2)}`
                setPendingAttachments((current) => [
                    ...current,
                    {
                        id: optimisticId,
                        fileName: file.name,
                        contentLength: file.size,
                        uploadedAt: new Date().toISOString(),
                        contentType: file.type || 'application/octet-stream',
                        contentUrl: '',
                        markdownReference: '',
                        isImage: file.type.startsWith('image/'),
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
            })
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
                onCancelGeneration={handleCancelGeneration}
                isCancelingSession={(sessionId) =>
                    cancelGenerationMutation.isPending && cancelGenerationMutation.variables === sessionId
                }
                onNewSession={handleNewSession}
                actionsDisabled={
                    createSessionMutation.isPending
                    || deleteSessionMutation.isPending
                    || renameSessionMutation.isPending
                    || activeSessionIsBusy
                    || createSessionMutation.isPending
                    || isCancelingActiveSession
                }
            />

            {showLoadingChat ? (
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
                    {chatLoadErrorMessage && displayMessages.length === 0 && (
                        <ChatMessage
                            message={{
                                id: 'chat-load-error',
                                role: 'assistant',
                                content: chatLoadErrorMessage,
                                timestamp: new Date().toISOString(),
                            }}
                            currentUserIdentity={currentUserIdentity}
                        />
                    )}
                    {timelineItems.map((item) => item.type === 'message' ? (
                        <ChatMessage
                            key={item.id}
                            message={item.message}
                            currentUserIdentity={currentUserIdentity}
                        />
                    ) : (
                        <ChatThinkingGroup
                            key={item.id}
                            group={item.group}
                        />
                    ))}
                    <div ref={messagesEndRef} />
                </div>
            )}
            {allowGenerateWorkItems && (
                <div
                    className={mergeClasses(
                        styles.generationOptionsPanel,
                        isCompact && styles.generationOptionsPanelCompact,
                        isMobile && styles.generationOptionsPanelMobile,
                    )}
                >
                    <Switch
                        label="Dynamic iteration"
                        checked={dynamicIterationEnabled}
                        onChange={(_event, data) => {
                            const isEnabled = Boolean(data.checked)
                            setDynamicIterationEnabled(isEnabled)
                            persistDynamicIterationOptions(isEnabled, dynamicBranchName, dynamicStrategy)
                        }}
                        disabled={dynamicSettingsDisabled}
                    />
                    {dynamicIterationEnabled && (
                        <div className={mergeClasses(
                            styles.generationOptionsRow,
                            (isMobile || hasConstrainedPaneWidth) && styles.generationOptionsRowStacked,
                        )}
                        >
                            <Field label="Branch override (optional)">
                                <Input
                                    value={dynamicBranchName}
                                    onChange={(_event, data) => setDynamicBranchName(data.value)}
                                    onBlur={() => persistDynamicIterationOptions(dynamicIterationEnabled, dynamicBranchName, dynamicStrategy)}
                                    placeholder="main"
                                    disabled={dynamicSettingsDisabled}
                                />
                            </Field>
                            <Field label="Dispatch strategy">
                                <Dropdown
                                    value={DYNAMIC_STRATEGIES.find((option) => option.value === dynamicStrategy)?.label ?? 'Balanced'}
                                    selectedOptions={[dynamicStrategy]}
                                    onOptionSelect={(_event, data) => {
                                        const selected = data.optionValue
                                        if (selected === 'balanced' || selected === 'parallel' || selected === 'sequential') {
                                            setDynamicStrategy(selected)
                                            persistDynamicIterationOptions(dynamicIterationEnabled, dynamicBranchName, selected)
                                        }
                                    }}
                                    disabled={dynamicSettingsDisabled}
                                >
                                    {DYNAMIC_STRATEGIES.map((option) => (
                                        <Option key={option.value} value={option.value}>
                                            {option.label}
                                        </Option>
                                    ))}
                                </Dropdown>
                            </Field>
                        </div>
                    )}
                    {policyBadges.length > 0 && (
                        <div className={styles.policyIndicators}>
                            {policyBadges.map((badge) => (
                                <Badge key={badge} appearance="tint">
                                    {badge}
                                </Badge>
                            ))}
                        </div>
                    )}
                </div>
            )}
            <AttachedFiles
                attachments={displayAttachments}
                onDelete={(id) => deleteMutation.mutate(id)}
                deleting={deleteMutation.isPending || uploadMutation.isPending || messageActionsDisabled}
            />

            <ChatInput
                value={message}
                onChange={setMessage}
                onSend={() => handleSend(dynamicIterationActive)}
                onGenerate={allowGenerateWorkItems ? () => handleSend(true) : undefined}
                onCancelGeneration={activeSessionIsGenerating ? () => handleCancelGeneration(activeSession) : undefined}
                onFileSelect={handleFileSelect}
                allowGenerate={allowGenerateWorkItems}
                forceStackedLayout={hasConstrainedPaneWidth}
                disabled={messageActionsDisabled || hasUploadingAttachments}
                uploading={uploadMutation.isPending}
                isGenerating={activeSessionIsGenerating}
                canceling={isCancelingActiveSession}
                statusMessage={activeSessionStatusMessage}
                statusState={activeSessionStatusState}
                dynamicIterationActive={dynamicIterationActive}
            />
        </div>
    )
}

function createOptimisticUserMessage(content: string, attachments: ChatAttachment[]): ChatMessageData {
    return {
        id: `optimistic-${Date.now()}`,
        role: 'user',
        content,
        timestamp: new Date().toISOString(),
        attachments,
    }
}

function resolveSessionDynamicOptions(session: ChatSessionData | undefined): ChatDynamicOptions | null {
    if (!session) {
        return null
    }

    if (session.dynamicOptions) {
        return session.dynamicOptions
    }

    const policy = parseDynamicPolicy(session.dynamicIterationPolicyJson)
    const strategy = normalizeDynamicStrategy(policy?.executionPolicy)
        ?? normalizeDynamicStrategy(policy?.strategy)
        ?? 'balanced'
    return {
        enabled: session.isDynamicIterationEnabled,
        branchName: session.dynamicIterationBranch,
        strategy,
    }
}

function parseDynamicPolicy(policyJson: string | null | undefined) {
    if (!policyJson) {
        return null
    }

    try {
        return JSON.parse(policyJson) as {
            executionPolicy?: ChatDynamicStrategy | null
            strategy?: ChatDynamicStrategy | null
            autoStartLimit?: number | null
        }
    } catch {
        return null
    }
}

function normalizeDynamicStrategy(value: unknown): ChatDynamicStrategy | null {
    return value === 'balanced' || value === 'parallel' || value === 'sequential'
        ? value
        : null
}

function getVisibleSessionStatus(
    session: {
        isGenerating: boolean
        generationState: ChatGenerationState
        generationStatus: string | null
    } | undefined,
    isDynamicIterationSession = false,
): string | null {
    if (!session) {
        return null
    }

    if (session.isGenerating) {
        return session.generationStatus
            ?? (isDynamicIterationSession ? 'Iterating on requested changes...' : 'Generating work items...')
    }

    switch (session.generationState) {
        case 'canceling':
            return session.generationStatus ?? 'Canceling generation...'
        case 'failed':
        case 'canceled':
        case 'interrupted':
            return session.generationStatus
        default:
            return null
    }
}
