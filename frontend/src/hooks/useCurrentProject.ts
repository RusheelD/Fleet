import { useParams } from 'react-router-dom'
import { useProjectDashboardBySlug } from '../proxies'

/**
 * Resolves the current project from the `:slug` route param.
 * Returns the project's internal ID, slug, title, and loading state.
 * All project-scoped pages should use this hook to get the projectId for API calls.
 */
export function useCurrentProject() {
  const { slug } = useParams<{ slug: string }>()
  const { data: dashboard, isLoading, isError } = useProjectDashboardBySlug(slug)

  return {
    slug,
    projectId: dashboard?.id,
    projectTitle: dashboard?.title,
    isLoading,
    isError,
  }
}
