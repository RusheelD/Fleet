import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { Layout } from './components/layout'
import { ProtectedRoute } from './components/shared'
import { ProjectsPage } from './pages/ProjectsPage'
import { ProjectDashboardPage } from './pages/ProjectDashboardPage'
import { WorkItemsPage } from './pages/WorkItemsPage'
import { AgentMonitorPage } from './pages/AgentMonitorPage'
import { SettingsPage } from './pages/SettingsPage'
import { SubscriptionPage } from './pages/SubscriptionPage'
import { SearchPage } from './pages/SearchPage'
import { LoginPage } from './pages/LoginPage'
import { SignUpPage } from './pages/SignUpPage'
import { GitHubCallbackPage } from './pages/GitHubCallbackPage'

function App() {
  return (
    <BrowserRouter>
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
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/subscription" element={<SubscriptionPage />} />
          </Route>

          {/* Project-scoped pages */}
          <Route path="/projects/:slug" element={<Layout />}>
            <Route index element={<ProjectDashboardPage />} />
            <Route path="work-items" element={<WorkItemsPage />} />
            <Route path="agents" element={<AgentMonitorPage />} />
          </Route>
        </Route>

        {/* Catch-all */}
        <Route path="*" element={<Navigate to="/projects" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export { App }
