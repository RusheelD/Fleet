import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { normalizeChatSessionActivities } from '../models/chat'
import {
  getProjects, getProjectDashboard, getProjectDashboardBySlug, createProject, updateProject, deleteProject, checkSlug, exportProjectsFile, importProjectsFile,
} from './projectsProxy'
import type { CreateProjectRequest, UpdateProjectRequest } from './projectsProxy'
import {
  getWorkItems, createWorkItem, updateWorkItem, bulkUpdateWorkItems, bulkDeleteWorkItems, deleteWorkItem, exportWorkItemsFile, importWorkItemsFile,
  getWorkItemAttachments, uploadWorkItemAttachment, deleteWorkItemAttachment,
} from './workItemsProxy'
import type { CreateWorkItemRequest, UpdateWorkItemRequest } from './workItemsProxy'
import { getWorkItemLevels, createWorkItemLevel, updateWorkItemLevel, deleteWorkItemLevel } from './levelsProxy'
import type { CreateWorkItemLevelRequest, UpdateWorkItemLevelRequest } from './levelsProxy'
import {
  getExecutions, getLogs, clearLogs, clearExecutionLogs, startExecution, cancelExecution, pauseExecution, resumeExecution, retryExecution, deleteExecution, getExecutionDocumentation,
} from './agentsProxy'
import { getChatData, getMessages, createChatSession, sendChatMessage, cancelChatGeneration, getAttachments, uploadAttachment, deleteAttachment, deleteChatSession, renameChatSession, updateChatSessionDynamicIteration } from './chatProxy'
import type { UpdateSessionDynamicIterationRequest } from './chatProxy'
import { search } from './searchProxy'
import { getSubscription } from './subscriptionProxy'
import {
  getUserSettings, updateProfile, updatePreferences, getUserMemories, createUserMemory, updateUserMemory, deleteUserMemory,
  getProjectMemories, createProjectMemory, updateProjectMemory, deleteProjectMemory,
  getSkillTemplates, getUserSkills, createUserSkill, updateUserSkill, deleteUserSkill,
  getProjectSkills, createProjectSkill, updateProjectSkill, deleteProjectSkill,
  linkGitHub, unlinkGitHub, setPrimaryGitHubAccount, getGitHubRepos, createGitHubRepo,
  getMcpServers, getMcpServerTemplates, getSystemMcpServers, createMcpServer, updateMcpServer, deleteMcpServer, validateMcpServer,
} from './userProxy'
import { getNotifications, markNotificationAsRead, markAllNotificationsAsRead } from './notificationProxy'
import type { AgentExecution, AgentInfo, ChatAttachment, ChatData, ChatDynamicPolicy, ChatDynamicStrategy, LogEntry, UserProfile, UserPreferences, WorkItemAttachment } from '../models'
import {
  findExecutionInCollection,
  patchExecutionCollection,
  removeExecutionCollection,
  upsertExecutionCollectionWithFallback,
} from '../models/executionTree'

const FIVE_MINUTES_IN_MILLISECONDS = 1000 * 60 * 5
const WORK_ITEMS_POLL_MS = 15000
const EXECUTIONS_POLL_MS = 10000
const LOGS_POLL_MS = 10000
const CHAT_DATA_POLL_MS = 4000
const CHAT_MESSAGES_POLL_MS = 5000
const NOTIFICATIONS_POLL_MS = 15000

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
    refetchOnReconnect?: boolean | 'always'
    enableFetch?: boolean
    refetchInterval?: number | false
    refetchIntervalInBackground?: boolean
  }
) => {
  const queryClient = useQueryClient()
  const hasRequiredParam = (param: unknown | undefined) => {
    if (param === undefined || param === null) return false
    if (typeof param === 'string') return param.trim().length > 0
    return true
  }

  const queryKey = [queryName, ...requiredParams, ...(optionalParams ?? [])]

  const query = useQuery<TData>({
    queryKey,
    queryFn: queryFunction,
    placeholderData: (queryOptions?.useCachedDataAsPlaceholder ?? true) ? (prev) => prev : undefined,
    enabled: requiredParams.every(hasRequiredParam) && (queryOptions?.enableFetch ?? true),
    staleTime: queryOptions?.staleTime ?? FIVE_MINUTES_IN_MILLISECONDS,
    refetchOnMount: queryOptions?.refetchOnMount,
    refetchOnWindowFocus: queryOptions?.refetchOnWindowFocus ?? false,
    refetchOnReconnect: queryOptions?.refetchOnReconnect ?? true,
    refetchInterval: queryOptions?.refetchInterval,
    refetchIntervalInBackground: queryOptions?.refetchIntervalInBackground ?? false,
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
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
    },
  })
}

// ── Work Items ────────────────────────────────────────────

export function useWorkItems(projectId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('work-items', () => getWorkItems(projectId!), [projectId], [], {
    refetchInterval: options?.pollingInterval ?? WORK_ITEMS_POLL_MS,
    refetchIntervalInBackground: true,
  })
}

export function useExportWorkItems(projectId: string | undefined) {
  return useMutation({
    mutationFn: () => exportWorkItemsFile(projectId!),
  })
}

export function useImportWorkItems(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: unknown) => importWorkItemsFile(projectId!, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })
}

export function useExportProjects() {
  return useMutation({
    mutationFn: () => exportProjectsFile(),
  })
}

export function useImportProjects() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: unknown) => importProjectsFile(payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
    },
  })
}

// ── Work Item Levels ──────────────────────────────────────

export function useWorkItemLevels(projectId: string | undefined) {
  return useDataQuery('work-item-levels', () => getWorkItemLevels(projectId!), [projectId])
}

// ── Agents ────────────────────────────────────────────────

export function useExecutions(projectId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('executions', () => getExecutions(projectId!), [projectId], [], {
    staleTime: 0,
    refetchOnWindowFocus: false,
    refetchOnReconnect: true,
    refetchInterval: options?.pollingInterval ?? EXECUTIONS_POLL_MS,
    refetchIntervalInBackground: true,
  })
}

export function useLogs(projectId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('logs', () => getLogs(projectId!), [projectId], [], {
    refetchOnWindowFocus: false,
    refetchOnReconnect: true,
    refetchInterval: options?.pollingInterval ?? LOGS_POLL_MS,
    refetchIntervalInBackground: true,
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

export function useClearExecutionLogs(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => clearExecutionLogs(projectId!, executionId),
    onMutate: async (executionId: string) => {
      await queryClient.cancelQueries({ queryKey: ['logs'] })

      const previousLogs = queryClient.getQueriesData<LogEntry[]>({ queryKey: ['logs'] })

      queryClient.setQueriesData<LogEntry[]>(
        { queryKey: ['logs'] },
        (current) => {
          if (!Array.isArray(current)) return current
          return current.filter((log) => log.executionId !== executionId)
        },
      )

      return { previousLogs }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['logs'] })
    },
    onError: (_error, _executionId, context) => {
      if (context?.previousLogs) {
        for (const [queryKey, snapshot] of context.previousLogs) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
    },
  })
}

export function useStartExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { workItemNumber: number; targetBranch?: string }) =>
      startExecution(projectId!, request),
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

export function useResumeExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => resumeExecution(projectId!, executionId),
    onMutate: async (executionId: string) => {
      await queryClient.cancelQueries({ queryKey: ['executions'] })
      await queryClient.cancelQueries({ queryKey: ['logs'] })

      const previousExecutions = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })
      const previousLogs = queryClient.getQueriesData<LogEntry[]>({ queryKey: ['logs'] })
      const nowIso = new Date().toISOString()

      queryClient.setQueriesData<AgentExecution[]>(
        { queryKey: ['executions'] },
        (current) => {
          if (!Array.isArray(current)) return current
          const updated = patchExecutionCollection(current, executionId, {
            status: 'running',
            currentPhase: 'Resuming paused execution',
          })

          return updated.found ? updated.executions : current
        },
      )

      queryClient.setQueriesData<LogEntry[]>(
        { queryKey: ['logs'] },
        (current) => {
          if (!Array.isArray(current)) return current
          return [
            ...current,
            {
              time: nowIso,
              agent: 'System',
              level: 'info',
              message: `Resume requested for execution ${executionId}`,
              isDetailed: false,
              executionId,
            },
          ]
        },
      )

      return { previousExecutions, previousLogs }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['logs'] })
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
    onError: (_error, _executionId, context) => {
      if (context?.previousExecutions) {
        for (const [queryKey, snapshot] of context.previousExecutions) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
      if (context?.previousLogs) {
        for (const [queryKey, snapshot] of context.previousLogs) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
    },
  })
}

// ── Chat ──────────────────────────────────────────────────

export function useChatData(projectId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('chat-data', () => getChatData(projectId), [], [projectId], {
    refetchInterval: options?.pollingInterval ?? CHAT_DATA_POLL_MS,
    refetchIntervalInBackground: true,
  })
}

export function useChatMessages(projectId: string | undefined, sessionId: string | undefined, options?: { pollingInterval?: number | false }) {
  return useDataQuery('chat-messages', () => getMessages(projectId, sessionId!), [sessionId], [projectId], {
    refetchInterval: options?.pollingInterval ?? CHAT_MESSAGES_POLL_MS,
    refetchIntervalInBackground: true,
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

export function useUserMemories(enabled = true) {
  return useDataQuery('user-memories', getUserMemories, [], [], { enableFetch: enabled, staleTime: 0 })
}

export function useCreateUserMemory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: createUserMemory,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-memories'] })
    },
  })
}

export function useUpdateUserMemory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: Parameters<typeof updateUserMemory>[1] }) => updateUserMemory(id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-memories'] })
    },
  })
}

export function useDeleteUserMemory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteUserMemory(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-memories'] })
    },
  })
}

export function useProjectMemories(projectId: string | undefined, enabled = true) {
  return useDataQuery('project-memories', () => getProjectMemories(projectId!), [projectId], [], { enableFetch: enabled, staleTime: 0 })
}

export function useCreateProjectMemory(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: Parameters<typeof createProjectMemory>[1]) => createProjectMemory(projectId!, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-memories'] })
    },
  })
}

export function useUpdateProjectMemory(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: Parameters<typeof updateProjectMemory>[2] }) => updateProjectMemory(projectId!, id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-memories'] })
    },
  })
}

export function useDeleteProjectMemory(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteProjectMemory(projectId!, id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-memories'] })
    },
  })
}

export function useSkillTemplates(enabled = true) {
  return useDataQuery('skill-templates', getSkillTemplates, [], [], { enableFetch: enabled })
}

export function useUserSkills(enabled = true) {
  return useDataQuery('user-skills', getUserSkills, [], [], { enableFetch: enabled, staleTime: 0 })
}

export function useCreateUserSkill() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: createUserSkill,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-skills'] })
    },
  })
}

export function useUpdateUserSkill() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: Parameters<typeof updateUserSkill>[1] }) => updateUserSkill(id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-skills'] })
    },
  })
}

export function useDeleteUserSkill() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteUserSkill(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-skills'] })
    },
  })
}

export function useProjectSkills(projectId: string | undefined, enabled = true) {
  return useDataQuery('project-skills', () => getProjectSkills(projectId!), [projectId], [], { enableFetch: enabled, staleTime: 0 })
}

export function useCreateProjectSkill(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: Parameters<typeof createProjectSkill>[1]) => createProjectSkill(projectId!, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-skills'] })
    },
  })
}

export function useUpdateProjectSkill(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: Parameters<typeof updateProjectSkill>[2] }) => updateProjectSkill(projectId!, id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-skills'] })
    },
  })
}

export function useDeleteProjectSkill(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteProjectSkill(projectId!, id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-skills'] })
    },
  })
}

export function useRetryExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => retryExecution(projectId!, executionId),
    onMutate: async (executionId: string) => {
      await queryClient.cancelQueries({ queryKey: ['executions'] })
      await queryClient.cancelQueries({ queryKey: ['logs'] })

      const previousExecutions = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })
      const previousLogs = queryClient.getQueriesData<LogEntry[]>({ queryKey: ['logs'] })

      const nowIso = new Date().toISOString()
      const optimisticId = `retry-pending-${executionId}-${Date.now()}`
      const sourceExecution = previousExecutions
        .map(([, executions]) => Array.isArray(executions) ? findExecutionInCollection(executions, executionId) : undefined)
        .find((execution): execution is AgentExecution => Boolean(execution))

      const optimisticAgents: AgentInfo[] = sourceExecution
        ? sourceExecution.agents.map((agent, index) => ({
          ...agent,
          status: index === 0 ? 'running' : 'idle',
          currentTask: index === 0 ? 'Retry queued' : 'Waiting to start',
          progress: 0,
        }))
        : []

      const optimisticExecution: AgentExecution = {
        id: optimisticId,
        workItemId: sourceExecution?.workItemId ?? 0,
        workItemTitle: sourceExecution?.workItemTitle ?? 'Retrying execution',
        executionMode: sourceExecution?.executionMode ?? 'standard',
        status: 'queued',
        agents: optimisticAgents,
        startedAt: nowIso,
        duration: 'starting...',
        progress: 0,
        branchName: sourceExecution?.branchName ?? null,
        pullRequestUrl: sourceExecution?.pullRequestUrl ?? null,
        currentPhase: 'Retry queued',
        reviewLoopCount: 0,
        lastReviewRecommendation: null,
        parentExecutionId: sourceExecution?.parentExecutionId ?? null,
        subFlows: [],
      }

      queryClient.setQueriesData<AgentExecution[]>(
        { queryKey: ['executions'] },
        (current) => {
          if (!Array.isArray(current)) return current
          if (findExecutionInCollection(current, optimisticId)) return current
          return upsertExecutionCollectionWithFallback(current, optimisticExecution).executions
        },
      )

      const optimisticLogMessage = sourceExecution
        ? `Retry requested for execution ${executionId} (work item #${sourceExecution.workItemId})`
        : `Retry requested for execution ${executionId}`

      queryClient.setQueriesData<LogEntry[]>(
        { queryKey: ['logs'] },
        (current) => {
          if (!Array.isArray(current)) return current
          return [
            ...current,
            {
              time: nowIso,
              agent: 'System',
              level: 'info',
              message: optimisticLogMessage,
              isDetailed: false,
              executionId: optimisticId,
            },
          ]
        },
      )

      return { previousExecutions, previousLogs, optimisticId }
    },
    onSuccess: (result, executionId, context) => {
      if (context?.optimisticId) {
        queryClient.setQueriesData<AgentExecution[]>(
          { queryKey: ['executions'] },
          (current) => {
            if (!Array.isArray(current)) return current
            const updated = patchExecutionCollection(current, context.optimisticId, {
              id: result.executionId,
              status: 'running',
              currentPhase: 'Initializing retry',
            })

            return updated.found ? updated.executions : current
          },
        )

        queryClient.setQueriesData<LogEntry[]>(
          { queryKey: ['logs'] },
          (current) => {
            if (!Array.isArray(current)) return current
            return current.map((log) =>
              log.executionId === context.optimisticId
                ? {
                  ...log,
                  executionId: result.executionId,
                  message: `Retry started for execution ${executionId} (new execution ${result.executionId})`,
                }
                : log,
            )
          },
        )
      }

      void queryClient.invalidateQueries({ queryKey: ['logs'] })
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
    onError: (_error, _executionId, context) => {
      if (context?.previousExecutions) {
        for (const [queryKey, snapshot] of context.previousExecutions) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
      if (context?.previousLogs) {
        for (const [queryKey, snapshot] of context.previousLogs) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
    },
  })
}

export function useDeleteExecution(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (executionId: string) => deleteExecution(projectId!, executionId),
    onMutate: async (executionId: string) => {
      await queryClient.cancelQueries({ queryKey: ['executions'] })
      await queryClient.cancelQueries({ queryKey: ['logs'] })

      const previousExecutions = queryClient.getQueriesData<AgentExecution[]>({ queryKey: ['executions'] })
      const previousLogs = queryClient.getQueriesData<LogEntry[]>({ queryKey: ['logs'] })

      queryClient.setQueriesData<AgentExecution[]>(
        { queryKey: ['executions'] },
        (current) => {
          if (!Array.isArray(current)) return current
          return removeExecutionCollection(current, executionId).executions
        },
      )

      queryClient.setQueriesData<LogEntry[]>(
        { queryKey: ['logs'] },
        (current) => {
          if (!Array.isArray(current)) return current
          return current.filter((log) => log.executionId !== executionId)
        },
      )

      return { previousExecutions, previousLogs }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['logs'] })
      void queryClient.invalidateQueries({ queryKey: ['executions'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
    onError: (_error, _executionId, context) => {
      if (context?.previousExecutions) {
        for (const [queryKey, snapshot] of context.previousExecutions) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }

      if (context?.previousLogs) {
        for (const [queryKey, snapshot] of context.previousLogs) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
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
    mutationFn: (accountId?: number) => unlinkGitHub(accountId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
      void queryClient.invalidateQueries({ queryKey: ['github-repos'] })
    },
  })
}

export function useSetPrimaryGitHubAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (accountId: number) => setPrimaryGitHubAccount(accountId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
      void queryClient.invalidateQueries({ queryKey: ['github-repos'] })
    },
  })
}

export function useGitHubRepos(enabled = true, accountId?: number) {
  return useDataQuery(
    'github-repos',
    () => getGitHubRepos(accountId),
    [],
    [accountId],
    { enableFetch: enabled },
  )
}

export function useCreateGitHubRepo() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { name: string; description?: string; private: boolean; accountId?: number }) =>
      createGitHubRepo(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['github-repos'] })
      void queryClient.invalidateQueries({ queryKey: ['user-settings'] })
    },
  })
}

export function useMcpServers(enabled = true) {
  return useDataQuery('mcp-servers', getMcpServers, [], [], { enableFetch: enabled, staleTime: 0 })
}

export function useMcpServerTemplates(enabled = true) {
  return useDataQuery('mcp-server-templates', getMcpServerTemplates, [], [], { enableFetch: enabled })
}

export function useSystemMcpServers(enabled = true) {
  return useDataQuery('system-mcp-servers', getSystemMcpServers, [], [], { enableFetch: enabled })
}

export function useCreateMcpServer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: createMcpServer,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['mcp-servers'] })
    },
  })
}

export function useUpdateMcpServer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: Parameters<typeof updateMcpServer>[1] }) => updateMcpServer(id, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['mcp-servers'] })
    },
  })
}

export function useDeleteMcpServer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteMcpServer(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['mcp-servers'] })
    },
  })
}

export function useValidateMcpServer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => validateMcpServer(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['mcp-servers'] })
    },
  })
}

export function useNotifications(unreadOnly = false) {
  return useDataQuery('notifications', () => getNotifications(unreadOnly), [], [unreadOnly], {
    staleTime: 0,
    refetchOnWindowFocus: true,
    refetchInterval: NOTIFICATIONS_POLL_MS,
    refetchIntervalInBackground: true,
  })
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
      void queryClient.invalidateQueries({ queryKey: ['work-item-attachments'] })
    },
  })
}

export function useWorkItemAttachments(projectId: string | undefined, workItemNumber: number | undefined) {
  return useDataQuery(
    'work-item-attachments',
    () => getWorkItemAttachments(projectId!, workItemNumber!),
    [projectId, workItemNumber],
  )
}

function buildWorkItemAttachmentsQueryKey(projectId: string | undefined, workItemNumber: number) {
  return ['work-item-attachments', JSON.stringify([projectId, workItemNumber])]
}

export function useUploadWorkItemAttachment(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workItemNumber, file }: { workItemNumber: number; file: File }) =>
      uploadWorkItemAttachment(projectId!, workItemNumber, file),
    onSuccess: (attachment, variables) => {
      queryClient.setQueryData<WorkItemAttachment[]>(
        buildWorkItemAttachmentsQueryKey(projectId, variables.workItemNumber),
        (current) => {
          const existing = current ?? []
          return [attachment, ...existing.filter((item) => item.id !== attachment.id)]
        },
      )
      void queryClient.invalidateQueries({ queryKey: ['work-item-attachments'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
    },
  })
}

export function useDeleteWorkItemAttachment(projectId: string | undefined, workItemNumber: number | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) => deleteWorkItemAttachment(projectId!, workItemNumber!, attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-item-attachments'] })
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

export function useBulkDeleteWorkItems(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workItemNumbers: number[]) => bulkDeleteWorkItems(projectId!, workItemNumbers),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
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

export function useRenameSession(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, title }: { sessionId: string; title: string }) =>
      renameChatSession(projectId, sessionId, title),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
  })
}

export function useUpdateSessionDynamicIteration(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, data }: { sessionId: string; data: UpdateSessionDynamicIterationRequest }) =>
      updateChatSessionDynamicIteration(projectId, sessionId, data),
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey: ['chat-data'] })

      const previousChatData = queryClient.getQueriesData<ChatData>({ queryKey: ['chat-data'] })

      queryClient.setQueriesData<ChatData>(
        { queryKey: ['chat-data'] },
        (current) => patchChatDataDynamicIteration(current, variables.sessionId, variables.data),
      )

      return { previousChatData }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
    },
    onError: (_error, _variables, context) => {
      if (context?.previousChatData) {
        for (const [queryKey, snapshot] of context.previousChatData) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
    },
  })
}

function patchChatDataDynamicIteration(
  current: ChatData | undefined,
  sessionId: string,
  data: UpdateSessionDynamicIterationRequest,
): ChatData | undefined {
  if (!current) return current

  const policy = parseChatDynamicPolicy(data.dynamicIterationPolicyJson)
  const strategy = normalizeChatDynamicStrategy(policy?.executionPolicy)
    ?? normalizeChatDynamicStrategy(policy?.strategy)

  return {
    ...current,
    sessions: current.sessions.map((session) =>
      session.id === sessionId
        ? {
          ...session,
          isDynamicIterationEnabled: data.isDynamicIterationEnabled,
          dynamicIterationBranch: data.dynamicIterationBranch ?? null,
          dynamicIterationPolicyJson: data.dynamicIterationPolicyJson ?? null,
          dynamicOptions: {
            enabled: data.isDynamicIterationEnabled,
            branchName: data.dynamicIterationBranch ?? null,
            strategy,
          },
          dynamicPolicy: policy,
        }
        : session,
    ),
  }
}

function parseChatDynamicPolicy(policyJson: string | null | undefined): ChatDynamicPolicy | null {
  if (!policyJson) return null

  try {
    return JSON.parse(policyJson) as ChatDynamicPolicy
  } catch {
    return null
  }
}

function normalizeChatDynamicStrategy(value: unknown): ChatDynamicStrategy | null {
  return value === 'balanced' || value === 'parallel' || value === 'sequential'
    ? value
    : null
}

export function useCancelChatGeneration(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (sessionId: string) => cancelChatGeneration(projectId, sessionId),
    onMutate: async (sessionId: string) => {
      await queryClient.cancelQueries({ queryKey: ['chat-data'] })

      const previousChatData = queryClient.getQueriesData<ChatData>({ queryKey: ['chat-data'] })

      queryClient.setQueriesData<ChatData>(
        { queryKey: ['chat-data'] },
        (current) => {
          if (!current) return current
          return {
            ...current,
            sessions: current.sessions.map((session) =>
              session.id === sessionId
                ? {
                  ...session,
                  isGenerating: false,
                  generationState: 'canceled',
                  generationStatus: 'Generation canceled.',
                  generationUpdatedAtUtc: new Date().toISOString(),
                  recentActivity: [
                    ...normalizeChatSessionActivities(session.recentActivity),
                    {
                      id: `cancel-${sessionId}-${Date.now()}`,
                      kind: 'status' as const,
                      message: 'Generation canceled.',
                      timestampUtc: new Date().toISOString(),
                    },
                  ].slice(-16),
                }
                : session,
            ),
          }
        },
      )

      return { previousChatData }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['chat-data'] })
      void queryClient.invalidateQueries({ queryKey: ['chat-messages'] })
      void queryClient.invalidateQueries({ queryKey: ['work-items'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['project-dashboard-slug'] })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
    onError: (_error, _sessionId, context) => {
      if (context?.previousChatData) {
        for (const [queryKey, snapshot] of context.previousChatData) {
          queryClient.setQueryData(queryKey, snapshot)
        }
      }
    },
  })
}

export function useSendMessage(projectId: string | undefined, sessionId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ content, generateWorkItems }: { content: string; generateWorkItems?: boolean }) =>
      sendChatMessage(projectId, sessionId!, { content, generateWorkItems }),
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

function buildAttachmentsQueryKey(projectId: string | undefined, sessionId: string) {
  return ['chat-attachments', JSON.stringify([sessionId, projectId])]
}

export function useUploadAttachment(projectId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, file }: { sessionId: string; file: File }) => uploadAttachment(projectId, sessionId, file),
    onSuccess: (attachment, variables) => {
      queryClient.setQueryData<ChatAttachment[]>(
        buildAttachmentsQueryKey(projectId, variables.sessionId),
        (current) => {
          const existing = current ?? []
          return [attachment, ...existing.filter((item) => item.id !== attachment.id)]
        },
      )
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
