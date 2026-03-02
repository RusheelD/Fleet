import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { Layout } from './components/layout'
import { ProjectsPage } from './pages/ProjectsPage'
import { ProjectDashboardPage } from './pages/ProjectDashboardPage'
import { WorkItemsPage } from './pages/WorkItemsPage'
import { AgentMonitorPage } from './pages/AgentMonitorPage'
import { SettingsPage } from './pages/SettingsPage'
import { SubscriptionPage } from './pages/SubscriptionPage'
import { SearchPage } from './pages/SearchPage'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Redirect root to projects */}
        <Route path="/" element={<Navigate to="/projects" replace />} />

        {/* Global pages with layout */}
        <Route element={<Layout />}>
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/search" element={<SearchPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/subscription" element={<SubscriptionPage />} />
        </Route>

        {/* Project-scoped pages */}
        <Route path="/projects/:projectId" element={<Layout />}>
          <Route index element={<ProjectDashboardPage />} />
          <Route path="work-items" element={<WorkItemsPage />} />
          <Route path="agents" element={<AgentMonitorPage />} />
        </Route>

        {/* Catch-all */}
        <Route path="*" element={<Navigate to="/projects" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
