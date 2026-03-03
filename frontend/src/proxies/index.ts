export { ApiError, setTokenGetter, get, post, put, del } from './proxy'
export { getProjects, getProjectDashboard, getProjectDashboardBySlug, createProject, updateProject, deleteProject, checkSlug } from './projectsProxy'
export type { CreateProjectRequest, UpdateProjectRequest } from './projectsProxy'
export { getWorkItems, createWorkItem, updateWorkItem, deleteWorkItem } from './workItemsProxy'
export type { CreateWorkItemRequest, UpdateWorkItemRequest } from './workItemsProxy'
export { getWorkItemLevels, createWorkItemLevel, updateWorkItemLevel, deleteWorkItemLevel } from './levelsProxy'
export type { CreateWorkItemLevelRequest, UpdateWorkItemLevelRequest } from './levelsProxy'
export { getExecutions, getLogs } from './agentsProxy'
export { getChatData, getMessages, createChatSession, sendChatMessage } from './chatProxy'
export { search } from './searchProxy'
export { getSubscription } from './subscriptionProxy'
export { getUserSettings, updateProfile, updatePreferences, linkGitHub, unlinkGitHub, getGitHubRepos } from './userProxy'
export {
  useDataQuery,
  useProjects,
  useProjectDashboard,
  useProjectDashboardBySlug,
  useCheckSlug,
  useCreateProject,
  useUpdateProject,
  useDeleteProject,
  useWorkItems,
  useWorkItemLevels,
  useExecutions,
  useLogs,
  useChatData,
  useChatMessages,
  useSearch,
  useSubscription,
  useUserSettings,
  useUpdateProfile,
  useUpdatePreferences,
  useLinkGitHub,
  useUnlinkGitHub,
  useGitHubRepos,
  useCreateWorkItem,
  useUpdateWorkItem,
  useDeleteWorkItem,
  useCreateWorkItemLevel,
  useUpdateWorkItemLevel,
  useDeleteWorkItemLevel,
  useCreateChatSession,
  useSendMessage,
  useSeedDatabase,
  useResetDatabase,
} from './dataClient'
export type { DataResult } from './dataClient'
export { resolveIcon } from './iconMap'
export { resolveLevelIcon, LEVEL_ICON_NAMES } from './levelIconMap'
