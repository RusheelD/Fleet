import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { useMemo } from 'react'
import {
  getProjects, getProjectDashboard, getProjectDashboardBySlug, createProject, updateProject, deleteProject, checkSlug,
  getWorkItems, createWorkItem, updateWorkItem, deleteWorkItem,
  getWorkItemLevels, createWorkItemLevel, updateWorkItemLevel, deleteWorkItemLevel,
  getExecutions, getLogs,
  getChatData, getMessages, createChatSession, sendChatMessage,
  getAttachments, uploadAttachment, deleteAttachment,
  search,
  getSubscription,
  getUserSettings, updateProfile, updatePreferences, linkGitHub, unlinkGitHub, getGitHubRepos,
  get,
} from './'
import type {
  CreateProjectRequest, UpdateProjectRequest,
  CreateWorkItemRequest, UpdateWorkItemRequest,
  CreateWorkItemLevelRequest, UpdateWorkItemLevelRequest,
} from './'
import type { UserProfile, UserPreferences } from '../models'

const FIVE_MINUTES_IN_MILLISECONDS = 1000 * 60 * 5

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
  }
) => {
  const queryClient = useQueryClient()

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
    enabled: requiredParams.every(p => p !== undefined && p !== null) && (queryOptions?.enableFetch ?? true),
    staleTime: queryOptions?.staleTime ?? FIVE_MINUTES_IN_MILLISECONDS,
    refetchOnMount: queryOptions?.refetchOnMount,
    refetchOnWindowFocus: queryOptions?.refetchOnWindowFocus ?? false,
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

export function useWorkItems(projectId: string | undefined) {
  return useDataQuery('work-items', () => getWorkItems(projectId!), [projectId])
}

// ── Work Item Levels ──────────────────────────────────────

export function useWorkItemLevels(projectId: string | undefined) {
  return useDataQuery('work-item-levels', () => getWorkItemLevels(projectId!), [projectId])
}

// ── Agents ────────────────────────────────────────────────

export function useExecutions(projectId: string | undefined) {
  return useDataQuery('executions', () => getExecutions(projectId!), [projectId])
}

export function useLogs(projectId: string | undefined) {
  return useDataQuery('logs', () => getLogs(projectId!), [projectId])
}

// ── Chat ──────────────────────────────────────────────────

export function useChatData(projectId: string | undefined) {
  return useDataQuery('chat-data', () => getChatData(projectId!), [projectId])
}

export function useChatMessages(projectId: string | undefined, sessionId: string | undefined) {
  return useDataQuery('chat-messages', () => getMessages(projectId!, sessionId!), [projectId, sessionId])
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
    mutationFn: (data: { code: string; redirectUri: string }) => linkGitHub(data.code, data.redirectUri),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
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
    mutationFn: ({ id, data }: { id: number; data: UpdateWorkItemRequest }) => updateWorkItem(projectId!, id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
    },
  })
}

export function useDeleteWorkItem(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteWorkItem(projectId!, id),
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
    mutationFn: (title: string) => createChatSession(projectId!, title),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

export function useSendMessage(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string) => sendChatMessage(projectId!, sessionId!, content),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

// ── Attachment Hooks ──────────────────────────────────────

export function useAttachments(projectId: string | undefined, sessionId: string | undefined) {
  return useDataQuery('chat-attachments', () => getAttachments(projectId!, sessionId!), [projectId, sessionId])
}

export function useUploadAttachment(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => uploadAttachment(projectId!, sessionId!, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
    },
  })
}

export function useDeleteAttachment(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) => deleteAttachment(projectId!, sessionId!, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-attachments'] })
    },
  })
}

// ── Admin / Seed Mutations ────────────────────────────────

export function useSeedDatabase() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => get<{ message: string }>('/api/admin/seed'),
    onSuccess: () => {
      void queryClient.invalidateQueries()
    },
  })
}

export function useResetDatabase() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => get<{ message: string }>('/api/admin/reset'),
    onSuccess: () => {
      void queryClient.invalidateQueries()
    },
  })
}
