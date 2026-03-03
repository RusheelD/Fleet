export interface DashboardActivity {
  icon: string
  text: string
  time: string
}

export interface DashboardMetric {
  icon: string
  label: string
  value: string
  subtext: string
  progress?: number
}

export interface DashboardAgent {
  name: string
  status: string
  task: string
  progress: number
}

export interface ProjectDashboard {
  id: string
  slug: string
  title: string
  repo: string
  metrics: DashboardMetric[]
  activities: DashboardActivity[]
  agents: DashboardAgent[]
}
