export interface NotificationEvent {
  id: number
  type: string
  title: string
  message: string
  projectId: string
  executionId?: string | null
  isRead: boolean
  createdAtUtc: string
}
