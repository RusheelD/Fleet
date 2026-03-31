export { ApiError, getApiErrorMessage, setTokenGetter, fetchWithAuth, get, post, put, del, postForm } from './proxy'
export {
  getProjects,
  getProjectDashboard,
  getProjectDashboardBySlug,
  createProject,
  updateProject,
  deleteProject,
  checkSlug,
  exportProjectsFile,
  importProjectsFile,
} from './projectsProxy'
export type { CreateProjectRequest, UpdateProjectRequest, ProjectsImportResult } from './projectsProxy'
export {
  getWorkItems,
  createWorkItem,
  updateWorkItem,
  bulkUpdateWorkItems,
  deleteWorkItem,
  exportWorkItemsFile,
  importWorkItemsFile,
} from './workItemsProxy'
export type { CreateWorkItemRequest, UpdateWorkItemRequest, WorkItemsImportResult } from './workItemsProxy'
export { getWorkItemLevels, createWorkItemLevel, updateWorkItemLevel, deleteWorkItemLevel } from './levelsProxy'
export type { CreateWorkItemLevelRequest, UpdateWorkItemLevelRequest } from './levelsProxy'
export {
  getExecutions,
  getLogs,
  clearLogs,
  startExecution,
  getExecutionStatus,
  cancelExecution,
  pauseExecution,
  steerExecution,
  retryExecution,
  getExecutionDocumentation,
} from './agentsProxy'
export type { ExecutionStatus, ExecutionDocumentation } from './agentsProxy'
export { getChatData, getMessages, createChatSession, sendChatMessage, getAttachments, uploadAttachment, deleteAttachment, deleteChatSession, renameChatSession, cancelChatSessionRequests } from './chatProxy'
export { search } from './searchProxy'
export { getSubscription } from './subscriptionProxy'
export {
  getUserSettings,
  updateProfile,
  updatePreferences,
  getGitHubOAuthState,
  getGitHubOAuthClientId,
  linkGitHub,
  unlinkGitHub,
  setPrimaryGitHubAccount,
  getGitHubRepos,
  createGitHubRepo,
} from './userProxy'
export type { CreateGitHubRepoRequest } from './userProxy'
export { getNotifications, markNotificationAsRead, markAllNotificationsAsRead } from './notificationProxy'
export {
  useDataQuery,
  useProjects,
  useProjectDashboard,
  useProjectDashboardBySlug,
  useCheckSlug,
  useCreateProject,
  useUpdateProject,
  useDeleteProject,
  useExportProjects,
  useImportProjects,
  useWorkItems,
  useExportWorkItems,
  useImportWorkItems,
  useWorkItemLevels,
  useExecutions,
  useLogs,
  useClearLogs,
  useStartExecution,
  useCancelExecution,
  usePauseExecution,
  useRetryExecution,
  useExecutionDocumentation,
  useChatData,
  useChatMessages,
  useSearch,
  useSubscription,
  useUserSettings,
  useUpdateProfile,
  useUpdatePreferences,
  useLinkGitHub,
  useUnlinkGitHub,
  useSetPrimaryGitHubAccount,
  useGitHubRepos,
  useCreateGitHubRepo,
  useCreateWorkItem,
  useUpdateWorkItem,
  useBulkUpdateWorkItems,
  useDeleteWorkItem,
  useCreateWorkItemLevel,
  useUpdateWorkItemLevel,
  useDeleteWorkItemLevel,
  useCreateChatSession,
  useSendMessage,
  useAttachments,
  useUploadAttachment,
  useDeleteAttachment,
  useDeleteSession,
  useRenameSession,
  useNotifications,
  useMarkNotificationAsRead,
  useMarkAllNotificationsAsRead,
} from './dataClient'
export type { DataResult } from './dataClient'
export { resolveIcon } from './iconMap'
export { resolveLevelIcon, LEVEL_ICON_NAMES } from './levelIconMap'
