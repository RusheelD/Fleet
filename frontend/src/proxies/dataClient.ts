import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { useMemo } from 'react'
import {
  getProjects, getProjectDashboard, getProjectDashboardBySlug, createProject, updateProject, deleteProject, checkSlug,
  getWorkItems, createWorkItem, updateWorkItem, bulkUpdateWorkItems, deleteWorkItem,
  getWorkItemLevels, createWorkItemLevel, updateWorkItemLevel, deleteWorkItemLevel,
  getExecutions, getLogs, clearLogs, startExecution, cancelExecution, pauseExecution, retryExecution, getExecutionDocumentation,
  getChatData, getMessages, createChatSession, sendChatMessage,
  getAttachments, uploadAttachment, deleteAttachment, deleteChatSession,
  search,
  getSubscription,
  getUserSettings, updateProfile, updatePreferences, linkGitHub, unlinkGitHub, getGitHubRepos,
  getNotifications, markNotificationAsRead, markAllNotificationsAsRead,
} from './'
import type {
  CreateProjectRequest, UpdateProjectRequest,
  CreateWorkItemRequest, UpdateWorkItemRequest,
  CreateWorkItemLevelRequest, UpdateWorkItemLevelRequest,
} from './'
import type { UserProfile, UserPreferences } from '../models'

const FIVE_MINUTES_IN_MILLISECONDS = 1000 * 60 * 5
const WORK_ITEMS_POLL_MS = 15000
const EXECUTIONS_POLL_MS = 10000
const LOGS_POLL_MS = 10000
const CHAT_DATA_POLL_MS = 10000
const CHAT_MESSAGES_POLL_MS = 5000

export type DataResult<T> = Pick<UseQueryResult<T>, 'data' | 'error' | 'isError' | 'isLoading' | 'status'>

// ── Core Hook ─────────────────────────────────────────────

export const useDataQuery = <TData>(
  queryName: string,
  queryFunction: () => TData | Promise<TData>,
  requiredParams: (unknown | undefined)[],
  optionalParams?: (unknown | undefined)[],
  queryOptions?: {
    useCachedDataAsPlaceholder?: boolean
    staleTime?: number
    refetchOnMount?: boolean | 'always'
    refetchOnWindowFocus?: boolean | 'always'
    enableFetch?: boolean
    refetchInterval?: number | false
  }
) => {
  const queryClient = useQueryClient()
  const hasRequiredParam = (param: unknown | undefined) => {
    if (param === undefined || param === null) return false
    if (typeof param === 'string') return param.trim().length > 0
    return true
  }

  const allParams = [...requiredParams, ...(optionalParams ?? [])]
  const allParamsString = JSON.stringify(allParams)

  const queryKey = useMemo(
    () => [queryName, allParamsString],
    [queryName, allParamsString]
  )

  const query = useQuery<TData>({
    queryKey,
    queryFn: queryFunction,
    placeholderData: (queryOptions?.useCachedDataAsPlaceholder ?? true) ? (prev) => prev : undefined,
    enabled: requiredParams.every(hasRequiredParam) && (queryOptions?.enableFetch ?? true),
    staleTime: queryOptions?.staleTime ?? FIVE_MINUTES_IN_MILLISECONDS,
    refetchOnMount: queryOptions?.refetchOnMount,
    refetchOnWindowFocus: queryOptions?.refetchOnWindowFocus ?? false,
    refetchInterval: queryOptions?.refetchInterval,
  })

  const setQueryData = (data: TData) => queryClient.setQueryData(queryKey, data)
  const invalidateQuery = (queryKeyToInvalidate?: unknown[]) =>
    queryClient.invalidateQueries({ queryKey: queryKeyToInvalidate ?? queryKey })

  return {
    ...query,
    setQueryData,
    invalidateQuery,
  }
}

// ── Projects ──────────────────────────────────────────────

export function useProjects() {
  return useDataQuery('projects', getProjects, [])
}

export function useProjectDashboard(projectId: string | undefined) {
  return useDataQuery('project-dashboard', () => getProjectDashboard(projectId!), [projectId])
}

export function useProjectDashboardBySlug(slug: string | undefined) {
  return useDataQuery('project-dashboard-slug', () => getProjectDashboardBySlug(slug!), [slug])
}

export function useCheckSlug(name: string) {
  return useDataQuery(
    'check-slug',
    () => checkSlug(name),
    [name],
    [],
    {
      useCachedDataAsPlaceholder: false,
      staleTime: 0,
      enableFetch: name.trim().length > 0,
    },
  )
}

export function useCreateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateProjectRequest) => createProject(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })
}

export function useUpdateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateProjectRequest }) => updateProject(id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

export function useDeleteProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })
}

// ── Work Items ────────────────────────────────────────────

export function useWorkItems(projectId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('work-items', () => getWorkItems(projectId!), [projectId], [], {
    refetchInterval: options?.pollingInterval ?? WORK_ITEMS_POLL_MS,
  })
}

// ── Work Item Levels ──────────────────────────────────────

export function useWorkItemLevels(projectId: string | undefined) {
  return useDataQuery('work-item-levels', () => getWorkItemLevels(projectId!), [projectId])
}

// ── Agents ────────────────────────────────────────────────

export function useExecutions(projectId: string | undefined) {
  return useDataQuery('executions', () => getExecutions(projectId!), [projectId], [], {
    refetchInterval: EXECUTIONS_POLL_MS,
  })
}

export function useLogs(projectId: string | undefined) {
  return useDataQuery('logs', () => getLogs(projectId!), [projectId], [], {
    refetchInterval: LOGS_POLL_MS,
  })
}

export function useClearLogs(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => clearLogs(projectId!),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['logs'] })
    },
  })
}

export function useStartExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workItemNumber: number) => startExecution(projectId!, workItemNumber),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })
}

export function useCancelExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => cancelExecution(projectId!, executionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
    },
  })
}

export function usePauseExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => pauseExecution(projectId!, executionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
    },
  })
}

// ── Chat ──────────────────────────────────────────────────

export function useChatData(projectId: string | undefined) {
  return useDataQuery('chat-data', () => getChatData(projectId), [], [projectId], {
    refetchInterval: CHAT_DATA_POLL_MS,
  })
}

export function useChatMessages(projectId: string | undefined, sessionId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('chat-messages', () => getMessages(projectId, sessionId!), [sessionId], [projectId], {
    refetchInterval: options?.pollingInterval ?? CHAT_MESSAGES_POLL_MS,
  })
}

// ── Search ────────────────────────────────────────────────

export function useSearch(query: string, type?: string) {
  return useDataQuery('search', () => search(query, type), [query], [type])
}

// ── Subscription ──────────────────────────────────────────

export function useSubscription() {
  return useDataQuery('subscription', getSubscription, [])
}

// ── User Settings ─────────────────────────────────────────

export function useUserSettings(enabled = true) {
  return useDataQuery('user-settings', getUserSettings, [], [], { enableFetch: enabled })
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: Partial<UserProfile>) => updateProfile(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
  })
}

export function useUpdatePreferences() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: UserPreferences) => updatePreferences(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
  })
}

export function useLinkGitHub() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { code: string; redirectUri: string; state: string }) => linkGitHub(data.code, data.redirectUri, data.state),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
  })
}

export function useRetryExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => retryExecution(projectId!, executionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })
}

export function useExecutionDocumentation(projectId: string | undefined) {
  return useMutation({
    mutationFn: (executionId: string) => getExecutionDocumentation(projectId!, executionId),
  })
}

export function useUnlinkGitHub() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => unlinkGitHub(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
  })
}

export function useGitHubRepos(enabled = true) {
  return useDataQuery('github-repos', getGitHubRepos, [], [], { enableFetch: enabled })
}

export function useNotifications(unreadOnly = false) {
  return useDataQuery('notifications', () => getNotifications(unreadOnly), [], [unreadOnly])
}

export function useMarkNotificationAsRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (notificationId: number) => markNotificationAsRead(notificationId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}

export function useMarkAllNotificationsAsRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => markAllNotificationsAsRead(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}

// ── Work Item Mutations ───────────────────────────────────

export function useCreateWorkItem(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateWorkItemRequest) => createWorkItem(projectId!, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

export function useUpdateWorkItem(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workItemNumber, data }: { workItemNumber: number; data: UpdateWorkItemRequest }) => updateWorkItem(projectId!, workItemNumber, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

export function useDeleteWorkItem(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workItemNumber: number) => deleteWorkItem(projectId!, workItemNumber),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

// ── Work Item Level Mutations ─────────────────────────────

export function useCreateWorkItemLevel(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateWorkItemLevelRequest) => createWorkItemLevel(projectId!, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-item-levels'] })
    },
  })
}

export function useUpdateWorkItemLevel(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateWorkItemLevelRequest }) => updateWorkItemLevel(projectId!, id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-item-levels'] })
    },
  })
}

export function useDeleteWorkItemLevel(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteWorkItemLevel(projectId!, id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-item-levels'] })
    },
  })
}

// ── Chat Mutations ────────────────────────────────────────

export function useCreateChatSession(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (title: string) => createChatSession(projectId, title),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

export function useBulkUpdateWorkItems(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workItemNumbers, data }: { workItemNumbers: number[]; data: UpdateWorkItemRequest }) =>
      bulkUpdateWorkItems(projectId!, workItemNumbers, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

export function useDeleteSession(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (sessionId: string) => deleteChatSession(projectId, sessionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

export function useSendMessage(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ content, generateWorkItems }: { content: string; generateWorkItems?: boolean }) =>
      sendChatMessage(projectId, sessionId!, content, generateWorkItems),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

// ── Attachment Hooks ──────────────────────────────────────

export function useAttachments(projectId: string | undefined, sessionId: string | undefined) {
  return useDataQuery('chat-attachments', () => getAttachments(projectId, sessionId!), [sessionId], [projectId])
}

export function useUploadAttachment(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => uploadAttachment(projectId, sessionId!, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
    },
  })
}

export function useDeleteAttachment(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) => deleteAttachment(projectId, sessionId!, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
    },
  })
}


