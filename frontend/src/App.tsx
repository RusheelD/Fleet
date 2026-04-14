import { Suspense, lazy } from 'react'
import { Spinner, makeStyles } from '@fluentui/react-components'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { Layout } from './components/layout'
import { ErrorBoundary, ProtectedRoute } from './components/shared'

const ProjectsPage = lazy(() => import('./pages/ProjectsPage').then((module) => ({ default: module.ProjectsPage })))
const ProjectDashboardPage = lazy(() => import('./pages/ProjectDashboardPage').then((module) => ({ default: module.ProjectDashboardPage })))
const WorkItemsPage = lazy(() => import('./pages/WorkItemsPage').then((module) => ({ default: module.WorkItemsPage })))
const AgentMonitorPage = lazy(() => import('./pages/AgentMonitorPage').then((module) => ({ default: module.AgentMonitorPage })))
const ProjectMemoryPage = lazy(() => import('./pages/ProjectMemoryPage').then((module) => ({ default: module.ProjectMemoryPage })))
const ProjectPlaybooksPage = lazy(() => import('./pages/ProjectPlaybooksPage').then((module) => ({ default: module.ProjectPlaybooksPage })))
const SettingsPage = lazy(() => import('./pages/SettingsPage').then((module) => ({ default: module.SettingsPage })))
const SubscriptionPage = lazy(() => import('./pages/SubscriptionPage').then((module) => ({ default: module.SubscriptionPage })))
const SearchPage = lazy(() => import('./pages/SearchPage').then((module) => ({ default: module.SearchPage })))
const NotificationsPage = lazy(() => import('./pages/NotificationsPage').then((module) => ({ default: module.NotificationsPage })))
const IntegrationsPage = lazy(() => import('./pages/IntegrationsPage').then((module) => ({ default: module.IntegrationsPage })))
const MemoryPage = lazy(() => import('./pages/MemoryPage').then((module) => ({ default: module.MemoryPage })))
const PlaybooksPage = lazy(() => import('./pages/PlaybooksPage').then((module) => ({ default: module.PlaybooksPage })))
const LoginPage = lazy(() => import('./pages/LoginPage').then((module) => ({ default: module.LoginPage })))
const SignUpPage = lazy(() => import('./pages/SignUpPage').then((module) => ({ default: module.SignUpPage })))
const GitHubCallbackPage = lazy(() => import('./pages/GitHubCallbackPage').then((module) => ({ default: module.GitHubCallbackPage })))

const useStyles = makeStyles({
  loadingShell: {
    minHeight: '100vh',
    display: 'grid',
    placeItems: 'center',
  },
})

function App() {
  const styles = useStyles()

  return (
    <BrowserRouter>
      <ErrorBoundary>
        <Suspense fallback={<div className={styles.loadingShell}><Spinner label="Loading Fleet..." /></div>}>
          <Routes>
            {/* Public auth routes */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignUpPage />} />

            {/* GitHub OAuth callback (protected — needs auth to link) */}
            <Route element={<ProtectedRoute />}>
              <Route path="/auth/github/callback" element={<GitHubCallbackPage />} />
            </Route>

            {/* Redirect root to projects */}
            <Route path="/" element={<Navigate to="/projects" replace />} />

            {/* Protected routes */}
            <Route element={<ProtectedRoute />}>
              {/* Global pages with layout */}
              <Route element={<Layout />}>
                <Route path="/projects" element={<ProjectsPage />} />
                <Route path="/search" element={<SearchPage />} />
                <Route path="/notifications" element={<NotificationsPage />} />
                <Route path="/integrations" element={<IntegrationsPage />} />
                <Route path="/memory" element={<MemoryPage />} />
                <Route path="/playbooks" element={<PlaybooksPage />} />
                <Route path="/settings" element={<SettingsPage />} />
                <Route path="/subscription" element={<SubscriptionPage />} />
              </Route>

              {/* Project-scoped pages */}
              <Route path="/projects/:slug" element={<Layout />}>
                <Route index element={<ProjectDashboardPage />} />
                <Route path="work-items" element={<WorkItemsPage />} />
                <Route path="agents" element={<AgentMonitorPage />} />
                <Route path="memory" element={<ProjectMemoryPage />} />
                <Route path="playbooks" element={<ProjectPlaybooksPage />} />
              </Route>
            </Route>

            {/* Catch-all */}
            <Route path="*" element={<Navigate to="/projects" replace />} />
          </Routes>
        </Suspense>
      </ErrorBoundary>
    </BrowserRouter>
  )
}

export { App }
